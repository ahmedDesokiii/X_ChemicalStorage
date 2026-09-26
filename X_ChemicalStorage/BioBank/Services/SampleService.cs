// Services/SampleService.cs
using Microsoft.EntityFrameworkCore;
using X_ChemicalStorage.BioBank.DTOs;
using X_ChemicalStorage.Models;
using X_ChemicalStorage.BioBank.Enums;
using X_ChemicalStorage.Data; // أو BioBankDbContext حسب مشروعك

namespace X_ChemicalStorage.BioBank.Services
{
    public class SampleService : ISampleService
    {
        private readonly ApplicationDbContext _db;

        public SampleService(ApplicationDbContext db)
        {
            _db = db;
        }


        private static string GenerateQrCode(string sampleId)
        {
            // مثال بسيط — يمكن استبداله بـ GUID أو مكتبة QR
            return $"QR-{sampleId}-{DateTime.UtcNow:yyyyMMddHHmmss}";
        }
        // ===================== STORE =====================
        public async Task<Bio_SampleDto> StoreAsync(StoreSampleDto dto)
        {
            var position = dto.Position.ToUpper().Trim();
            ValidatePosition(position);

            return await ExecuteInTransactionAsync(async () =>
            {
                var box = await _db.Bio_Boxes.AsNoTracking()
                    .FirstOrDefaultAsync(b => b.Id == dto.BoxId)
                    ?? throw new InvalidOperationException($"Box '{dto.BoxId}' not found.");

                var tube = await GetOrCreateTubeAsync(dto.BoxId, position);

                // CHANGED: a slot locked by an active check-out can't receive a new sample either
                if (tube.Status == TubeStatus.Occupied || tube.Status == TubeStatus.CheckedOut)
                    throw new InvalidOperationException($"Position {position} is already occupied.");

                // Sample exists?
                var sample = await _db.Bio_Samples
                    .FirstOrDefaultAsync(s => s.Id == dto.SampleId);

                if (sample != null && sample.Status == SampleLifecycleStatus.Stored)
                    throw new InvalidOperationException($"Sample '{dto.SampleId}' is already stored.");

                if (sample == null)
                {
                    sample = new Bio_Sample
                    {
                        Id = dto.SampleId.Trim().ToUpper(),
                        ExternalId = dto.ExternalId,
                        Name = dto.Name,
                        SampleType = dto.SampleType,
                        PatientRef = dto.PatientRef,
                        ProjectCode = dto.ProjectCode,
                        CollectedAt = dto.CollectedAt,
                        ExpiryDate = dto.ExpiryDate,
                        Notes = dto.Notes,
                        // NEW: blood-sample-specific fields from the form
                        BloodType = dto.BloodType,
                        FastaAccessCode = dto.FastaAccessCode,
                        VolumeMl = dto.VolumeMl,
                        RemainingVolumeMl = dto.VolumeMl,   // starts full
                                                            // NEW
                        CollectedByEmployeeId = dto.CollectedByEmployeeId,
                        BacterialTestResult = dto.BacterialTestResult ?? TestResult.Pending,
                        BacterialTestDate = dto.BacterialTestDate,
                        IsolateTestResult = dto.IsolateTestResult ?? TestResult.Pending,
                        IsolateTestDate = dto.IsolateTestDate,
                        SOPId = dto.SOPId,
                        AnalyticsResults = dto.AnalyticsResults,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = dto.PerformedBy
                    };
                    _db.Bio_Samples.Add(sample);
                }
                else
                {
                    sample.Name = dto.Name;
                    sample.ExternalId = dto.ExternalId ?? sample.ExternalId;
                    sample.SampleType = dto.SampleType ?? sample.SampleType;
                    sample.PatientRef = dto.PatientRef ?? sample.PatientRef;
                    sample.ProjectCode = dto.ProjectCode ?? sample.ProjectCode;
                    sample.CollectedAt = dto.CollectedAt ?? sample.CollectedAt;
                    sample.ExpiryDate = dto.ExpiryDate ?? sample.ExpiryDate;
                    sample.Notes = dto.Notes ?? sample.Notes;
                    // NEW
                    sample.BloodType = dto.BloodType != BloodType.Unknown ? dto.BloodType : sample.BloodType;
                    sample.FastaAccessCode = dto.FastaAccessCode ?? sample.FastaAccessCode;
                    if (dto.VolumeMl.HasValue)
                    {
                        sample.VolumeMl = dto.VolumeMl;
                        sample.RemainingVolumeMl = dto.VolumeMl;
                    }
                    sample.CollectedByEmployeeId = dto.CollectedByEmployeeId ?? sample.CollectedByEmployeeId;
                    sample.UpdatedAt = DateTime.UtcNow;
                }

                var freezerId = await ResolveFreezerIdAsync(dto.BoxId);

                // Occupy tube
                tube.Status = TubeStatus.Occupied;
                tube.SampleId = sample.Id;
                tube.SampleName = sample.Name;
                tube.StoredAt = DateTime.UtcNow;
                if (!string.IsNullOrWhiteSpace(dto.TubeBarcode))     // NEW
                    tube.Barcode = dto.TubeBarcode.Trim();

                // Update sample location
                sample.Status = SampleLifecycleStatus.Stored;
                sample.CurrentTubeId = tube.Id;
                sample.CurrentBoxId = dto.BoxId;
                sample.CurrentPosition = position;
                sample.CurrentFreezerId = freezerId;
                sample.UpdatedAt = DateTime.UtcNow;

                _db.Bio_SampleMovements.Add(new Bio_SampleMovement
                {
                    SampleId = sample.Id,
                    Type = MovementType.Store,
                    ToTubeId = tube.Id,
                    ToBoxId = dto.BoxId,
                    ToPosition = position,
                    Reason = string.IsNullOrEmpty(freezerId)
                        ? "Stored in cryobox"
                        : $"Stored in cryobox — Freezer {freezerId}",
                    PerformedBy = dto.PerformedBy,
                    PerformedAt = DateTime.UtcNow
                });

                return MapSample(sample);
            });
        }

