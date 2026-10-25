namespace X_ChemicalStorage.Models.TempSamples
{
    /// <summary>
    /// Which purpose a storage unit serves inside a room.
    /// Lots and temporary samples live in the SAME rooms but in SEPARATE units,
    /// so their grids never collide and each side has its own design.
    /// </summary>
    public enum StorageZone
    {
        Inventory = 0,          // Lots (existing behaviour — all current units default here)
        TemporaryHolding = 1    // Temporary custody samples
    }

    public enum TempSampleStatus
    {
        Received = 0,     // logged at reception, not yet placed in a slot
        Stored = 1,       // occupying a box slot
        CheckedOut = 2,   // temporarily out (e.g. to the lab), expected back
        Withdrawn = 3,    // final withdrawal — slot released
        Disposed = 4      // destroyed / discarded — slot released
    }

    public enum TransportCondition
    {
        Ambient = 0,          // 15–25 °C
        Refrigerated = 1,     // 2–8 °C
        Frozen = 2,           // -20 °C
        DeepFrozen = 3,       // -80 °C
        DryIce = 4,
        LiquidNitrogen = 5,   // -150 °C and below
        Other = 9
    }

    public enum CustodyEventType
    {
        Received = 0,
        Stored = 1,
        Relocated = 2,
        MetadataUpdated = 3,
        AttachmentAdded = 4,
        CheckedOut = 5,
        Returned = 6,
        PartialWithdrawal = 7,
        Withdrawn = 8,
        Disposed = 9,
        TemperatureExcursion = 10,   // reserved for the freezer IoT integration
        Note = 11,
        Placed = 12          // first placement of a sample received without a slot
    }

    public enum AttachmentKind
    {
        DeliveryNote = 0,
        RequestForm = 1,
        ChainOfCustodyForm = 2,
        TemperatureLog = 3,
        Photo = 4,
        Signature = 5,        // hand-over / disposal signature captured on screen
        DisposalCertificate = 6,
        Other = 9
    }
}
