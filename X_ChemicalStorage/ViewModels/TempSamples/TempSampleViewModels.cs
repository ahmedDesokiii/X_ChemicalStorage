using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using X_ChemicalStorage.Models.TempSamples;

namespace X_ChemicalStorage.ViewModels.TempSamples
{
    public class TempSampleReceiveViewModel
    {
        // Step 1 — Hand-over
        [Required] public DateTime DeliveryDateTime { get; set; } = DateTime.Now;
        [Required] public DateTime ReceptionDateTime { get; set; } = DateTime.Now;
        [Required, MaxLength(150)] public string DeliveredByName { get; set; } = "";
        [MaxLength(200)] public string? DeliveringOrganization { get; set; }
        [MaxLength(200)] public string? ReceivingOrganization { get; set; }

        // Step 2 — Sample
        [Required, MaxLength(100)] public string SampleType { get; set; } = "";
        [MaxLength(1000)] public string? Description { get; set; }
        [Range(0.001, 1_000_000)] public decimal QuantityReceived { get; set; } = 1;
        public int? UnitId { get; set; }
        [Range(1, 10_000)] public int ContainersCount { get; set; } = 1;
        [MaxLength(100)] public string? ExternalBarcode { get; set; }
        [MaxLength(500)] public string? RequestedAnalysis { get; set; }
        [MaxLength(100)] public string? RequestReferenceNumber { get; set; }
        public DateTime? PlannedWithdrawalDate { get; set; }

        // Step 3 — Transport & documents
        public TransportCondition TransportCondition { get; set; } = TransportCondition.Refrigerated;
        [Range(-200, 60)] public decimal? ArrivalTemperatureC { get; set; }
        [MaxLength(2000)] public string? Comments { get; set; }
        public List<IFormFile>? Files { get; set; }
        public List<AttachmentKind>? FileKinds { get; set; }   // same order as Files

        // Step 4 — Location (optional: can be received now, placed later)
        public int? BoxId { get; set; }
        public int? RowIndex { get; set; }
        public int? ColumnIndex { get; set; }

        // Dropdown sources
        public List<SelectListItem> ListRooms { get; set; } = new();
        public List<SelectListItem> ListUnits { get; set; } = new();
        public List<string> SampleTypeSuggestions { get; set; } = new();
    }

    public class TempSampleListRow
    {
        public int Id { get; set; }
        public string SampleCode { get; set; } = "";
        public string? QrImagePath { get; set; }
        public string SampleType { get; set; } = "";
        public string? DeliveringOrganization { get; set; }
        public string? RequestReferenceNumber { get; set; }
        public decimal CurrentQuantity { get; set; }
        public string? UnitName { get; set; }
        public TempSampleStatus Status { get; set; }
        public DateTime ReceptionDateTime { get; set; }
        public DateTime? PlannedWithdrawalDate { get; set; }
        public bool ArrivedOutOfRange { get; set; }
        public string? LocationPath { get; set; }
        public int EventsCount { get; set; }

        public int? DaysToWithdrawal =>
            PlannedWithdrawalDate.HasValue ? (int)(PlannedWithdrawalDate.Value.Date - DateTime.Today).TotalDays : null;
    }

    public class TempSampleDashboardStats
    {
        public int InCustody { get; set; }
        public int AwaitingPlacement { get; set; }
        public int DueThisWeek { get; set; }
        public int Overdue { get; set; }
        public int WithdrawnThisMonth { get; set; }
    }

    public class TempSampleIndexViewModel
    {
        public TempSampleDashboardStats Stats { get; set; } = new();
        public List<TempSampleListRow> Rows { get; set; } = new();
        public TempSampleStatus? Filter { get; set; }
        public string? Search { get; set; }
    }
}

namespace X_ChemicalStorage.ViewModels.TempSamples
{
    /// <summary>Who did it — passed to every custody mutation.</summary>
    public record CustodyActor(int? UserId, string Name, string? ClientInfo);

    /// <summary>Everything the Sample Dossier page needs.</summary>
    public class TempSampleDossierViewModel
    {
        public TempSample Sample { get; set; } = default!;
        public List<TempSampleCustodyEvent> Events { get; set; } = new();
        public List<TempSampleAttachment> Attachments { get; set; } = new();

