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
       
       
        public static List<string> GenerateAllPermissions()
        {
            var allSuppliersPermissions = new List<string>();
            var allPermissions = new List<string>();

                allSuppliersPermissions.AddRange(Generate_Suppliers_PermissionsList(Modules.Suppliers.ToString()));
                allPermissions = allSuppliersPermissions;
        
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
        #region GL Modules
        public static class GL
        {
            public const string View = "Permissions.GL.الأستاذ العام";
            public const string View_Settings = "Permissions.GL.اعدادات الأستاذ العام";
            public const string View_Definitions = "Permissions.GL.اعدادات الأستاذ العام";
            public const string View_Transactions = "Permissions.GL.اعدادات الأستاذ العام";
            public const string View_Reports = "Permissions.GL.اعدادات الأستاذ العام";

            public const string View_Accounts = "Permissions.GL.عرض قائمة دليل الحسابات";
            public const string Create_Accounts = "Permissions.GL.إضافة حساب لدليل الحسابات";
            public const string Edit_Accounts = "Permissions.GL.تعديل حساب من دليل الحسابات";

            public const string View_CostCenters = "Permissions.GL.عرض قائمة مراكز التكلفة";
            public const string Create_CostCenters = "Permissions.GL.إضافة حساب مراكز التكلفة";
            public const string Edit_CostCenters = "Permissions.GL.تعديل حساب من مراكز التكلفة";

            public const string View_EntryTypes = "Permissions.GL.عرض قائمة أنواع القيود";
            public const string Create_EntryTypes = "Permissions.GL.إضافة نوع قيد جديد";
            public const string Edit_EntryTypes = "Permissions.GL.تعديل نوع قيد";

            public const string View_DailyEntries = "Permissions.GL.عرض قائمة القيود اليومية";
            public const string Create_DailyEntries = "Permissions.GL.إضافة قيد جديد";
            public const string Edit_DailyEntries = "Permissions.GL.تعديل بيانات قيد";

            public const string View_ClosedPeriods = "Permissions.GL.عرض قائمة الإقفالات المالية";
            public const string Create_ClosedPeriods = "Permissions.GL.إضافة إقفال فترة مالية";
        }
        #endregion
        #region AR Modules
        public static class AR
        {
            public const string View = "Permissions.AR.المبيعات";
            public const string View_Invoice = "Permissions.AR.عرض قائمة الفواتير اليومية";
            public const string Create_Invoice = "Permissions.AR.إنشاء فاتورة بيع جديدة";
            public const string Edit_Invoice = "Permissions.AR.تعديل فاتورة بيع";
            public const string Delete_Invoice = "Permissions.AR.حذف فاتورة بيع";
        }
        #endregion

    }
}
