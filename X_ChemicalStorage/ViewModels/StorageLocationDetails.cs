namespace X_ChemicalStorage.ViewModels
{
    public class StorageLocationDetails
    {
        public string RoomName { get; set; } = string.Empty;
        public string StorageUnitName { get; set; } = string.Empty;
        public string StorageUnitTypeName { get; set; } = string.Empty;
        public bool IsHazardous { get; set; }
        public string ShelfName { get; set; } = string.Empty;
        public string BoxName { get; set; } = string.Empty;
        public int RowIndex { get; set; }
        public int ColumnIndex { get; set; }
    }
}