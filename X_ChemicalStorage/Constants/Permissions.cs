using X_ChemicalStorage.Authorization;

namespace X_ChemicalStorage.Constants
{
    /// <summary>
    /// The single source of truth for every permission in the system.
    ///
    /// Each nested class is a module, each <c>const</c> is one permission key ("Module.Action").
    /// On startup <see cref="PermissionSeeder"/> reads these via reflection and syncs them into the
    /// ERP_Permissions table, so the UI and the role/user assignments always see the full list.
    ///
    /// To add a new permission: add a const with a [Permission] attribute here, then use it in
    /// [HasPermission(Permissions.Module.Action)] and in views (has-permission="..."). No migration needed.
    /// </summary>
    public static class Permissions
    {
        [PermissionModule("Dashboard", "لوحة التحكم", "bi-speedometer", 10)]
        public static class Dashboard
        {
            [Permission("View dashboard", "عرض لوحة التحكم")] public const string View = "Dashboard.View";
        }

        [PermissionModule("Items", "الأصناف", "bi-boxes", 20)]
        public static class Items
        {
            [Permission("View list", "عرض قائمة الأصناف")] public const string View = "Items.View";
            [Permission("Details", "عرض تفاصيل الصنف")] public const string Details = "Items.Details";
            [Permission("Create", "إضافة صنف")] public const string Create = "Items.Create";
            [Permission("Edit", "تعديل صنف")] public const string Edit = "Items.Edit";
            [Permission("Delete", "حذف صنف")] public const string Delete = "Items.Delete";
            [Permission("Print barcode", "طباعة باركود الصنف")] public const string PrintBarcode = "Items.PrintBarcode";
        }

        [PermissionModule("Lots", "التشغيلات (Lots)", "bi-archive-fill", 30)]
        public static class Lots
        {
            [Permission("View list", "عرض قائمة التشغيلات")] public const string View = "Lots.View";
            [Permission("View expiring lots", "عرض التشغيلات قاربت على الانتهاء")] public const string ViewExpiry = "Lots.ViewExpiry";
            [Permission("Details", "عرض تفاصيل التشغيلة")] public const string Details = "Lots.Details";
            [Permission("Create", "إضافة تشغيلة")] public const string Create = "Lots.Create";
            [Permission("Edit", "تعديل تشغيلة")] public const string Edit = "Lots.Edit";
            [Permission("Delete", "حذف تشغيلة")] public const string Delete = "Lots.Delete";
            [Permission("Exchange (issue)", "صرف من التشغيلة")] public const string Exchange = "Lots.Exchange";
            [Permission("Print barcode", "طباعة باركود التشغيلة")] public const string PrintBarcode = "Lots.PrintBarcode";
        }

        [PermissionModule("Rooms", "الغرف وأماكن التخزين", "bi-building", 40)]
        public static class Rooms
        {
            [Permission("View", "عرض الغرف وأماكن التخزين")] public const string View = "Rooms.View";
            [Permission("Create", "إضافة غرفة / وحدة / رف / صندوق")] public const string Create = "Rooms.Create";
            [Permission("Edit", "تعديل الغرف وأماكن التخزين")] public const string Edit = "Rooms.Edit";
            [Permission("Delete", "حذف الغرف وأماكن التخزين")] public const string Delete = "Rooms.Delete";
            [Permission("Storage generation wizard", "معالج إنشاء أماكن التخزين")] public const string Generate = "Rooms.Generate";
        }