        // ===================== RESERVE =====================
        public async Task<Bio_SampleDto> ReserveAsync(ReserveSampleDto dto)
        {
            var position = dto.Position.ToUpper().Trim();
            ValidatePosition(position);

            return await ExecuteInTransactionAsync(async () =>
            {
                _ = await _db.Bio_Boxes.AsNoTracking()
                    .FirstOrDefaultAsync(b => b.Id == dto.BoxId)
                    ?? throw new InvalidOperationException($"Box '{dto.BoxId}' not found.");

                var tube = await GetOrCreateTubeAsync(dto.BoxId, position);

                // CHANGED: also block reserving a slot that's locked by a check-out
                if (tube.Status == TubeStatus.Occupied || tube.Status == TubeStatus.CheckedOut)
                    throw new InvalidOperationException($"Position {position} is already occupied.");

                if (tube.Status == TubeStatus.Reserved)
                    throw new InvalidOperationException($"Position {position} is already reserved.");

                var sampleId = string.IsNullOrWhiteSpace(dto.SampleId)
                    ? $"RSV-{DateTime.UtcNow:yyyyMMddHHmmss}"
                    : dto.SampleId.Trim().ToUpper();

                var sample = await _db.Bio_Samples.FirstOrDefaultAsync(s => s.Id == sampleId);
                if (sample == null)
                {
                    sample = new Bio_Sample
                    {
                        Id = sampleId,
                        Name = dto.SampleName ?? $"Reserved {position}",
                        Status = SampleLifecycleStatus.Reserved,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = dto.PerformedBy
                    };
                    _db.Bio_Samples.Add(sample);
                }
                else
                {
                    sample.Status = SampleLifecycleStatus.Reserved;
                    sample.UpdatedAt = DateTime.UtcNow;
                }

                tube.Status = TubeStatus.Reserved;
                tube.SampleId = sample.Id;
                tube.SampleName = sample.Name;
                tube.StoredAt = null;

                var freezerId = await ResolveFreezerIdAsync(dto.BoxId);
                sample.CurrentTubeId = tube.Id;
                sample.CurrentBoxId = dto.BoxId;
                sample.CurrentPosition = position;
                sample.CurrentFreezerId = freezerId;

                _db.Bio_SampleMovements.Add(new Bio_SampleMovement
                {
                    SampleId = sample.Id,
                    Type = MovementType.Reserve,
                    ToTubeId = tube.Id,
                    ToBoxId = dto.BoxId,
                    ToPosition = position,
                    Reason = dto.Reason ?? "Position reserved",
                    PerformedBy = dto.PerformedBy,
                    PerformedAt = DateTime.UtcNow
                });

                return MapSample(sample);
            });
        }

