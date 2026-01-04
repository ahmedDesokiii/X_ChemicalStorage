using ERPWeb_v02.Constants;

namespace X_ChemicalStorage.Constants
{
    public class Permissions
    {
        public static List<string> GeneratePermissionsList(string module)
        {
            var permissionsList = GenerateAllPermissions();
            return permissionsList;
        }
        public static List<string> Generate_Suppliers_PermissionsList(string module)
        {
            return new List<string>()
            {
                 $"Permissions.{module}.Suppliers",
                 $"Permissions.{module}.View Suppliers List",
                 $"Permissions.{module}.Create New Supplier",
                 $"Permissions.{module}.Details Supplier",
                 $"Permissions.{module}.Edit Supplier ",
                 $"Permissions.{module}.Delete Supplier",
            };
        }
        public static List<string> Generate_Categories_PermissionsList(string module)
        {
            return new List<string>()
            {
                 $"Permissions.{module}.Categories",
                 $"Permissions.{module}.View Categories List",
                 $"Permissions.{module}.Create New Category",
                 $"Permissions.{module}.Details Category",
                 $"Permissions.{module}.Edit Category ",
                 $"Permissions.{module}.Delete Category",
            };
        }
        public static List<string> Generate_Locations_PermissionsList(string module)
        {
            return new List<string>()
            {
                 $"Permissions.{module}.Locations",
                 $"Permissions.{module}.View Locations List",
                 $"Permissions.{module}.Create New Location",
                 $"Permissions.{module}.Details Location",
                 $"Permissions.{module}.Edit Location ",
                 $"Permissions.{module}.Delete Location",
            };
        }
        public static List<string> Generate_Manufacuters_PermissionsList(string module)
        {
            return new List<string>()
            {
                 $"Permissions.{module}.Manufacuters",
                 $"Permissions.{module}.View Manufacuters List",
                 $"Permissions.{module}.Create New Manufacuter",
                 $"Permissions.{module}.Details Manufacuter",
                 $"Permissions.{module}.Edit Manufacuter ",
                 $"Permissions.{module}.Delete Manufacuter",
            };
        }
        public static List<string> Generate_Lots_PermissionsList(string module)
        {
            return new List<string>()
            {
                 $"Permissions.{module}.Lots",
                 $"Permissions.{module}.View Lots List",
                 $"Permissions.{module}.Create New Lot",
                 $"Permissions.{module}.Details Lot",
                 $"Permissions.{module}.Edit Lot ",
                 $"Permissions.{module}.Delete Lot",
            };
        }

        public static List<string> GenerateAllPermissions()
        {
            var allSuppliersPermissions = new List<string>();
            var allCategoriesPermissions = new List<string>();
            var allLocationsPermissions = new List<string>();
            var allManufacutersPermissions = new List<string>();
            var allLotsPermissions = new List<string>();
            var allPermissions = new List<string>();

                allSuppliersPermissions.AddRange(Generate_Suppliers_PermissionsList(Modules.Suppliers.ToString()));
                allCategoriesPermissions.AddRange(Generate_Categories_PermissionsList(Modules.Categories.ToString()));
                allLocationsPermissions.AddRange(Generate_Locations_PermissionsList(Modules.Locations.ToString()));
                allManufacutersPermissions.AddRange(Generate_Manufacuters_PermissionsList(Modules.Manufacuters.ToString()));
                allLotsPermissions.AddRange(Generate_Lots_PermissionsList(Modules.Lots.ToString()));
                allPermissions = allSuppliersPermissions.Concat(allCategoriesPermissions).Concat(allManufacutersPermissions).Concat(allLocationsPermissions).Concat(allLotsPermissions).ToList();
        
            return allPermissions;
        }

        #region Suppliers Modules
        public static class Suppliers
        {
            public const string View = "Permissions.Suppliers.Suppliers";
            public const string View_Suppliers = "Permissions.Suppliers.View Suppliers List";
            public const string Create_Suppliers = "Permissions.Suppliers.Create New Supplier";
            public const string Details_Suppliers = "Permissions.Suppliers.Details Supplier";
            public const string Edit_Suppliers = "Permissions.Suppliers.Edit Supplier ";
            public const string Delete_Suppliers = "Permissions.Suppliers.Delete Supplier";
        }
        #endregion
        #region Categories Modules
        public static class Categories
        {
            public const string View = "Permissions.Categories.Categories";
            public const string View_Categories = "Permissions.Categories.View Categories List";
            public const string Create_Categories = "Permissions.Categories.Create New Category";
            public const string Details_Categories = "Permissions.Categories.Details Category";
            public const string Edit_Categories = "Permissions.Categories.Edit Category ";
            public const string Delete_Categories = "Permissions.Categories.Delete Category";
        }
        #endregion
        #region Locations Modules
        public static class Locations
        {
            public const string View = "Permissions.Locations.Locations";
            public const string View_Locations = "Permissions.Locations.View Locations List";
            public const string Create_Locations = "Permissions.Locations.Create New Location";
            public const string Details_Locations = "Permissions.Locations.Details Location";
            public const string Edit_Locations = "Permissions.Locations.Edit Location ";
            public const string Delete_Locations = "Permissions.Locations.Delete Location";
        }
        #endregion
        #region Manufacuters Modules
        public static class Manufacuters
        {
            public const string View = "Permissions.Manufacuters.Manufacuters";
            public const string View_Manufacuters = "Permissions.Manufacuters.View Manufacuters List";
            public const string Create_Manufacuters = "Permissions.Manufacuters.Create New Manufacuter";
            public const string Details_Manufacuters = "Permissions.Manufacuters.Details Manufacuter";
            public const string Edit_Manufacuters = "Permissions.Manufacuters.Edit Manufacuter ";
            public const string Delete_Manufacuters = "Permissions.Manufacuters.Delete Manufacuter";
        }
        #endregion
        #region Lots Modules
        public static class Lots
        {
            public const string View = "Permissions.Lots.Lots";
            public const string View_Lots = "Permissions.Lots.View Lots List";
            public const string Create_Lots = "Permissions.Lots.Create New Lot";
            public const string Details_Lots = "Permissions.Lots.Details Lot";
            public const string Edit_Lots = "Permissions.Lots.Edit Lot ";
            public const string Delete_Lots = "Permissions.Lots.Delete Lot";
        }

        #endregion
    }
}
