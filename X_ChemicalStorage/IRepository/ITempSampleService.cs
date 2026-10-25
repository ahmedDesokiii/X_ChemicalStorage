using X_ChemicalStorage.Models.TempSamples;
using X_ChemicalStorage.ViewModels.TempSamples;

namespace X_ChemicalStorage.IRepository
{
    public interface ITempSampleService
    {
        // ───────── Reception ─────────
        /// <summary>Creates the sample + Received event (+ Stored event if a slot was chosen) + attachments, in one transaction.</summary>
        Task<TempSample> ReceiveAsync(TempSampleReceiveViewModel vm, int userId, string userName, string? clientInfo);

        // ───────── Queries ─────────
        Task<List<TempSampleListRow>> GetListAsync(TempSampleStatus? status = null, string? search = null);
        Task<TempSampleDashboardStats> GetStatsAsync();
        Task<TempSampleDossierViewModel?> GetDossierAsync(int id);
        Task<(Stream Stream, string ContentType, string FileName)?> OpenAttachmentAsync(int attachmentId);
        /// <summary>Recomputes every hash in the sample's custody chain. Returns the first broken sequence, or null if intact.</summary>
        Task<int?> VerifyChainAsync(int tempSampleId);

        // ───────── Custody actions (each = one sealed event) ─────────
        Task UpdateMetadataAsync(TempSampleEditViewModel vm, CustodyActor actor);
        Task PlaceAsync(TempSampleActionInput input, CustodyActor actor);          // first placement or relocation
        Task CheckOutAsync(TempSampleActionInput input, CustodyActor actor);       // out for analysis, slot stays reserved
        Task ReturnAsync(TempSampleActionInput input, CustodyActor actor);         // back from analysis (optional consumed qty)
        Task WithdrawAsync(TempSampleActionInput input, CustodyActor actor);       // partial or full release to a recipient
        Task DisposeAsync(TempSampleActionInput input, CustodyActor actor);        // destroyed — slot released
        Task AddNoteAsync(TempSampleActionInput input, CustodyActor actor);
        Task AddAttachmentsAsync(int id, List<IFormFile> files, List<AttachmentKind>? kinds, CustodyActor actor);

        /// <summary>Generic append — used internally and by future integrations (e.g. freezer temperature excursions).</summary>
        Task<TempSampleCustodyEvent> AppendEventAsync(TempSample sample, CustodyEventType type, int? userId, string userName,
            string? counterparty = null, string? fromLocation = null, string? toLocation = null,
            decimal? qtyDelta = null, string? notes = null, string? dataJson = null, string? clientInfo = null);
    }
}
