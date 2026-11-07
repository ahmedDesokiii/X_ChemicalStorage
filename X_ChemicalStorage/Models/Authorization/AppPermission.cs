namespace X_ChemicalStorage.Models.Authorization
{
    /// <summary>
    /// One permission (e.g. "Items.Create"). Rows are synced from <c>Constants.Permissions</c> on startup;
    /// assignments live in <see cref="RolePermission"/> and <see cref="UserPermission"/>.
    /// </summary>
    public class AppPermission
    {
        public int Id { get; set; }

        /// <summary>Unique key used in code: "Module.Action".</summary>
        [Required, MaxLength(150)]
        public string Key { get; set; } = string.Empty;

        [Required, MaxLength(100)]
        public string Module { get; set; } = string.Empty;

        [Required, MaxLength(100)]
        public string Action { get; set; } = string.Empty;

        [MaxLength(200)]
        public string DisplayName { get; set; } = string.Empty;

        [MaxLength(200)]
        public string ArabicName { get; set; } = string.Empty;

        [MaxLength(200)]
        public string ModuleDisplayName { get; set; } = string.Empty;

        [MaxLength(200)]
        public string ModuleArabicName { get; set; } = string.Empty;

        [MaxLength(60)]
        public string? ModuleIcon { get; set; }

        public int ModuleOrder { get; set; }
        public int SortOrder { get; set; }

        /// <summary>False when the key was removed from code. Kept so re-adding it restores old assignments.</summary>
        public bool IsActive { get; set; } = true;

        public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
        public ICollection<UserPermission> UserPermissions { get; set; } = new List<UserPermission>();
    }

    /// <summary>Role ⇄ Permission (many-to-many).</summary>
    public class RolePermission
    {
        public int RoleId { get; set; }
        public ApplicationRole? Role { get; set; }

        public int PermissionId { get; set; }
        public AppPermission? Permission { get; set; }

        public DateTime GrantedAt { get; set; } = DateTime.Now;

        [MaxLength(256)]
        public string? GrantedBy { get; set; }
    }

    /// <summary>
    /// Per-user override on top of the user's roles:
    /// IsGranted = true  → the user gets the permission even if no role has it;
    /// IsGranted = false → the user is denied even if a role grants it.
    /// </summary>
    public class UserPermission
    {
        public int UserId { get; set; }
        public ApplicationUser? User { get; set; }

        public int PermissionId { get; set; }
        public AppPermission? Permission { get; set; }

        public bool IsGranted { get; set; }

        public DateTime GrantedAt { get; set; } = DateTime.Now;

        [MaxLength(256)]
        public string? GrantedBy { get; set; }
    }
}
