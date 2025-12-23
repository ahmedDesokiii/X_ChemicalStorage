namespace X_ChemicalStorage.Constants
{
    public class Permissions
    {
        public static List<string> GeneratePermissionsList(string module)
        {
            var permissionsList = GenerateAllPermissions();
            return permissionsList;
        }
        public static List<string> Generate_AR_PermissionsList(string module)
        {
            return new List<string>()
            {
                $"Permissions.{module}.المبيعات",
                $"Permissions.{module}.عرض قائمة الفواتير اليومية",
                $"Permissions.{module}.إنشاء فاتورة بيع جديدة",
                $"Permissions.{module}.تعديل فاتورة بيع",
                $"Permissions.{module}.حذف فاتورة بيع",
            };
        }
        public static List<string> Generate_GL_PermissionsList(string module)
        {
            return new List<string>()
            {
                $"Permissions.{module}.الأستاذ العام",
                $"Permissions.{module}.إعدادات الأستاذ العام",
                $"Permissions.{module}.تعريفات أساسية",
                $"Permissions.{module}.عمليات الأستاذ العام",
                $"Permissions.{module}.استعلامات وتقارير",
                //Accounts
                $"Permissions.{module}.عرض قائمة دليل الحسابات",
                $"Permissions.{module}.إضافة حساب دليل الحسابات",
                $"Permissions.{module}.تعديل حساب من دليل الحسابات",
                //CostCenters
                $"Permissions.{module}.عرض قائمة مراكز التكلفة",
                $"Permissions.{module}.إضافة حساب مراكز التكلفة",
                $"Permissions.{module}.تعديل حساب من مراكز التكلفة",
                //EntryTypes
                $"Permissions.{module}.عرض قائمة أنواع القيود",
                $"Permissions.{module}.إضافة نوع قيد جديد",
                $"Permissions.{module}.تعديل نوع قيد",
                //DailyEntries
                $"Permissions.{module}.عرض قائمة القيود اليومية",
                $"Permissions.{module}.إضافة قيد جديد",
                $"Permissions.{module}.تعديل بيانات قيد",
                //ClosedPeriods
                $"Permissions.{module}.عرض قائمة الإقفالات ",
                $"Permissions.{module}. إضافة إقفال فترة مالية",
            };
        }
        public static List<string> Generate_HR_PermissionsList(string module)
        {
            return new List<string>()
            {
                $"Permissions.{module}.شئون الموظفين",
                $"Permissions.{module}.عرض قائمة بيانات الموظفين",
                $"Permissions.{module}.إضافة موظف جديد",
                $"Permissions.{module}.تعديل بيانات موظف",
                $"Permissions.{module}.عرض وإرسال المرتبات عبر الواتس آب "
            };
        }

        public static List<string> GenerateAllPermissions()
        {
            var allPermissions = new List<string>();
        
            return allPermissions;
        }

        #region HR Modules
        public static class HR
        {
            public const string View = "Permissions.HR.شئون الموظفين";
            public const string View_Employees = "Permissions.HR.عرض قائمة بيانات الموظفين";
            public const string Create_Employees = "Permissions.HR.إضافة موظف جديد";
            public const string Edit_Employees = "Permissions.HR.تعديل بيانات موظف";
            public const string View_SalaryWhatsApp = "Permissions.HR.عرض وإرسال المرتبات عبر الواتس آب";
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