        // ===================== CANCEL RESERVE =====================
        public async Task<bool> CancelReserveAsync(string boxId, string position, string? performedBy = null)
        {
            position = position.ToUpper().Trim();

            return await ExecuteInTransactionAsync(async () =>
            {
                var tube = await _db.Bio_Tubes
                    .FirstOrDefaultAsync(t => t.BoxId == boxId && t.Position == position);

                if (tube == null || tube.Status != TubeStatus.Reserved)
                    return false;

                var sampleId = tube.SampleId;

                tube.Status = TubeStatus.Available;
                tube.SampleId = null;
                tube.SampleName = null;
                tube.StoredAt = null;

                if (!string.IsNullOrEmpty(sampleId))
                {
                    var sample = await _db.Bio_Samples.FirstOrDefaultAsync(s => s.Id == sampleId);
                    if (sample != null)
                    {
                        sample.Status = SampleLifecycleStatus.Draft;
                        sample.CurrentTubeId = null;
                        sample.CurrentBoxId = null;
                        sample.CurrentPosition = null;
                        sample.CurrentFreezerId = null;
                        sample.UpdatedAt = DateTime.UtcNow;

                        _db.Bio_SampleMovements.Add(new Bio_SampleMovement
                        {
                            SampleId = sample.Id,
                            Type = MovementType.CancelReserve,
                            FromTubeId = tube.Id,
                            FromBoxId = boxId,
                            FromPosition = position,
                            Reason = "Reservation cancelled",
                            PerformedBy = performedBy,
                            PerformedAt = DateTime.UtcNow
                        });
                    }
                }

                return true;
            });
        }

        // ===================== WITHDRAW =====================
        public async Task<Bio_SampleDto> WithdrawAsync(WithdrawSampleDto dto)
        {
            return await ExecuteInTransactionAsync(async () =>
            {
                var sample = await _db.Bio_Samples
                    .FirstOrDefaultAsync(s => s.Id == dto.SampleId)
                    ?? throw new InvalidOperationException($"Sample '{dto.SampleId}' not found.");

                if (sample.Status != SampleLifecycleStatus.Stored && sample.Status != SampleLifecycleStatus.Reserved)
                    throw new InvalidOperationException($"Sample '{dto.SampleId}' is not currently stored/reserved.");

                if (string.IsNullOrEmpty(sample.CurrentTubeId))
                    throw new InvalidOperationException("Sample has no current location.");

                var tube = await _db.Bio_Tubes.FirstOrDefaultAsync(t => t.Id == sample.CurrentTubeId);

                var fromTubeId = sample.CurrentTubeId;
                var fromBoxId = sample.CurrentBoxId;
                var fromPos = sample.CurrentPosition;

                if (tube != null)
                {
                    tube.Status = TubeStatus.Available;
                    tube.SampleId = null;
                    tube.SampleName = null;
                    tube.StoredAt = null;
                }

                sample.Status = SampleLifecycleStatus.Withdrawn;
                sample.CurrentTubeId = null;
                sample.CurrentBoxId = null;
                sample.CurrentPosition = null;
                sample.CurrentFreezerId = null;
                sample.UpdatedAt = DateTime.UtcNow;

                _db.Bio_SampleMovements.Add(new Bio_SampleMovement
                {
                    SampleId = sample.Id,
                    Type = MovementType.Withdraw,
                    FromTubeId = fromTubeId,
                    FromBoxId = fromBoxId,
                    FromPosition = fromPos,
                    Reason = dto.Reason ?? "Withdrawn",
                    Notes = dto.Notes,
                    PerformedBy = dto.PerformedBy,
                    PerformedAt = DateTime.UtcNow
                });

                return MapSample(sample);
            });
        }

        // ===================== TRANSFER =====================
        public async Task<Bio_SampleDto> TransferAsync(TransferSampleDto dto)
        {
            var toPosition = dto.ToPosition.ToUpper().Trim();
            ValidatePosition(toPosition);

            return await ExecuteInTransactionAsync(async () =>
            {
                var sample = await _db.Bio_Samples
                    .FirstOrDefaultAsync(s => s.Id == dto.SampleId)
                    ?? throw new InvalidOperationException($"Sample '{dto.SampleId}' not found.");

                if (sample.Status != SampleLifecycleStatus.Stored)
                    throw new InvalidOperationException("Only stored samples can be transferred.");

                if (string.IsNullOrEmpty(sample.CurrentTubeId))
                    throw new InvalidOperationException("Sample has no current location.");

                _ = await _db.Bio_Boxes.AsNoTracking()
                    .FirstOrDefaultAsync(b => b.Id == dto.ToBoxId)
                    ?? throw new InvalidOperationException($"Target box '{dto.ToBoxId}' not found.");

                var toTube = await GetOrCreateTubeAsync(dto.ToBoxId, toPosition);
                // CHANGED: also block transferring into a slot locked by a check-out
                if (toTube.Status == TubeStatus.Occupied || toTube.Status == TubeStatus.CheckedOut)
                    throw new InvalidOperationException($"Target position {toPosition} is already occupied.");

                // Free old tube
                var fromTube = await _db.Bio_Tubes.FirstOrDefaultAsync(t => t.Id == sample.CurrentTubeId);
                var fromTubeId = sample.CurrentTubeId;
                var fromBoxId = sample.CurrentBoxId;
                var fromPos = sample.CurrentPosition;

                if (fromTube != null)
                {
                    fromTube.Status = TubeStatus.Available;
                    fromTube.SampleId = null;
                    fromTube.SampleName = null;
                    fromTube.StoredAt = null;
                }

                // Occupy new tube
                toTube.Status = TubeStatus.Occupied;
                toTube.SampleId = sample.Id;
                toTube.SampleName = sample.Name;
                toTube.StoredAt = DateTime.UtcNow;

                var freezerId = await ResolveFreezerIdAsync(dto.ToBoxId);

                sample.Status = SampleLifecycleStatus.Stored; // still stored after transfer
                sample.CurrentTubeId = toTube.Id;
                sample.CurrentBoxId = dto.ToBoxId;
                sample.CurrentPosition = toPosition;
                sample.CurrentFreezerId = freezerId;
                sample.UpdatedAt = DateTime.UtcNow;

                _db.Bio_SampleMovements.Add(new Bio_SampleMovement
                {
                    SampleId = sample.Id,
                    Type = MovementType.Transfer,
                    FromTubeId = fromTubeId,
                    FromBoxId = fromBoxId,
                    FromPosition = fromPos,
                    ToTubeId = toTube.Id,
                    ToBoxId = dto.ToBoxId,
                    ToPosition = toPosition,
                    Reason = !string.IsNullOrWhiteSpace(dto.Reason)
                    ? dto.Reason
                    : (string.IsNullOrEmpty(freezerId) ? "Transferred" : $"Transferred to Freezer {freezerId}"),
                    PerformedBy = dto.PerformedBy,
                    PerformedAt = DateTime.UtcNow
                });

                return MapSample(sample);
            });
        }

