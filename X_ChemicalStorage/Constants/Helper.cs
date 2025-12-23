namespace X_ChemicalStorage.Constants
{
    public class Helper
    {
        public const string Success = "success";
        public const string Error = "error";
        public const string Warning = "warning";

        public const string MsgType = "msgType";
        public const string Title = "title";
        public const string Msg = "msg";

        public const string AddRequestAsk = "New Request Ask";
        public const string Save = "Add New";
        public const string Update = "Update";
        public const string Delete = "Delete";
        public enum eCurrentState
        {
            Active = 2, // نشط
            Suspended = 1, // معلق
            Delete = 0 // غير نشط
        }
    }
}
