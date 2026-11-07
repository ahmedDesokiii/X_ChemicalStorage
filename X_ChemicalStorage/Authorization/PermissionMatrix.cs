using Microsoft.Extensions.Logging.Abstractions;

namespace X_ChemicalStorage.Authorization
{
    /// <summary>Reads and saves the role / user permission grids shown in the admin screens.</summary>
    public static class PermissionMatrix
    {
        public static async Task<PermissionMatrixViewModel> ForRoleAsync(ApplicationDbContext db, int roleId, bool isAdmin)
        {
            var permissions = await ActivePermissionsAsync(db);
            var assigned = (await db.RolePermissions.Where(rp => rp.RoleId == roleId)
                                                     .Select(rp => rp.PermissionId).ToListAsync()).ToHashSet();

            return Build(permissions, isAdmin, (p, item) =>
            {
                item.IsSelected = isAdmin || assigned.Contains(p.Id);
                item.IsEffective = item.IsSelected;
            });
        }

        public static async Task<PermissionMatrixViewModel> ForUserAsync(ApplicationDbContext db, int userId, bool isAdmin)
        {
            var permissions = await ActivePermissionsAsync(db);
            var roleIds = db.UserRoles.Where(ur => ur.UserId == userId).Select(ur => ur.RoleId);
            var fromRoles = (await db.RolePermissions.Where(rp => roleIds.Contains(rp.RoleId))
                                                      .Select(rp => rp.PermissionId).Distinct().ToListAsync()).ToHashSet();
            var overrides = await db.UserPermissions.Where(up => up.UserId == userId)
                                                     .ToDictionaryAsync(up => up.PermissionId, up => up.IsGranted);

            return Build(permissions, isAdmin, (p, item) =>
            {
                item.FromRole = isAdmin || fromRoles.Contains(p.Id);
                item.Override = overrides.TryGetValue(p.Id, out var granted)
                    ? (granted ? PermissionOverride.Grant : PermissionOverride.Deny)
                    : PermissionOverride.Inherit;
                item.IsEffective = isAdmin || item.Override switch
                {
                    PermissionOverride.Grant => true,
                    PermissionOverride.Deny => false,
                    _ => item.FromRole
                };
            });
        }

        /// <summary>Replaces the role's permissions with <paramref name="selectedIds"/>. Returns the number of changes.</summary>
        public static async Task<int> SaveRoleAsync(ApplicationDbContext db, int roleId, IEnumerable<int> selectedIds, string? by)
        {
            var valid = (await db.AppPermissions.Where(p => p.IsActive).Select(p => p.Id).ToListAsync()).ToHashSet();
            var wanted = selectedIds.Where(valid.Contains).ToHashSet();
            var current = await db.RolePermissions.Where(rp => rp.RoleId == roleId).ToListAsync();

            var toRemove = current.Where(rp => !wanted.Contains(rp.PermissionId) && valid.Contains(rp.PermissionId)).ToList();
            var toAdd = wanted.Except(current.Select(rp => rp.PermissionId))
                              .Select(id => new RolePermission { RoleId = roleId, PermissionId = id, GrantedBy = by, GrantedAt = DateTime.Now })
                              .ToList();

            db.RolePermissions.RemoveRange(toRemove);
            db.RolePermissions.AddRange(toAdd);
            await db.SaveChangesAsync();
            return toRemove.Count + toAdd.Count;
        }

        /// <summary>Applies grant / deny / inherit per permission id for a user. Returns the number of changes.</summary>
        public static async Task<int> SaveUserOverridesAsync(ApplicationDbContext db, int userId, IDictionary<int, string> overrides, string? by)
        {
            var valid = (await db.AppPermissions.Where(p => p.IsActive).Select(p => p.Id).ToListAsync()).ToHashSet();
            var current = await db.UserPermissions.Where(up => up.UserId == userId).ToDictionaryAsync(up => up.PermissionId);
            var changes = 0;

            foreach (var (permissionId, state) in overrides)
            {
                if (!valid.Contains(permissionId)) continue;
                current.TryGetValue(permissionId, out var row);

                switch (state)
                {
                    case PermissionOverride.Grant:
                    case PermissionOverride.Deny:
                        var granted = state == PermissionOverride.Grant;
                        if (row == null)
                        {
                            db.UserPermissions.Add(new UserPermission { UserId = userId, PermissionId = permissionId, IsGranted = granted, GrantedBy = by, GrantedAt = DateTime.Now });
                            changes++;
                        }
                        else if (row.IsGranted != granted)
                        {
                            row.IsGranted = granted;
                            row.GrantedBy = by;
                            row.GrantedAt = DateTime.Now;
                            changes++;
                        }
                        break;

                    default: // inherit → remove the override
                        if (row != null)
                        {
                            db.UserPermissions.Remove(row);
                            changes++;
                        }
                        break;
                }
            }

            await db.SaveChangesAsync();
            return changes;
        }

        // ─────────────────────────────── helpers ───────────────────────────────

        private static async Task<List<AppPermission>> ActivePermissionsAsync(ApplicationDbContext db)
        {
            // Tables created after the app started → fill them now instead of showing an empty grid.
            if (!await db.AppPermissions.AnyAsync())
                await PermissionSeeder.SyncAsync(db, NullLogger.Instance);

            return await db.AppPermissions.AsNoTracking()
                .Where(p => p.IsActive)
                .OrderBy(p => p.ModuleOrder).ThenBy(p => p.SortOrder)
                .ToListAsync();
        }

        private static PermissionMatrixViewModel Build(List<AppPermission> permissions, bool isAdmin,
                                                       Action<AppPermission, PermissionItemViewModel> fill)
        {
            var model = new PermissionMatrixViewModel { IsAdmin = isAdmin };
            foreach (var group in permissions.GroupBy(p => p.Module))
            {
                var first = group.First();
                var module = new PermissionModuleViewModel
                {
                    Module = group.Key,
                    DisplayName = first.ModuleDisplayName,
                    ArabicName = first.ModuleArabicName,
                    Icon = first.ModuleIcon ?? "bi-grid"
                };
                foreach (var p in group)
                {
                    var item = new PermissionItemViewModel
                    {
                        Id = p.Id,
                        Key = p.Key,
                        Action = p.Action,
                        DisplayName = p.DisplayName,
                        ArabicName = p.ArabicName
                    };
                    fill(p, item);
                    module.Items.Add(item);
                }
                model.Modules.Add(module);
            }
            return model;
        }
    }
}