        public string? RoomName { get; set; }
        public string? UnitName { get; set; }
        public string? UnitTypeName { get; set; }
        public string? ShelfName { get; set; }
        public string? BoxName { get; set; }
        public int BoxRows { get; set; }
        public int BoxCols { get; set; }

        public int? BrokenAtSequence { get; set; }      // null = chain intact
        public bool IsFinal => Sample.Status is TempSampleStatus.Withdrawn or TempSampleStatus.Disposed;

        public List<SelectListItem> ListHoldingRooms { get; set; } = new();
        public List<SelectListItem> ListUnits { get; set; } = new();

        public TempSampleEditViewModel Edit { get; set; } = new();
    }

    /// <summary>Metadata correction — every change is diffed into the custody chain with a mandatory reason.</summary>
    public class TempSampleEditViewModel
    {
        public int Id { get; set; }

        [Required] public DateTime DeliveryDateTime { get; set; }
        [Required] public DateTime ReceptionDateTime { get; set; }
        [Required, MaxLength(150)] public string DeliveredByName { get; set; } = "";
        [MaxLength(200)] public string? DeliveringOrganization { get; set; }
        [MaxLength(200)] public string? ReceivingOrganization { get; set; }

        [Required, MaxLength(100)] public string SampleType { get; set; } = "";
        [MaxLength(1000)] public string? Description { get; set; }
        [MaxLength(100)] public string? ExternalBarcode { get; set; }
        public int? UnitId { get; set; }
        [Range(1, 10_000)] public int ContainersCount { get; set; } = 1;
        [MaxLength(500)] public string? RequestedAnalysis { get; set; }
        [MaxLength(100)] public string? RequestReferenceNumber { get; set; }
        public DateTime? PlannedWithdrawalDate { get; set; }

        public TransportCondition TransportCondition { get; set; }
        [Range(-200, 60)] public decimal? ArrivalTemperatureC { get; set; }
        [MaxLength(2000)] public string? Comments { get; set; }

        [Required(ErrorMessage = "A reason for the change is required (audit trail)."), MaxLength(500)]
        public string ChangeReason { get; set; } = "";

        public static TempSampleEditViewModel From(TempSample s) => new()
        {
            Id = s.Id,
            DeliveryDateTime = s.DeliveryDateTime,
            ReceptionDateTime = s.ReceptionDateTime,
            DeliveredByName = s.DeliveredByName,
            DeliveringOrganization = s.DeliveringOrganization,
            ReceivingOrganization = s.ReceivingOrganization,
            SampleType = s.SampleType,
            Description = s.Description,
            ExternalBarcode = s.ExternalBarcode,
            UnitId = s.UnitId,
            ContainersCount = s.ContainersCount,
            RequestedAnalysis = s.RequestedAnalysis,
            RequestReferenceNumber = s.RequestReferenceNumber,
            PlannedWithdrawalDate = s.PlannedWithdrawalDate,
            TransportCondition = s.TransportCondition,
            ArrivalTemperatureC = s.ArrivalTemperatureC,
            Comments = s.Comments
        };
    }

    /// <summary>One input model for every custody action posted from the dossier.</summary>
    public class TempSampleActionInput
    {
        public int Id { get; set; }

        // Place / Relocate
        public int? BoxId { get; set; }
        public int? RowIndex { get; set; }
        public int? ColumnIndex { get; set; }

        // Withdraw / Return / Dispose
        [Range(0, 1_000_000)] public decimal? Quantity { get; set; }
        public bool FullWithdrawal { get; set; }

        [MaxLength(150)] public string? Counterparty { get; set; }   // handed to / checked out to / witness
        [MaxLength(500)] public string? Reason { get; set; }
        [MaxLength(1000)] public string? Notes { get; set; }
        [MaxLength(60)] public string? Method { get; set; }          // disposal method
        public DateTime? ExpectedReturn { get; set; }               // check-out

        /// <summary>PNG data URL from the on-screen signature pad (optional).</summary>
        public string? SignatureDataUrl { get; set; }
    }
}