        // ===================== CHECK-OUT (NEW) =====================
        // Sample goes out for an experiment. Tube position stays LOCKED (CheckedOut),
        // not freed — so no one else can claim it while the sample is away.
        public async Task<Bio_SampleDto> CheckOutAsync(CheckOutSampleDto dto)
        {
            return await ExecuteInTransactionAsync(async () =>
            {
                var sample = await _db.Bio_Samples
                    .FirstOrDefaultAsync(s => s.Id == dto.SampleId)
                    ?? throw new InvalidOperationException($"Sample '{dto.SampleId}' not found.");

                if (sample.Status != SampleLifecycleStatus.Stored)
                    throw new InvalidOperationException($"Only stored samples can be checked out. Current status: {sample.Status}.");

                if (string.IsNullOrEmpty(sample.CurrentTubeId))
                    throw new InvalidOperationException("Sample has no current location.");

                var tube = await _db.Bio_Tubes.FirstOrDefaultAsync(t => t.Id == sample.CurrentTubeId)
                    ?? throw new InvalidOperationException("Sample's tube record not found.");

                // Note: tube.BoxId / tube.Position / sample.CurrentTubeId etc are left UNTOUCHED on purpose —
                // this is the whole point: the slot stays reserved for this exact sample.
                tube.Status = TubeStatus.CheckedOut;

                sample.Status = SampleLifecycleStatus.CheckedOut;
                sample.CheckedOutAt = DateTime.UtcNow;
                sample.CheckedOutBy = dto.PerformedBy;
                sample.CheckOutReason = dto.Reason;
                sample.ExpectedReturnAt = dto.ExpectedReturnAt;
                sample.ReturnedAt = null;
                sample.UpdatedAt = DateTime.UtcNow;

                _db.Bio_SampleMovements.Add(new Bio_SampleMovement
                {
                    SampleId = sample.Id,
                    Type = MovementType.CheckOut,
                    FromTubeId = tube.Id,
                    FromBoxId = tube.BoxId,
                    FromPosition = tube.Position,
                    Reason = dto.Reason,
                    PerformedBy = dto.PerformedBy,
                    PerformedAt = DateTime.UtcNow
                });

                return MapSample(sample);
            });
        }

