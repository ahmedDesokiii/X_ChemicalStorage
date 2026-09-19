// Enums/FreezerType.cs
namespace X_ChemicalStorage.BioBank.Enums
{
    public enum FreezerType
    {
        Minus80 = 1,   // Has Shelves → Racks → Drawers → Boxes
        Minus150 = 2   // No Shelves / No Drawers → Racks contain Boxes directly
    }

    public enum FreezerStatus
    {
        Online = 1,
        Offline = 2,
        Maintenance = 3,
        Alarm = 4
    }

    public enum TubeStatus
    {
        Available = 0,
        Occupied = 1,
        Reserved = 2,
        Empty = 3
    }
}
