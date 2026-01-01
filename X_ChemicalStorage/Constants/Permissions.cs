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
                $"Permissions.{module}.الموردين",
                $"Permissions.{module}.عرض قائمة الموردين",
                $"Permissions.{module}.إنشاء مورد جديد",
                $"Permissions.{module}.عرض بيانات مورد",
                $"Permissions.{module}.تعديل بيانات مورد ",
                $"Permissions.{module}.حذف بيانات مورد",
            };
        }
        public static List<string> Generate_Categories_PermissionsList(string module)
        {
            return new List<string>()
            {
                $"Permissions.{module}.فئات الأصناف",
                $"Permissions.{module}.عرض قائمة الفئات",
                $"Permissions.{module}.إنشاء فئة جديد",
                $"Permissions.{module}.عرض بيانات فئة",
                $"Permissions.{module}.تعديل بيانات فئة ",
                $"Permissions.{module}.حذف بيانات فئة",
            };
        }
        public static List<string> Generate_Locations_PermissionsList(string module)
        {
            return new List<string>()
            {
                $"Permissions.{module}.أماكن التخزين",
                $"Permissions.{module}.عرض قائمة أماكن التخزين",
                $"Permissions.{module}.إنشاء مكان تخزين جديد",
                $"Permissions.{module}.عرض بيانات مكان التخزين",
                $"Permissions.{module}.تعديل بيانات اماكن التخزين ",
                $"Permissions.{module}.حذف بيانات اماكن التخزين",
            };
        }
        public static List<string> Generate_ManufacuterCompanies_PermissionsList(string module)
        {
            return new List<string>()
            {
                $"Permissions.{module}.الشركة المصنعة",
                $"Permissions.{module}.عرض قائمة الشركات المصنعة",
                $"Permissions.{module}.إنشاء شركة جديدة",
                $"Permissions.{module}.عرض بيانات شركة",
                $"Permissions.{module}.تعديل بيانات شركة ",
                $"Permissions.{module}.حذف بيانات شركة",
            };
        }

        public static List<string> GenerateAllPermissions()
        {
            var allSuppliersPermissions = new List<string>();
            var allCategoriesPermissions = new List<string>();
            var allLocationsPermissions = new List<string>();
            var allManufacuterCompaniesPermissions = new List<string>();
            var allPermissions = new List<string>();

                allSuppliersPermissions.AddRange(Generate_Suppliers_PermissionsList(Modules.Suppliers.ToString()));
                allCategoriesPermissions.AddRange(Generate_Categories_PermissionsList(Modules.Categories.ToString()));
                allLocationsPermissions.AddRange(Generate_Locations_PermissionsList(Modules.Locations.ToString()));
                allManufacuterCompaniesPermissions.AddRange(Generate_ManufacuterCompanies_PermissionsList(Modules.Manufacuters.ToString()));
                allPermissions = allSuppliersPermissions.Concat(allCategoriesPermissions).Concat(allManufacuterCompaniesPermissions).Concat(allLocationsPermissions).ToList();
        
            return allPermissions;
        }

        #region Suppliers Modules
        public static class Suppliers
        {
            public const string View = "Permissions.Suppliers.الموردين";
            public const string View_Suppliers = "Permissions.Suppliers.عرض قائمة الموردين";
            public const string Create_Suppliers = "Permissions.Suppliers.إنشاء مورد جديد";
            public const string Details_Suppliers = "Permissions.Suppliers.عرض بيانات مورد";
            public const string Edit_Suppliers = "Permissions.Suppliers.تعديل بيانات مورد ";
            public const string Delete_Suppliers = "Permissions.Suppliers.حذف بيانات مورد";
        }
        #endregion
        #region Categories Modules
        public static class Categories
        {
            public const string View = "Permissions.Categories.فئات الأصناف";
            public const string View_Categories = "Permissions.Categories.عرض قائمة الفئات";
            public const string Create_Categories = "Permissions.Categories.إنشاء فئة جديد";
            public const string Details_Categories = "Permissions.Categories.عرض بيانات فئة";
            public const string Edit_Categories = "Permissions.Categories.تعديل بيانات فئة ";
            public const string Delete_Categories = "Permissions.Categories.حذف بيانات فئة";
        }
        #endregion
        #region Locations Modules
        public static class Locations
        {
            public const string View = "Permissions.Locations.أماكن التخزين";
            public const string View_Locations = "Permissions.Locations.عرض قائمة أماكن التخزين";
            public const string Create_Locations = "Permissions.Locations.إنشاء مكان تخزين جديد";
            public const string Details_Locations = "Permissions.Locations.عرض بيانات مكان تخزين";
            public const string Edit_Locations = "Permissions.Locations.تعديل بيانات مكان تخزين ";
            public const string Delete_Locations = "Permissions.Locations.حذف بيانات مكان تخزين";
        }
        #endregion
        #region ManufacuterCompanies Modules
        public static class ManufacuterCompanies
        {
            public const string View = "Permissions.ManufacuterCompanies.الشركة المصنعة";
            public const string View_ManufacuterCompanies = "Permissions.ManufacuterCompanies.عرض قائمة الشركات المصنعة";
            public const string Create_ManufacuterCompanies = "Permissions.ManufacuterCompanies.إنشاء شركة جديدة";
            public const string Details_ManufacuterCompanies = "Permissions.ManufacuterCompanies.عرض بيانات شركة";
            public const string Edit_ManufacuterCompanies = "Permissions.ManufacuterCompanies.تعديل بيانات شركة ";
            public const string Delete_ManufacuterCompanies = "Permissions.ManufacuterCompanies.حذف بيانات شركة";
        }
        #endregion
    }
}