        // ===================== CHECK-IN (NEW) =====================
        // Sample returns from testing. Either goes back to Stored in the SAME tube,
        // or — if consumed / nothing left — gets Disposed and the slot finally opens up.
        public async Task<Bio_SampleDto> CheckInAsync(CheckInSampleDto dto)
        {
            return await ExecuteInTransactionAsync(async () =>
            {
                var sample = await _db.Bio_Samples
                    .FirstOrDefaultAsync(s => s.Id == dto.SampleId)
                    ?? throw new InvalidOperationException($"Sample '{dto.SampleId}' not found.");

                if (sample.Status != SampleLifecycleStatus.CheckedOut)
                    throw new InvalidOperationException($"Sample '{dto.SampleId}' is not currently checked out.");

                if (string.IsNullOrEmpty(sample.CurrentTubeId))
                    throw new InvalidOperationException("Sample has no current location.");

                var tube = await _db.Bio_Tubes.FirstOrDefaultAsync(t => t.Id == sample.CurrentTubeId)
                    ?? throw new InvalidOperationException("Sample's tube record not found.");

                var fullyConsumed = dto.Consumed || (dto.RemainingVolumeMl.HasValue && dto.RemainingVolumeMl <= 0);

                if (fullyConsumed)
                {
                    // Nothing came back — dispose, and NOW the slot opens up.
                    tube.Status = TubeStatus.Available;
                    tube.SampleId = null;
                    tube.SampleName = null;
                    tube.StoredAt = null;

                    sample.Status = SampleLifecycleStatus.Disposed;
                    sample.RemainingVolumeMl = 0;
                    sample.ReturnedAt = DateTime.UtcNow;
                    sample.CurrentTubeId = null;
                    sample.CurrentBoxId = null;
                    sample.CurrentPosition = null;
                    sample.CurrentFreezerId = null;
                    sample.UpdatedAt = DateTime.UtcNow;

                    _db.Bio_SampleMovements.Add(new Bio_SampleMovement
                    {
                        SampleId = sample.Id,
                        Type = MovementType.Dispose,
                        FromTubeId = tube.Id,
                        FromBoxId = tube.BoxId,
                        FromPosition = tube.Position,
                        Reason = "Fully consumed during testing",
                        Notes = dto.Notes,
                        PerformedBy = dto.PerformedBy,
                        PerformedAt = DateTime.UtcNow
                    });
                }
                else
                {
                    // Back to storage, same tube, same position — nothing to re-pick.
                    tube.Status = TubeStatus.Occupied;
                    tube.StoredAt = DateTime.UtcNow;

                    sample.Status = SampleLifecycleStatus.Stored;
                    sample.RemainingVolumeMl = dto.RemainingVolumeMl ?? sample.RemainingVolumeMl;
                    sample.ReturnedAt = DateTime.UtcNow;
                    sample.UpdatedAt = DateTime.UtcNow;

                    _db.Bio_SampleMovements.Add(new Bio_SampleMovement
                    {
                        SampleId = sample.Id,
                        Type = MovementType.CheckIn,
                        ToTubeId = tube.Id,
                        ToBoxId = tube.BoxId,
                        ToPosition = tube.Position,
                        Reason = "Returned from testing",
                        Notes = dto.Notes,
                        PerformedBy = dto.PerformedBy,
                        PerformedAt = DateTime.UtcNow
                    });
                }

                return MapSample(sample);
            });
        }

        // ===================== ACTIVE / OVERDUE CHECK-OUTS (NEW) =====================
        public async Task<List<Bio_SampleDto>> GetActiveCheckOutsAsync()
        {
            var list = await _db.Bio_Samples.AsNoTracking()
                .Where(s => s.Status == SampleLifecycleStatus.CheckedOut)
                .OrderBy(s => s.ExpectedReturnAt == null)   // samples with a due date first
                .ThenBy(s => s.ExpectedReturnAt)
                .ToListAsync();

            return list.Select(MapSample).ToList();
        }

        public async Task<List<Bio_SampleDto>> GetOverdueCheckOutsAsync()
        {
            var now = DateTime.UtcNow;
            var list = await _db.Bio_Samples.AsNoTracking()
                .Where(s => s.Status == SampleLifecycleStatus.CheckedOut
                            && s.ExpectedReturnAt != null
                            && s.ExpectedReturnAt < now)
                .OrderBy(s => s.ExpectedReturnAt)
                .ToListAsync();

            return list.Select(MapSample).ToList();
        }

        // ===================== GET / HISTORY / SEARCH =====================
        public async Task<Bio_SampleDto?> GetByIdAsync(string sampleId)
        {
            var s = await _db.Bio_Samples.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == sampleId);
            return s == null ? null : MapSample(s);
        }

        public async Task<List<Bio_SampleMovementDto>> GetHistoryAsync(string sampleId)
        {
            return await _db.Bio_SampleMovements.AsNoTracking()
                .Where(m => m.SampleId == sampleId)
                .OrderByDescending(m => m.PerformedAt)
                .Select(m => new Bio_SampleMovementDto
                {
                    Id = m.Id,
                    SampleId = m.SampleId,
                    Type = m.Type,
                    FromTubeId = m.FromTubeId,
                    ToTubeId = m.ToTubeId,
                    FromPosition = m.FromPosition,
                    ToPosition = m.ToPosition,
                    FromBoxId = m.FromBoxId,
                    ToBoxId = m.ToBoxId,
                    Reason = m.Reason,
                    Notes = m.Notes,
                    PerformedBy = m.PerformedBy,
                    PerformedAt = m.PerformedAt
                })
                .ToListAsync();
        }