        [PermissionModule("TempSamples", "العينات المؤقتة (العهدة)", "bi-hourglass-split", 50)]
        public static class TempSamples
        {
            [Permission("View register", "عرض سجل العينات")] public const string View = "TempSamples.View";
            [Permission("Details", "عرض ملف العينة")] public const string Details = "TempSamples.Details";
            [Permission("Receive", "استلام عينة جديدة")] public const string Receive = "TempSamples.Receive";
            [Permission("Edit", "تعديل بيانات العينة")] public const string Edit = "TempSamples.Edit";
            [Permission("Place in storage", "وضع العينة في مكان التخزين")] public const string Place = "TempSamples.Place";
            [Permission("Check out", "إخراج العينة (Check out)")] public const string CheckOut = "TempSamples.CheckOut";
            [Permission("Return", "إرجاع العينة")] public const string Return = "TempSamples.Return";
            [Permission("Withdraw", "سحب العينة")] public const string Withdraw = "TempSamples.Withdraw";
            [Permission("Dispose", "إعدام العينة")] public const string Dispose = "TempSamples.Dispose";
            [Permission("Add note", "إضافة ملاحظة")] public const string AddNote = "TempSamples.AddNote";
            [Permission("Attachments", "رفع وعرض المرفقات")] public const string Attachments = "TempSamples.Attachments";
            [Permission("Print label / report", "طباعة الملصق وتقرير العهدة")] public const string Print = "TempSamples.Print";
            [Permission("Verify custody chain", "التحقق من سلسلة العهدة")] public const string VerifyChain = "TempSamples.VerifyChain";
        }

        [PermissionModule("BioBank", "بنك العينات الحيوية", "bi-droplet", 60)]
        public static class BioBank
        {
            [Permission("View", "عرض بنك العينات")] public const string View = "BioBank.View";
            [Permission("Manage freezers", "إدارة الفريزرات (إضافة/حذف/إعادة بناء)")] public const string ManageFreezers = "BioBank.ManageFreezers";
            [Permission("Store samples", "تخزين العينات وشغل الأنابيب")] public const string Store = "BioBank.Store";
            [Permission("Reserve", "حجز أماكن / عينات")] public const string Reserve = "BioBank.Reserve";
            [Permission("Edit samples", "تعديل بيانات العينات")] public const string Edit = "BioBank.Edit";
            [Permission("Withdraw / clear", "سحب العينات وتفريغ الأنابيب")] public const string Withdraw = "BioBank.Withdraw";
            [Permission("Transfer", "نقل العينات")] public const string Transfer = "BioBank.Transfer";
            [Permission("Check out / in", "إخراج وإرجاع العينات")] public const string CheckInOut = "BioBank.CheckInOut";
            [Permission("Attachments", "مرفقات العينات")] public const string Attachments = "BioBank.Attachments";
            [Permission("Manage SOPs", "إدارة إجراءات التشغيل (SOP)")] public const string ManageSOPs = "BioBank.ManageSOPs";
        }

        [PermissionModule("IoT", "مراقبة الفريزرات (IoT)", "bi-thermometer-half", 70)]
        public static class IoT
        {
            [Permission("View monitoring", "عرض لوحة المراقبة والقراءات")] public const string View = "IoT.View";
            [Permission("Acknowledge alarms", "تأكيد الإنذارات")] public const string AcknowledgeAlarms = "IoT.AcknowledgeAlarms";
            [Permission("Freezer settings", "تعديل إعدادات الفريزر")] public const string ManageSettings = "IoT.ManageSettings";
            [Permission("Link devices", "ربط أجهزة IoT")] public const string ManageDevices = "IoT.ManageDevices";
            [Permission("Alarm contacts", "إدارة جهات اتصال الإنذار")] public const string ManageAlarmContacts = "IoT.ManageAlarmContacts";
            [Permission("Notifications log", "عرض سجل الإشعارات")] public const string ViewNotifications = "IoT.ViewNotifications";
        }

        [PermissionModule("Suppliers", "الموردين", "bi-truck", 80)]
        public static class Suppliers
        {
            [Permission("View list", "عرض قائمة الموردين")] public const string View = "Suppliers.View";
            [Permission("Details", "عرض تفاصيل المورد")] public const string Details = "Suppliers.Details";
            [Permission("Create", "إضافة مورد")] public const string Create = "Suppliers.Create";
            [Permission("Edit", "تعديل مورد")] public const string Edit = "Suppliers.Edit";
            [Permission("Delete", "حذف مورد")] public const string Delete = "Suppliers.Delete";
        }

