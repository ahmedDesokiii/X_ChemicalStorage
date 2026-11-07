namespace X_ChemicalStorage.Authorization
{
    /// <summary>
    /// Keeps ERP_Permissions in sync with <see cref="Constants.Permissions"/> and migrates the
    /// old "Permission" role claims (ERP_RoleClaims) into ERP_RolePermissions. Safe to run on every startup.
    /// </summary>
    public static class PermissionSeeder
    {
        private const string LegacyClaimType = "Permission";

        public static async Task SyncAsync(ApplicationDbContext db, ILogger logger)
        {
            // 1) Upsert catalog rows
            var existing = await db.AppPermissions.ToListAsync();
            var byKey = existing.ToDictionary(p => p.Key, StringComparer.OrdinalIgnoreCase);
            var added = 0;

            foreach (var def in PermissionCatalog.All)
            {
                if (!byKey.TryGetValue(def.Key, out var row))
                {
                    row = new AppPermission { Key = def.Key };
                    db.AppPermissions.Add(row);
                    byKey[def.Key] = row;
                    added++;
                }
                row.Module = def.Module;
                row.Action = def.Action;
                row.DisplayName = def.DisplayName;
                row.ArabicName = def.ArabicName;
                row.ModuleDisplayName = def.ModuleDisplayName;
                row.ModuleArabicName = def.ModuleArabicName;
                row.ModuleIcon = def.ModuleIcon;
                row.ModuleOrder = def.ModuleOrder;
                row.SortOrder = def.SortOrder;
                row.IsActive = true;
            }

            // Keys removed from code are deactivated, not deleted (assignments come back if re-added).
            foreach (var row in existing.Where(r => !PermissionCatalog.IsKnown(r.Key)))
                row.IsActive = false;

            await db.SaveChangesAsync();
            if (added > 0) logger.LogInformation("Permissions: {Count} new permission(s) registered.", added);

            // 2) One-time migration of the old claim-based permissions
            var legacyClaims = await db.RoleClaims.Where(c => c.ClaimType == LegacyClaimType).ToListAsync();
            if (legacyClaims.Count == 0) return;

            var assigned = (await db.RolePermissions.Select(rp => new { rp.RoleId, rp.PermissionId }).ToListAsync())
                .Select(x => (x.RoleId, x.PermissionId)).ToHashSet();
            var migrated = 0;

            foreach (var claim in legacyClaims)
            {
                foreach (var key in MapLegacy(claim.ClaimValue))
                {
                    if (!byKey.TryGetValue(key, out var perm)) continue;
                    if (assigned.Add((claim.RoleId, perm.Id)))
                    {
                        db.RolePermissions.Add(new RolePermission { RoleId = claim.RoleId, PermissionId = perm.Id, GrantedBy = "migration" });
                        migrated++;
                    }
                }
            }

            db.RoleClaims.RemoveRange(legacyClaims);
            await db.SaveChangesAsync();
            logger.LogInformation("Permissions: migrated {Claims} legacy role claim(s) into {Rows} role permission(s).", legacyClaims.Count, migrated);
        }

        /// <summary>"Permissions.Items.Create New Item" → "Items.Create", etc.</summary>
        internal static IEnumerable<string> MapLegacy(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) yield break;
            var parts = value.Trim().Split('.', 3);
            if (parts.Length < 3 || parts[0] != "Permissions") yield break;

            var module = parts[1] == "Manufacuters" ? "Manufacturers" : parts[1];
            var name = parts[2].Trim();

            string? action =
                name.Contains("Expiery", StringComparison.OrdinalIgnoreCase) ? "ViewExpiry" :
                name.Equals(parts[1], StringComparison.OrdinalIgnoreCase) || name.StartsWith("View", StringComparison.OrdinalIgnoreCase) ? "View" :
                name.StartsWith("Create", StringComparison.OrdinalIgnoreCase) ? "Create" :
                name.StartsWith("Details", StringComparison.OrdinalIgnoreCase) ? "Details" :
                name.StartsWith("Edit", StringComparison.OrdinalIgnoreCase) ? "Edit" :
                name.StartsWith("Delete", StringComparison.OrdinalIgnoreCase) ? "Delete" :
                name.StartsWith("Print", StringComparison.OrdinalIgnoreCase) ? "PrintBarcode" :
                name.StartsWith("Exchange", StringComparison.OrdinalIgnoreCase) ? "Exchange" :
                null;

            if (action == null) yield break;
            var key = $"{module}.{action}";
            if (PermissionCatalog.IsKnown(key)) yield return key;
        }
    }
}