        // NEW
        public async Task<List<Bio_SampleDto>> SearchAsync(string query)
        {
            if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
                return new List<Bio_SampleDto>();

            query = query.Trim().ToUpper();

            // Barcode lives on Bio_Tube, not Bio_Sample — resolve matching sample ids first
            var barcodeSampleIds = await _db.Bio_Tubes.AsNoTracking()
                .Where(t => t.Barcode != null && t.Barcode.ToUpper().Contains(query) && t.SampleId != null)
                .Select(t => t.SampleId!)
                .Distinct()
                .ToListAsync();

            var list = await _db.Bio_Samples.AsNoTracking()
                .Where(s =>
                    s.Id.Contains(query) ||
                    (s.ExternalId != null && s.ExternalId.ToUpper().Contains(query)) ||
                    s.Name.ToUpper().Contains(query) ||
                    (s.PatientRef != null && s.PatientRef.ToUpper().Contains(query)) ||
                    (s.ProjectCode != null && s.ProjectCode.ToUpper().Contains(query)) ||
                    (s.FastaAccessCode != null && s.FastaAccessCode.ToUpper().Contains(query)) ||
                    barcodeSampleIds.Contains(s.Id))
                .OrderByDescending(s => s.CreatedAt)
                .Take(50)
                .ToListAsync();

            return list.Select(MapSample).ToList();
        }

        public async Task<TubeSampleDetailsDto?> GetTubeDetailsAsync(string boxId, string position)
        {
            position = position.ToUpper().Trim();

            var tube = await _db.Bio_Tubes.AsNoTracking()
                .FirstOrDefaultAsync(t => t.BoxId == boxId && t.Position == position);

            if (tube == null)
            {
                // empty position still valid
                return new TubeSampleDetailsDto
                {
                    TubeId = $"{boxId}-{position}",
                    BoxId = boxId,
                    Position = position,
                    TubeStatus = TubeStatus.Available,
                    Barcode = null,
                    Sample = null
                };
            }

            Bio_SampleDto? sampleDto = null;
            if (!string.IsNullOrEmpty(tube.SampleId))
            {
                var sample = await _db.Bio_Samples.AsNoTracking()
                    .FirstOrDefaultAsync(s => s.Id == tube.SampleId);
                if (sample != null) sampleDto = MapSample(sample);
            }

            return new TubeSampleDetailsDto
            {
                TubeId = tube.Id,
                BoxId = boxId,
                Position = position,
                TubeStatus = tube.Status,
                Barcode = tube.Barcode,
                Sample = sampleDto
            };
        }

        // ===================== HELPERS =====================
        private async Task<Bio_Tube> GetOrCreateTubeAsync(string boxId, string position)
        {
            var tube = await _db.Bio_Tubes
                .FirstOrDefaultAsync(t => t.BoxId == boxId && t.Position == position);

            if (tube != null) return tube;

            var (row, col) = ParsePosition(position);
            tube = new Bio_Tube
            {
                Id = $"{boxId}-{position}",
                BoxId = boxId,
                Position = position,
                Row = row,
                Column = col,
                Status = TubeStatus.Available
            };
            _db.Bio_Tubes.Add(tube);
            return tube;
        }

        private async Task<string?> ResolveFreezerIdAsync(string boxId)
        {
            var box = await _db.Bio_Boxes.AsNoTracking().FirstOrDefaultAsync(b => b.Id == boxId);
            if (box == null) return null;

            if (!string.IsNullOrEmpty(box.DrawerId))
            {
                var drawer = await _db.Bio_Drawers.AsNoTracking().FirstAsync(d => d.Id == box.DrawerId);
                var rack = await _db.Bio_Racks.AsNoTracking().FirstAsync(r => r.Id == drawer.RackId);
                return rack.FreezerId;
            }

            if (!string.IsNullOrEmpty(box.RackId))
            {
                var rack = await _db.Bio_Racks.AsNoTracking().FirstAsync(r => r.Id == box.RackId);
                return rack.FreezerId;
            }

            return null;
        }

        private static void ValidatePosition(string position)
        {
            if (string.IsNullOrWhiteSpace(position) || position.Length < 2)
                throw new ArgumentException("Invalid position. Expected e.g. A01");

            char row = char.ToUpper(position[0]);
            if (row < 'A' || row > 'J')
                throw new ArgumentException("Row must be A-J");

            if (!int.TryParse(position.Substring(1), out int col) || col < 1 || col > 10)
                throw new ArgumentException("Column must be 01-10");
        }

        private static (int row, int col) ParsePosition(string position)
        {
            ValidatePosition(position);
            return (char.ToUpper(position[0]) - 'A', int.Parse(position.Substring(1)) - 1);
        }

