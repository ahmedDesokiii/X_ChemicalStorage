namespace X_ChemicalStorage.BioBank.Services
{
    /// One backup / restore at a time across the whole app (full database, Bio Bank data, restores).
    public static class BackupLocks
    {
        public static readonly SemaphoreSlim Gate = new(1, 1);

        /// Auto | Manual | PreRestore (safety copy taken right before a restore) | Uploaded
        public static string NormalizeType(string? type) => type?.Trim().ToLowerInvariant() switch
        {
            "auto" => "Auto",
            "prerestore" => "PreRestore",
            "uploaded" => "Uploaded",
            _ => "Manual"
        };
    }
}
