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
        Empty = 3,
        CheckedOut = 4   // NEW: sample physically out for testing — slot stays LOCKED, not available
    }

    // Samples
    public enum SampleLifecycleStatus
    {
        Draft = 0,
        Reserved = 1,
        Stored = 2,
        Withdrawn = 3,
        Transferred = 4,
        Disposed = 5,
        Quarantine = 6,
        CheckedOut = 7   // NEW: sample is out of the freezer, undergoing testing
    }

    public enum MovementType
    {
        Register = 0,
        Reserve = 1,
        CancelReserve = 2,
        Store = 3,
        Withdraw = 4,
        Transfer = 5,
        Dispose = 6,
        Update = 7,
        CheckOut = 8,    // NEW: sample taken out for an experiment/test
        CheckIn = 9      // NEW: sample (or remainder) returned to storage
    }

    // NEW: blood type of the sample
    public enum BloodType
    {
        Unknown = 0,
        APos = 1,
        ANeg = 2,
        BPos = 3,
        BNeg = 4,
        ABPos = 5,
        ABNeg = 6,
        OPos = 7,
        ONeg = 8
    }
}