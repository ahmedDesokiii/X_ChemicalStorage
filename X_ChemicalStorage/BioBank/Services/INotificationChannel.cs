namespace X_ChemicalStorage.BioBank.Services
{
    public interface INotificationChannel
    {
        string Name { get; }          // SMS | Email | Telegram
        bool IsEnabled { get; }
        /// لازم ترمي Exception لو الإرسال فشل (عشان يتعاد).
        Task SendAsync(string recipient, string subject, string body, CancellationToken ct);
    }
}