        private static Bio_SampleDto MapSample(Bio_Sample s) => new()
        {
            Id = s.Id,
            ExternalId = s.ExternalId,
            Name = s.Name,
            SampleType = s.SampleType,
            PatientRef = s.PatientRef,
            ProjectCode = s.ProjectCode,
            CollectedAt = s.CollectedAt,
            ExpiryDate = s.ExpiryDate,
            Notes = s.Notes,
            Status = s.Status,
            BloodType = s.BloodType,
            FastaAccessCode = s.FastaAccessCode,
            VolumeMl = s.VolumeMl,
            RemainingVolumeMl = s.RemainingVolumeMl,
            CollectedByEmployeeId = s.CollectedByEmployeeId,
            CheckedOutAt = s.CheckedOutAt,
            CheckedOutBy = s.CheckedOutBy,
            CheckOutReason = s.CheckOutReason,
            ExpectedReturnAt = s.ExpectedReturnAt,
            ReturnedAt = s.ReturnedAt,
            CurrentTubeId = s.CurrentTubeId,
            CurrentBoxId = s.CurrentBoxId,
            CurrentPosition = s.CurrentPosition,
            CurrentFreezerId = s.CurrentFreezerId,
            BacterialTestResult = s.BacterialTestResult,
            BacterialTestDate = s.BacterialTestDate,
            IsolateTestResult = s.IsolateTestResult,
            IsolateTestDate = s.IsolateTestDate,
            SOPId = s.SOPId,
            AnalyticsResults = s.AnalyticsResults,
            CreatedAt = s.CreatedAt,
            CreatedBy = s.CreatedBy
        };

