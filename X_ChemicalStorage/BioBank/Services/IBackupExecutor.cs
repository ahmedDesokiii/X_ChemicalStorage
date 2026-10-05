namespace X_ChemicalStorage.BioBank.Services
{
    /// الجزء اللي بيكلّم SQL Server فعلاً (معزول عشان نقدر نختبر باقي المنطق).
    public interface IBackupExecutor
    {
        string DatabaseName { get; }
        string DataSource { get; }
        /// عمل الفولدر عن طريق SQL Server نفسه (لو التطبيق مش على نفس الجهاز).
        Task EnsureDirectoryAsync(string directory, CancellationToken ct);
        Task BackupAsync(string filePath, bool compress, int timeoutSeconds, CancellationToken ct);
        Task VerifyAsync(string filePath, int timeoutSeconds, CancellationToken ct);
    }
}