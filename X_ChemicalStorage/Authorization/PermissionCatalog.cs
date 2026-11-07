using System.Reflection;

namespace X_ChemicalStorage.Authorization
{
    /// <summary>Marks a nested class of <c>Constants.Permissions</c> as a permission module.</summary>
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class PermissionModuleAttribute : Attribute
    {
        public PermissionModuleAttribute(string displayName, string arabicName, string icon = "bi-grid", int order = 0)
        {
            DisplayName = displayName;
            ArabicName = arabicName;
            Icon = icon;
            Order = order;
        }
        public string DisplayName { get; }
        public string ArabicName { get; }
        public string Icon { get; }
        public int Order { get; }
    }

    /// <summary>Describes one permission constant (English + Arabic display names).</summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class PermissionAttribute : Attribute
    {
        public PermissionAttribute(string displayName, string arabicName)
        {
            DisplayName = displayName;
            ArabicName = arabicName;
        }
        public string DisplayName { get; }
        public string ArabicName { get; }
    }

    public sealed record PermissionDefinition(
        string Key, string Module, string Action,
        string DisplayName, string ArabicName,
        string ModuleDisplayName, string ModuleArabicName, string ModuleIcon,
        int ModuleOrder, int SortOrder);

    /// <summary>
    /// Reads every permission declared in <see cref="Constants.Permissions"/> once (reflection, cached).
    /// This is what gets synced to the ERP_Permissions table.
    /// </summary>
    public static class PermissionCatalog
    {
        private static readonly Lazy<IReadOnlyList<PermissionDefinition>> _all = new(Build);
        private static readonly Lazy<HashSet<string>> _keys =
            new(() => new HashSet<string>(_all.Value.Select(p => p.Key), StringComparer.OrdinalIgnoreCase));

        public static IReadOnlyList<PermissionDefinition> All => _all.Value;

        public static bool IsKnown(string key) => _keys.Value.Contains(key);

        private static IReadOnlyList<PermissionDefinition> Build()
        {
            var list = new List<PermissionDefinition>();
            var moduleTypes = typeof(Constants.Permissions)
                .GetNestedTypes(BindingFlags.Public | BindingFlags.Static);

            foreach (var type in moduleTypes)
            {
                var moduleAttr = type.GetCustomAttribute<PermissionModuleAttribute>();
                var moduleName = type.Name;
                var order = 0;

                foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Static)
                                          .Where(f => f.IsLiteral && f.FieldType == typeof(string)))
                {
                    var key = (string)field.GetRawConstantValue()!;
                    var attr = field.GetCustomAttribute<PermissionAttribute>();
                    var parts = key.Split('.', 2);

                    list.Add(new PermissionDefinition(
                        Key: key,
                        Module: parts[0],
                        Action: parts.Length > 1 ? parts[1] : field.Name,
                        DisplayName: attr?.DisplayName ?? field.Name,
                        ArabicName: attr?.ArabicName ?? attr?.DisplayName ?? field.Name,
                        ModuleDisplayName: moduleAttr?.DisplayName ?? moduleName,
                        ModuleArabicName: moduleAttr?.ArabicName ?? moduleName,
                        ModuleIcon: moduleAttr?.Icon ?? "bi-grid",
                        ModuleOrder: moduleAttr?.Order ?? 0,
                        SortOrder: ++order));
                }
            }

            var dup = list.GroupBy(p => p.Key, StringComparer.OrdinalIgnoreCase).FirstOrDefault(g => g.Count() > 1);
            if (dup != null)
                throw new InvalidOperationException($"Duplicate permission key '{dup.Key}' in Constants.Permissions.");

            return list.OrderBy(p => p.ModuleOrder).ThenBy(p => p.SortOrder).ToList();
        }
    }
}