        [PermissionModule("Categories", "التصنيفات", "bi-tags", 90)]
        public static class Categories
        {
            [Permission("View list", "عرض التصنيفات")] public const string View = "Categories.View";
            [Permission("Details", "عرض تفاصيل التصنيف")] public const string Details = "Categories.Details";
            [Permission("Create", "إضافة تصنيف / تصنيف فرعي")] public const string Create = "Categories.Create";
            [Permission("Edit", "تعديل تصنيف / تصنيف فرعي")] public const string Edit = "Categories.Edit";
            [Permission("Delete", "حذف تصنيف / تصنيف فرعي")] public const string Delete = "Categories.Delete";
        }

        [PermissionModule("Units", "الوحدات", "bi-rulers", 100)]
        public static class Units
        {
            [Permission("View list", "عرض الوحدات")] public const string View = "Units.View";
            [Permission("Details", "عرض تفاصيل الوحدة")] public const string Details = "Units.Details";
            [Permission("Create", "إضافة وحدة")] public const string Create = "Units.Create";
            [Permission("Edit", "تعديل وحدة")] public const string Edit = "Units.Edit";
            [Permission("Delete", "حذف وحدة")] public const string Delete = "Units.Delete";
        }

        [PermissionModule("Manufacturers", "الشركات المصنعة", "bi-buildings", 110)]
        public static class Manufacturers
        {
            [Permission("View list", "عرض الشركات المصنعة")] public const string View = "Manufacturers.View";
            [Permission("Details", "عرض تفاصيل الشركة")] public const string Details = "Manufacturers.Details";
            [Permission("Create", "إضافة شركة")] public const string Create = "Manufacturers.Create";
            [Permission("Edit", "تعديل شركة")] public const string Edit = "Manufacturers.Edit";
            [Permission("Delete", "حذف شركة")] public const string Delete = "Manufacturers.Delete";
        }

        [PermissionModule("Locations", "المواقع", "bi-geo-alt", 120)]
        public static class Locations
        {
            [Permission("View list", "عرض المواقع")] public const string View = "Locations.View";
            [Permission("Details", "عرض تفاصيل الموقع")] public const string Details = "Locations.Details";
            [Permission("Create", "إضافة موقع")] public const string Create = "Locations.Create";
            [Permission("Edit", "تعديل موقع")] public const string Edit = "Locations.Edit";
            [Permission("Delete", "حذف موقع")] public const string Delete = "Locations.Delete";
        }

        [PermissionModule("Users", "المستخدمين", "bi-people-fill", 130)]
        public static class Users
        {
            [Permission("View list", "عرض المستخدمين")] public const string View = "Users.View";
            [Permission("Create", "إضافة مستخدم")] public const string Create = "Users.Create";
            [Permission("Edit", "تعديل مستخدم")] public const string Edit = "Users.Edit";
            [Permission("Delete", "حذف مستخدم")] public const string Delete = "Users.Delete";
            [Permission("Assign roles", "تعيين أدوار المستخدم")] public const string ManageRoles = "Users.ManageRoles";
            [Permission("User-specific permissions", "صلاحيات خاصة بالمستخدم")] public const string ManagePermissions = "Users.ManagePermissions";
        }

        [PermissionModule("Roles", "الأدوار والصلاحيات", "bi-shield-lock", 140)]
        public static class Roles
        {
            [Permission("View list", "عرض الأدوار")] public const string View = "Roles.View";
            [Permission("Create", "إضافة دور")] public const string Create = "Roles.Create";
            [Permission("Edit", "تعديل دور")] public const string Edit = "Roles.Edit";
            [Permission("Delete", "حذف دور")] public const string Delete = "Roles.Delete";
            [Permission("Manage role permissions", "إدارة صلاحيات الدور")] public const string ManagePermissions = "Roles.ManagePermissions";
        }

        [PermissionModule("Backup", "النسخ الاحتياطي", "bi-cloud-arrow-up", 150)]
        public static class Backup
        {
            [Permission("View status", "عرض حالة النسخ الاحتياطي")] public const string View = "Backup.View";
            [Permission("Run backup", "تشغيل نسخة احتياطية")] public const string Run = "Backup.Run";
            [Permission("Upload and restore", "رفع واسترجاع نسخة احتياطية")] public const string Restore = "Backup.Restore";
        }
    }
}
