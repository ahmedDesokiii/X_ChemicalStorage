namespace X_ChemicalStorage.ViewModels
{
    /// <summary>Data for the permission grid (used for a role, or for a user's overrides).</summary>
    public class PermissionMatrixViewModel
    {
        public int SubjectId { get; set; }
        public string SubjectName { get; set; } = string.Empty;
        public string? Details { get; set; }

        /// <summary>Admin role / admin user: everything is granted and the grid is read-only.</summary>
        public bool IsAdmin { get; set; }

        /// <summary>Roles of the user (user mode only).</summary>
        public List<string> SubjectRoles { get; set; } = new();

        public List<PermissionModuleViewModel> Modules { get; set; } = new();

        public int TotalCount => Modules.Sum(m => m.Items.Count);
        public int GrantedCount => Modules.Sum(m => m.Items.Count(i => i.IsEffective));
    }

    public class PermissionModuleViewModel
    {
        public string Module { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string ArabicName { get; set; } = string.Empty;
        public string Icon { get; set; } = "bi-grid";
        public List<PermissionItemViewModel> Items { get; set; } = new();
    }

    public class PermissionItemViewModel
    {
        public int Id { get; set; }
        public string Key { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string ArabicName { get; set; } = string.Empty;

        /// <summary>Role mode: assigned to the role.</summary>
        public bool IsSelected { get; set; }

        /// <summary>User mode: granted by one of the user's roles.</summary>
        public bool FromRole { get; set; }

        /// <summary>User mode: "inherit" | "grant" | "deny".</summary>
        public string Override { get; set; } = PermissionOverride.Inherit;

        /// <summary>Final result after roles + overrides.</summary>
        public bool IsEffective { get; set; }
    }

    public static class PermissionOverride
    {
        public const string Inherit = "inherit";
        public const string Grant = "grant";
        public const string Deny = "deny";
    }
}
