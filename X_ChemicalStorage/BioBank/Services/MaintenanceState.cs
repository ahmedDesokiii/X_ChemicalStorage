namespace X_ChemicalStorage.BioBank.Services
{
    /// <summary>
    /// Set while a backup restore replaces data. Freezer messages are dropped (not queued) during that window —
    /// a restore takes seconds to a few minutes and the gateways send again every 5 minutes.
    /// </summary>
    public sealed class MaintenanceState
    {
        private readonly object _lock = new();
        public bool Active { get; private set; }
        public string? Reason { get; private set; }
        public DateTime? Since { get; private set; }

        public bool TryBegin(string reason)
        {
            lock (_lock)
            {
                if (Active) return false;
                Active = true; Reason = reason; Since = DateTime.UtcNow;
                return true;
            }
        }

        public void End()
        {
            lock (_lock) { Active = false; Reason = null; Since = null; }
        }
    }
}