        private async Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> operation)
        {
            await using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                var result = await operation();
                await _db.SaveChangesAsync();
                await tx.CommitAsync();
                return result;
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }

        // Add anywhere in the class, e.g. right after CheckInAsync():

        // ===================== NEXT SAMPLE ID (NEW) =====================
        public async Task<string> GenerateNextSampleIdAsync()
        {
            var prefix = $"SMP-{DateTime.UtcNow.Year}-";

            var count = await _db.Bio_Samples
                .AsNoTracking()
                .CountAsync(s => s.Id.StartsWith(prefix));

            return $"{prefix}{(count + 1):D5}";
        }


        // Add anywhere in the class, e.g. right after WithdrawAsync():

        // ===================== UPDATE / EDIT (NEW) =====================
        public async Task<Bio_SampleDto> UpdateAsync(UpdateSampleDto dto)
        {
            return await ExecuteInTransactionAsync(async () =>
            {
                var sample = await _db.Bio_Samples
                    .FirstOrDefaultAsync(s => s.Id == dto.SampleId)
                    ?? throw new InvalidOperationException($"Sample '{dto.SampleId}' not found.");

                if (!string.IsNullOrWhiteSpace(dto.Name)) sample.Name = dto.Name;
                sample.ExternalId = dto.ExternalId ?? sample.ExternalId;
                sample.SampleType = dto.SampleType ?? sample.SampleType;
                sample.PatientRef = dto.PatientRef ?? sample.PatientRef;
                sample.ProjectCode = dto.ProjectCode ?? sample.ProjectCode;
                sample.CollectedAt = dto.CollectedAt ?? sample.CollectedAt;
                sample.ExpiryDate = dto.ExpiryDate ?? sample.ExpiryDate;
                sample.Notes = dto.Notes ?? sample.Notes;
                if (dto.BloodType.HasValue) sample.BloodType = dto.BloodType.Value;
                sample.FastaAccessCode = dto.FastaAccessCode ?? sample.FastaAccessCode;
                if (dto.VolumeMl.HasValue) sample.VolumeMl = dto.VolumeMl;
                sample.CollectedByEmployeeId = dto.CollectedByEmployeeId ?? sample.CollectedByEmployeeId;
                sample.UpdatedAt = DateTime.UtcNow;

                if (dto.BacterialTestResult.HasValue) sample.BacterialTestResult = dto.BacterialTestResult.Value;
                sample.BacterialTestDate = dto.BacterialTestDate ?? sample.BacterialTestDate;
                if (dto.IsolateTestResult.HasValue) sample.IsolateTestResult = dto.IsolateTestResult.Value;
                sample.IsolateTestDate = dto.IsolateTestDate ?? sample.IsolateTestDate;
                sample.SOPId = dto.SOPId ?? sample.SOPId;
                sample.AnalyticsResults = dto.AnalyticsResults ?? sample.AnalyticsResults;

                // Keep the tube's denormalized SampleName / Barcode in sync
                if (!string.IsNullOrEmpty(sample.CurrentTubeId))
                {
                    var tube = await _db.Bio_Tubes.FirstOrDefaultAsync(t => t.Id == sample.CurrentTubeId);
                    if (tube != null)
                    {
                        if (!string.IsNullOrWhiteSpace(dto.Name))
                            tube.SampleName = sample.Name;

                        if (!string.IsNullOrWhiteSpace(dto.TubeBarcode))
                            tube.Barcode = dto.TubeBarcode.Trim();
                    }
                }

                _db.Bio_SampleMovements.Add(new Bio_SampleMovement
                {
                    SampleId = sample.Id,
                    Type = MovementType.Update,
                    Reason = "Sample details edited",
                    PerformedBy = dto.PerformedBy,
                    PerformedAt = DateTime.UtcNow
                });

                return MapSample(sample);
            });
        }

        // Add anywhere in the class, e.g. right after ResolveFreezerIdAsync():

        public async Task<BoxLocationDto?> GetBoxLocationAsync(string boxId)
        {
            var box = await _db.Bio_Boxes.AsNoTracking().FirstOrDefaultAsync(b => b.Id == boxId);
            if (box == null) return null;

            if (!string.IsNullOrEmpty(box.DrawerId))
            {
                var drawer = await _db.Bio_Drawers.AsNoTracking().FirstAsync(d => d.Id == box.DrawerId);
                var rack = await _db.Bio_Racks.AsNoTracking().FirstAsync(r => r.Id == drawer.RackId);
                return new BoxLocationDto
                {
                    FreezerId = rack.FreezerId,
                    ShelfIndex = rack.ShelfIndex,
                    RackId = rack.Id,
                    RackIndex = rack.RackIndex,
                    DrawerId = drawer.Id,
                    DrawerIndex = drawer.DrawerIndex,
                    BoxId = box.Id
                };
            }

            if (!string.IsNullOrEmpty(box.RackId))
            {
                var rack = await _db.Bio_Racks.AsNoTracking().FirstAsync(r => r.Id == box.RackId);
                return new BoxLocationDto
                {
                    FreezerId = rack.FreezerId,
                    ShelfIndex = null,
                    RackId = rack.Id,
                    RackIndex = rack.RackIndex,
                    DrawerId = null,
                    DrawerIndex = null,
                    BoxId = box.Id
                };
            }

            return null;
        }


        // Add right after GetBoxLocationAsync():

        public async Task<NextAvailableSlotDto?> FindNextAvailableSlotAsync(string? freezerId = null)
        {
            var freezersQuery = _db.Bio_Freezers.AsNoTracking()
                .Where(f => f.Status == FreezerStatus.Online);

            if (!string.IsNullOrWhiteSpace(freezerId))
                freezersQuery = freezersQuery.Where(f => f.Id == freezerId);

            var freezers = await freezersQuery.OrderBy(f => f.Id).ToListAsync();

            foreach (var freezer in freezers)
            {
                var boxes = await _db.Bio_Boxes.AsNoTracking()
                    .Where(b =>
                        (b.DrawerId != null && _db.Bio_Drawers.Any(d => d.Id == b.DrawerId &&
                            _db.Bio_Racks.Any(r => r.Id == d.RackId && r.FreezerId == freezer.Id)))
                        ||
                        (b.RackId != null && _db.Bio_Racks.Any(r => r.Id == b.RackId && r.FreezerId == freezer.Id)))
                    .OrderBy(b => b.Id)
                    .ToListAsync();

                foreach (var box in boxes)
                {
                    var takenPositions = await _db.Bio_Tubes.AsNoTracking()
                        .Where(t => t.BoxId == box.Id && t.Status != TubeStatus.Available)
                        .Select(t => t.Position)
                        .ToListAsync();

                    var taken = new HashSet<string>(takenPositions);

                    for (int row = 0; row < box.Rows; row++)
                    {
                        for (int col = 0; col < box.Columns; col++)
                        {
                            var pos = $"{(char)('A' + row)}{(col + 1):D2}";
                            if (!taken.Contains(pos))
                            {
                                return new NextAvailableSlotDto
                                {
                                    FreezerId = freezer.Id,
                                    BoxId = box.Id,
                                    Position = pos
                                };
                            }
                        }
                    }
                }
            }

            return null; // every online freezer is completely full
        }

        // ===================== SOPs (NEW) =====================
        public async Task<List<Bio_SOPDto>> GetSOPsAsync()
        {
            return await _db.Bio_SOPs.AsNoTracking()
                .Where(s => s.IsActive)
                .OrderBy(s => s.Name)
                .Select(s => new Bio_SOPDto { Id = s.Id, Name = s.Name, Description = s.Description })
                .ToListAsync();
        }

        public async Task<Bio_SOPDto> CreateSOPAsync(CreateSOPDto dto)
        {
            if (await _db.Bio_SOPs.AnyAsync(s => s.Id == dto.Id))
                throw new InvalidOperationException($"SOP '{dto.Id}' already exists.");

            var sop = new Bio_SOP { Id = dto.Id.Trim(), Name = dto.Name.Trim(), Description = dto.Description, IsActive = true };
            _db.Bio_SOPs.Add(sop);
            await _db.SaveChangesAsync();

            return new Bio_SOPDto { Id = sop.Id, Name = sop.Name, Description = sop.Description };
        }
    }
}