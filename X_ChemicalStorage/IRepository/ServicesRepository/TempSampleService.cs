using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using QRCoder;
using X_ChemicalStorage.Data;
using X_ChemicalStorage.IRepository;
using X_ChemicalStorage.Models;
using X_ChemicalStorage.Models.TempSamples;
using X_ChemicalStorage.ViewModels.TempSamples;

namespace X_ChemicalStorage.Services
{
    public class TempSampleService : ITempSampleService
    {
        private const string GenesisHash = "0000000000000000000000000000000000000000000000000000000000000000";

        private static readonly HashSet<string> DeniedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".exe", ".dll", ".bat", ".cmd", ".sh", ".msi", ".com", ".scr", ".ps1",
            ".vbs", ".vbe", ".js", ".jse", ".jar", ".app", ".cpl", ".lnk", ".reg", ".wsf", ".msc"
        };
        private const long MaxFileBytes = 25L * 1024 * 1024; // 25 MB per document

        private readonly ApplicationDbContext _db;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<TempSampleService> _logger;
        private readonly string _uploadRoot;
        private readonly string _qrRoot;

        public TempSampleService(ApplicationDbContext db, IWebHostEnvironment env, ILogger<TempSampleService> logger)
        {
            _db = db;
            _env = env;
            _logger = logger;
            _uploadRoot = Path.Combine(env.ContentRootPath, "App_Data", "tempsample-uploads"); // not URL-reachable
            _qrRoot = Path.Combine(env.WebRootPath, "tempsample-qr");
            Directory.CreateDirectory(_uploadRoot);
            Directory.CreateDirectory(_qrRoot);
        }

        // ══════════════════════════════════════════════════════════════
        //  RECEIVE
        // ══════════════════════════════════════════════════════════════
        public async Task<TempSample> ReceiveAsync(TempSampleReceiveViewModel vm, int userId, string userName, string? clientInfo)
        {
            if (vm.ReceptionDateTime < vm.DeliveryDateTime)
                throw new InvalidOperationException("Reception time cannot be earlier than delivery time.");

            // ---- validate files up-front (before touching the DB) ----
            var files = vm.Files?.Where(f => f is { Length: > 0 }).ToList() ?? new();
            foreach (var f in files)
            {
                if (DeniedExtensions.Contains(Path.GetExtension(f.FileName)))
                    throw new InvalidOperationException($"File type '{Path.GetExtension(f.FileName)}' is not allowed.");
                if (f.Length > MaxFileBytes)
                    throw new InvalidOperationException($"'{f.FileName}' exceeds the 25 MB limit.");
            }

            // ---- validate slot (optional) ----
            string? locationPath = null;
            bool hasSlot = vm.BoxId.HasValue && vm.RowIndex.HasValue && vm.ColumnIndex.HasValue;
            if (hasSlot)
                locationPath = await ValidateSlotAsync(vm.BoxId!.Value, vm.RowIndex!.Value, vm.ColumnIndex!.Value);

            var writtenFiles = new List<string>();
            await using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                var sample = new TempSample
                {
                    SampleCode = "PENDING-" + Guid.NewGuid().ToString("N")[..16],
                    ExternalBarcode = Clean(vm.ExternalBarcode),
                    DeliveryDateTime = vm.DeliveryDateTime,
                    ReceptionDateTime = vm.ReceptionDateTime,
                    DeliveredByName = vm.DeliveredByName.Trim(),
                    DeliveringOrganization = Clean(vm.DeliveringOrganization),
                    ReceivedByUserId = userId,
                    ReceivedByName = userName,
                    ReceivingOrganization = Clean(vm.ReceivingOrganization),
                    SampleType = vm.SampleType.Trim(),
                    Description = Clean(vm.Description),
                    QuantityReceived = vm.QuantityReceived,
                    CurrentQuantity = vm.QuantityReceived,
                    UnitId = vm.UnitId,
                    ContainersCount = vm.ContainersCount,
                    RequestedAnalysis = Clean(vm.RequestedAnalysis),
                    RequestReferenceNumber = Clean(vm.RequestReferenceNumber),
                    TransportCondition = vm.TransportCondition,
                    ArrivalTemperatureC = vm.ArrivalTemperatureC,
                    ArrivedOutOfRange = IsOutOfRange(vm.TransportCondition, vm.ArrivalTemperatureC),
                    Comments = Clean(vm.Comments),
                    PlannedWithdrawalDate = vm.PlannedWithdrawalDate?.Date,
                    Status = TempSampleStatus.Received
                };
                _db.TempSamples.Add(sample);
                await _db.SaveChangesAsync();                        // → Id

                sample.SampleCode = $"TS-{DateTime.Now:yyyy}-{sample.Id:D6}";
                sample.QrImagePath = GenerateQr(sample.SampleCode, writtenFiles);

                // ---- Event #1: Received ----
                await AppendEventAsync(sample, CustodyEventType.Received, userId, userName,
                    counterparty: sample.DeliveredByName + (sample.DeliveringOrganization is null ? "" : $" ({sample.DeliveringOrganization})"),
                    qtyDelta: sample.QuantityReceived,
                    notes: sample.ArrivedOutOfRange
                        ? $"⚠ Arrival temperature {sample.ArrivalTemperatureC}°C outside {sample.TransportCondition} range"
                        : null,
                    clientInfo: clientInfo);

                // ---- Event #2: Stored (if a slot was picked) ----
                if (hasSlot)
                {
                    sample.BoxId = vm.BoxId;
                    sample.RowIndex = vm.RowIndex;
                    sample.ColumnIndex = vm.ColumnIndex;
                    sample.Status = TempSampleStatus.Stored;
                    await AppendEventAsync(sample, CustodyEventType.Stored, userId, userName,
                        toLocation: locationPath, clientInfo: clientInfo);
                }

                // ---- Attachments ----
                for (int i = 0; i < files.Count; i++)
                {
                    var kind = vm.FileKinds != null && i < vm.FileKinds.Count ? vm.FileKinds[i] : AttachmentKind.Other;
                    var att = await SaveFileAsync(sample, files[i], kind, userName, writtenFiles);
                    await AppendEventAsync(sample, CustodyEventType.AttachmentAdded, userId, userName,
                        notes: $"{kind}: {att.OriginalFileName}",
                        dataJson: $"{{\"sha256\":\"{att.Sha256}\",\"bytes\":{att.SizeBytes}}}",
                        clientInfo: clientInfo);
                }

                await _db.SaveChangesAsync();
                await tx.CommitAsync();
                return sample;
            }
            catch
            {
                await tx.RollbackAsync();
                foreach (var p in writtenFiles) TryDelete(p);
                throw;
            }
        }

        // ══════════════════════════════════════════════════════════════
        //  CUSTODY CHAIN
        // ══════════════════════════════════════════════════════════════
        public async Task<TempSampleCustodyEvent> AppendEventAsync(TempSample sample, CustodyEventType type, int? userId, string userName,
            string? counterparty = null, string? fromLocation = null, string? toLocation = null,
            decimal? qtyDelta = null, string? notes = null, string? dataJson = null, string? clientInfo = null)
        {
            // last link: pending (not yet saved) events first, then the DB
            var pending = _db.ChangeTracker.Entries<TempSampleCustodyEvent>()
                .Where(e => e.State == EntityState.Added && e.Entity.TempSampleId == sample.Id)
                .Select(e => e.Entity)
                .OrderByDescending(e => e.Sequence)
                .FirstOrDefault();

            int lastSeq; string lastHash;
            if (pending != null) { lastSeq = pending.Sequence; lastHash = pending.Hash; }
            else
            {
                var last = await _db.TempSampleCustodyEvents.AsNoTracking()
                    .Where(e => e.TempSampleId == sample.Id)
                    .OrderByDescending(e => e.Sequence)
                    .Select(e => new { e.Sequence, e.Hash })
                    .FirstOrDefaultAsync();
                lastSeq = last?.Sequence ?? 0;
                lastHash = last?.Hash ?? GenesisHash;
            }

            var ev = new TempSampleCustodyEvent
            {
                TempSampleId = sample.Id,
                Sequence = lastSeq + 1,
                EventType = type,
                OccurredAtUtc = DateTime.UtcNow,
                PerformedByUserId = userId,
                PerformedByName = userName,
                Counterparty = counterparty,
                FromLocation = fromLocation,
                ToLocation = toLocation,
                QuantityDelta = qtyDelta,
                QuantityAfter = sample.CurrentQuantity,
                StatusAfter = sample.Status,
                Notes = notes,
                DataJson = dataJson,
                ClientInfo = clientInfo,
                PreviousHash = lastHash
            };
            ev.Hash = ComputeHash(ev, sample.SampleCode);

            _db.TempSampleCustodyEvents.Add(ev);
            return ev;
        }

        public async Task<int?> VerifyChainAsync(int tempSampleId)
        {
            var code = await _db.TempSamples.Where(s => s.Id == tempSampleId).Select(s => s.SampleCode).FirstAsync();
            var events = await _db.TempSampleCustodyEvents.AsNoTracking()
                .Where(e => e.TempSampleId == tempSampleId)
                .OrderBy(e => e.Sequence)
                .ToListAsync();

            var prev = GenesisHash;
            int expectedSeq = 1;
            foreach (var e in events)
            {
                if (e.Sequence != expectedSeq || e.PreviousHash != prev || e.Hash != ComputeHash(e, code))
                    return e.Sequence;
                prev = e.Hash;
                expectedSeq++;
            }
            return null;
        }

        private static string ComputeHash(TempSampleCustodyEvent e, string sampleCode)
        {
            static string D(decimal? d) => d?.ToString("0.###", CultureInfo.InvariantCulture) ?? "";
            var canonical = string.Join("|",
                sampleCode, e.Sequence, e.EventType, e.OccurredAtUtc.Ticks, // Ticks: survives the SQL round-trip (DateTimeKind is lost)
                e.PerformedByUserId, e.PerformedByName, e.Counterparty, e.FromLocation, e.ToLocation,
                D(e.QuantityDelta), D(e.QuantityAfter), e.StatusAfter, e.Notes, e.DataJson, e.PreviousHash);
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
        }

        // ══════════════════════════════════════════════════════════════
        //  QUERIES
        // ══════════════════════════════════════════════════════════════
        public async Task<List<TempSampleListRow>> GetListAsync(TempSampleStatus? status = null, string? search = null)
        {
            var q = _db.TempSamples.AsNoTracking().AsQueryable();
            if (status.HasValue) q = q.Where(s => s.Status == status);
            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim();
                q = q.Where(x => x.SampleCode.Contains(s) || x.SampleType.Contains(s)
                              || (x.RequestReferenceNumber != null && x.RequestReferenceNumber.Contains(s))
                              || (x.ExternalBarcode != null && x.ExternalBarcode.Contains(s))
                              || (x.DeliveringOrganization != null && x.DeliveringOrganization.Contains(s)));
            }

            return await q.OrderByDescending(x => x.ReceptionDateTime)
                .Select(x => new TempSampleListRow
                {
                    Id = x.Id,
                    SampleCode = x.SampleCode,
                    QrImagePath = x.QrImagePath,
                    SampleType = x.SampleType,
                    DeliveringOrganization = x.DeliveringOrganization,
                    RequestReferenceNumber = x.RequestReferenceNumber,
                    CurrentQuantity = x.CurrentQuantity,
                    UnitName = x.Unit != null ? x.Unit.Name : null,
                    Status = x.Status,
                    ReceptionDateTime = x.ReceptionDateTime,
                    PlannedWithdrawalDate = x.PlannedWithdrawalDate,
                    ArrivedOutOfRange = x.ArrivedOutOfRange,
                    LocationPath = x.Box == null ? null :
                        x.Box.Shelf.StorageUnit.Room.Name + " › " + x.Box.Shelf.StorageUnit.Name + " › " +
                        x.Box.Shelf.Name + " › " + x.Box.Name + " [R" + x.RowIndex + "·C" + x.ColumnIndex + "]",
                    EventsCount = x.CustodyEvents.Count
                })
                .ToListAsync();
        }

        public async Task<TempSampleDashboardStats> GetStatsAsync()
        {
            var today = DateTime.Today;
            var weekEnd = today.AddDays(7);
            var monthStart = new DateTime(today.Year, today.Month, 1);
            var active = _db.TempSamples.AsNoTracking()
                .Where(s => s.Status == TempSampleStatus.Received || s.Status == TempSampleStatus.Stored || s.Status == TempSampleStatus.CheckedOut);

            return new TempSampleDashboardStats
            {
                InCustody = await active.CountAsync(),
                AwaitingPlacement = await active.CountAsync(s => s.Status == TempSampleStatus.Received),
                DueThisWeek = await active.CountAsync(s => s.PlannedWithdrawalDate >= today && s.PlannedWithdrawalDate <= weekEnd),
                Overdue = await active.CountAsync(s => s.PlannedWithdrawalDate < today),
                WithdrawnThisMonth = await _db.TempSampleCustodyEvents.AsNoTracking()
                    .CountAsync(e => e.EventType == CustodyEventType.Withdrawn && e.OccurredAtUtc >= monthStart.ToUniversalTime())
            };
        }

        // ══════════════════════════════════════════════════════════════
        //  DOSSIER (Phase 2)
        // ══════════════════════════════════════════════════════════════
        public async Task<TempSampleDossierViewModel?> GetDossierAsync(int id)
        {
            var s = await _db.TempSamples.AsNoTracking()
                .Include(x => x.Unit)
                .Include(x => x.Box!).ThenInclude(b => b.Shelf).ThenInclude(sh => sh.StorageUnit).ThenInclude(u => u.StorageUnitType)
                .Include(x => x.Box!).ThenInclude(b => b.Shelf).ThenInclude(sh => sh.StorageUnit).ThenInclude(u => u.Room)
                .AsSplitQuery()
                .FirstOrDefaultAsync(x => x.Id == id);
            if (s == null) return null;

            var vm = new TempSampleDossierViewModel
            {
                Sample = s,
                Events = await _db.TempSampleCustodyEvents.AsNoTracking().Where(e => e.TempSampleId == id).OrderBy(e => e.Sequence).ToListAsync(),
                Attachments = await _db.TempSampleAttachments.AsNoTracking().Where(a => a.TempSampleId == id).OrderByDescending(a => a.UploadedAtUtc).ToListAsync(),
                RoomName = s.Box?.Shelf?.StorageUnit?.Room?.Name,
                UnitName = s.Box?.Shelf?.StorageUnit?.Name,
                UnitTypeName = s.Box?.Shelf?.StorageUnit?.StorageUnitType?.Name,
                ShelfName = s.Box?.Shelf?.Name,
                BoxName = s.Box?.Name,
                BoxRows = s.Box?.RowsCount ?? 0,
                BoxCols = s.Box?.ColumnsCount ?? 0,
                Edit = TempSampleEditViewModel.From(s)
            };
            vm.BrokenAtSequence = VerifyLoaded(vm.Events, s.SampleCode);
            return vm;
        }

        public async Task<(Stream Stream, string ContentType, string FileName)?> OpenAttachmentAsync(int attachmentId)
        {
            var a = await _db.TempSampleAttachments.AsNoTracking().FirstOrDefaultAsync(x => x.Id == attachmentId);
            if (a == null) return null;
            var full = Path.GetFullPath(Path.Combine(_uploadRoot, a.RelativePath));
            if (!full.StartsWith(Path.GetFullPath(_uploadRoot), StringComparison.OrdinalIgnoreCase) || !File.Exists(full)) return null;
            return (new FileStream(full, FileMode.Open, FileAccess.Read, FileShare.Read), a.ContentType, a.OriginalFileName);
        }

        // ══════════════════════════════════════════════════════════════
        //  CUSTODY ACTIONS
        // ══════════════════════════════════════════════════════════════
        public Task UpdateMetadataAsync(TempSampleEditViewModel vm, CustodyActor actor) => MutateAsync(vm.Id, actor, async s =>
        {
            EnsureNotFinal(s);
            if (vm.ReceptionDateTime < vm.DeliveryDateTime)
                throw new InvalidOperationException("Reception time cannot be earlier than delivery time.");

            var diff = new List<object>();
            void Set<T>(string field, T oldV, T newV, Action<T> apply)
            {
                if (EqualityComparer<T>.Default.Equals(oldV, newV)) return;
                diff.Add(new { field, from = oldV?.ToString(), to = newV?.ToString() });
                apply(newV);
            }

            Set("DeliveryDateTime", s.DeliveryDateTime, vm.DeliveryDateTime, v => s.DeliveryDateTime = v);
            Set("ReceptionDateTime", s.ReceptionDateTime, vm.ReceptionDateTime, v => s.ReceptionDateTime = v);
            Set("DeliveredByName", s.DeliveredByName, vm.DeliveredByName.Trim(), v => s.DeliveredByName = v);
            Set("DeliveringOrganization", s.DeliveringOrganization, Clean(vm.DeliveringOrganization), v => s.DeliveringOrganization = v);
            Set("ReceivingOrganization", s.ReceivingOrganization, Clean(vm.ReceivingOrganization), v => s.ReceivingOrganization = v);
            Set("SampleType", s.SampleType, vm.SampleType.Trim(), v => s.SampleType = v);
            Set("Description", s.Description, Clean(vm.Description), v => s.Description = v);
            Set("ExternalBarcode", s.ExternalBarcode, Clean(vm.ExternalBarcode), v => s.ExternalBarcode = v);
            Set("UnitId", s.UnitId, vm.UnitId, v => s.UnitId = v);
            Set("ContainersCount", s.ContainersCount, vm.ContainersCount, v => s.ContainersCount = v);
            Set("RequestedAnalysis", s.RequestedAnalysis, Clean(vm.RequestedAnalysis), v => s.RequestedAnalysis = v);
            Set("RequestReferenceNumber", s.RequestReferenceNumber, Clean(vm.RequestReferenceNumber), v => s.RequestReferenceNumber = v);
            Set("PlannedWithdrawalDate", s.PlannedWithdrawalDate, vm.PlannedWithdrawalDate?.Date, v => s.PlannedWithdrawalDate = v);
            Set("TransportCondition", s.TransportCondition, vm.TransportCondition, v => s.TransportCondition = v);
            Set("ArrivalTemperatureC", s.ArrivalTemperatureC, vm.ArrivalTemperatureC, v => s.ArrivalTemperatureC = v);
            Set("Comments", s.Comments, Clean(vm.Comments), v => s.Comments = v);

            if (diff.Count == 0) throw new InvalidOperationException("Nothing was changed.");
            s.ArrivedOutOfRange = IsOutOfRange(s.TransportCondition, s.ArrivalTemperatureC);

            await AppendEventAsync(s, CustodyEventType.MetadataUpdated, actor.UserId, actor.Name,
                notes: $"{diff.Count} field(s) corrected — {vm.ChangeReason.Trim()}",
                dataJson: JsonSerializer.Serialize(diff), clientInfo: actor.ClientInfo);
        });

        public Task PlaceAsync(TempSampleActionInput input, CustodyActor actor) => MutateAsync(input.Id, actor, async s =>
        {
            EnsureNotFinal(s);
            if (input.BoxId is null || input.RowIndex is null || input.ColumnIndex is null)
                throw new InvalidOperationException("Choose a box and a free slot.");
            if (s.BoxId == input.BoxId && s.RowIndex == input.RowIndex && s.ColumnIndex == input.ColumnIndex)
                throw new InvalidOperationException("The sample is already in this slot.");

            var to = await ValidateSlotAsync(input.BoxId.Value, input.RowIndex.Value, input.ColumnIndex.Value, s.Id);
            var from = await CurrentPathAsync(s);
            bool first = s.BoxId == null;

            s.BoxId = input.BoxId; s.RowIndex = input.RowIndex; s.ColumnIndex = input.ColumnIndex;
            if (s.Status == TempSampleStatus.Received) s.Status = TempSampleStatus.Stored;

            await AppendEventAsync(s, first ? CustodyEventType.Placed : CustodyEventType.Relocated, actor.UserId, actor.Name,
                fromLocation: from, toLocation: to, notes: Clean(input.Reason ?? input.Notes), clientInfo: actor.ClientInfo);
        });

        public Task CheckOutAsync(TempSampleActionInput input, CustodyActor actor) => MutateAsync(input.Id, actor, async s =>
        {
            if (s.Status != TempSampleStatus.Stored) throw new InvalidOperationException("Only a stored sample can be checked out.");
            if (string.IsNullOrWhiteSpace(input.Counterparty)) throw new InvalidOperationException("Who is taking the sample?");

            s.Status = TempSampleStatus.CheckedOut;          // slot stays reserved for the return
            var notes = Clean(input.Reason);
            if (input.ExpectedReturn.HasValue) notes = $"{notes} (expected back {input.ExpectedReturn:dd MMM yyyy HH:mm})".Trim();
            await AppendEventAsync(s, CustodyEventType.CheckedOut, actor.UserId, actor.Name,
                counterparty: input.Counterparty!.Trim(), fromLocation: await CurrentPathAsync(s), notes: notes, clientInfo: actor.ClientInfo);
        });

        public Task ReturnAsync(TempSampleActionInput input, CustodyActor actor) => MutateAsync(input.Id, actor, async s =>
        {
            if (s.Status != TempSampleStatus.CheckedOut) throw new InvalidOperationException("The sample is not checked out.");
            var consumed = input.Quantity ?? 0;
            if (consumed < 0 || consumed > s.CurrentQuantity) throw new InvalidOperationException($"Consumed quantity must be between 0 and {s.CurrentQuantity:0.###}.");

            s.CurrentQuantity -= consumed;
            s.Status = TempSampleStatus.Stored;
            await AppendEventAsync(s, CustodyEventType.Returned, actor.UserId, actor.Name,
                counterparty: Clean(input.Counterparty), toLocation: await CurrentPathAsync(s),
                qtyDelta: consumed > 0 ? -consumed : null, notes: Clean(input.Notes), clientInfo: actor.ClientInfo);
        });

        public Task WithdrawAsync(TempSampleActionInput input, CustodyActor actor) => MutateAsync(input.Id, actor, async s =>
        {
            EnsureNotFinal(s);
            if (string.IsNullOrWhiteSpace(input.Counterparty)) throw new InvalidOperationException("Recipient (handed to) is required.");

            var qty = input.FullWithdrawal ? s.CurrentQuantity : (input.Quantity ?? 0);
            if (qty <= 0 || qty > s.CurrentQuantity) throw new InvalidOperationException($"Quantity must be between 0 and {s.CurrentQuantity:0.###}.");
            bool full = qty == s.CurrentQuantity;

            var from = await CurrentPathAsync(s);
            var sig = await SaveSignatureAsync(s, input.SignatureDataUrl, actor.Name);

            s.CurrentQuantity -= qty;
            if (full) ReleaseSlot(s, TempSampleStatus.Withdrawn);

            await AppendEventAsync(s, full ? CustodyEventType.Withdrawn : CustodyEventType.PartialWithdrawal, actor.UserId, actor.Name,
                counterparty: input.Counterparty!.Trim(), fromLocation: from, qtyDelta: -qty,
                notes: Clean(input.Reason), dataJson: sig, clientInfo: actor.ClientInfo);
        });

        public Task DisposeAsync(TempSampleActionInput input, CustodyActor actor) => MutateAsync(input.Id, actor, async s =>
        {
            EnsureNotFinal(s);
            if (string.IsNullOrWhiteSpace(input.Method)) throw new InvalidOperationException("Disposal method is required.");
            if (string.IsNullOrWhiteSpace(input.Reason)) throw new InvalidOperationException("Disposal reason is required.");

            var from = await CurrentPathAsync(s);
            var sig = await SaveSignatureAsync(s, input.SignatureDataUrl, actor.Name);
            var qty = s.CurrentQuantity;
            s.CurrentQuantity = 0;
            ReleaseSlot(s, TempSampleStatus.Disposed);

            await AppendEventAsync(s, CustodyEventType.Disposed, actor.UserId, actor.Name,
                counterparty: Clean(input.Counterparty) is string w ? "Witness: " + w : null,
                fromLocation: from, qtyDelta: qty > 0 ? -qty : null,
                notes: $"{input.Method!.Trim()} — {input.Reason!.Trim()}", dataJson: sig, clientInfo: actor.ClientInfo);
        });

        public Task AddNoteAsync(TempSampleActionInput input, CustodyActor actor) => MutateAsync(input.Id, actor, async s =>
        {
            if (string.IsNullOrWhiteSpace(input.Notes)) throw new InvalidOperationException("The note is empty.");
            await AppendEventAsync(s, CustodyEventType.Note, actor.UserId, actor.Name, notes: input.Notes!.Trim(), clientInfo: actor.ClientInfo);
        });

        public async Task AddAttachmentsAsync(int id, List<IFormFile> files, List<AttachmentKind>? kinds, CustodyActor actor)
        {
            files = files?.Where(f => f is { Length: > 0 }).ToList() ?? new();
            if (files.Count == 0) throw new InvalidOperationException("No file selected.");
            foreach (var f in files)
            {
                if (DeniedExtensions.Contains(Path.GetExtension(f.FileName))) throw new InvalidOperationException($"File type '{Path.GetExtension(f.FileName)}' is not allowed.");
                if (f.Length > MaxFileBytes) throw new InvalidOperationException($"'{f.FileName}' exceeds the 25 MB limit.");
            }

            var written = new List<string>();
            try
            {
                await MutateAsync(id, actor, async s =>
                {
                    for (int i = 0; i < files.Count; i++)
                    {
                        var kind = kinds != null && i < kinds.Count ? kinds[i] : AttachmentKind.Other;
                        var att = await SaveFileAsync(s, files[i], kind, actor.Name, written);
                        await AppendEventAsync(s, CustodyEventType.AttachmentAdded, actor.UserId, actor.Name,
                            notes: $"{kind}: {att.OriginalFileName}",
                            dataJson: JsonSerializer.Serialize(new { sha256 = att.Sha256, bytes = att.SizeBytes }), clientInfo: actor.ClientInfo);
                    }
                });
            }
            catch
            {
                foreach (var p in written) TryDelete(p);
                throw;
            }
        }

        // ───────── mutation pipeline: load tracked → act → save, all in one transaction ─────────
        private async Task MutateAsync(int id, CustodyActor actor, Func<TempSample, Task> action)
        {
            await using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                var s = await _db.TempSamples.FirstOrDefaultAsync(x => x.Id == id)
                        ?? throw new InvalidOperationException("Sample not found.");
                await action(s);
                s.UpdatedAtUtc = DateTime.UtcNow;
                await _db.SaveChangesAsync();
                await tx.CommitAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                await tx.RollbackAsync();
                throw new InvalidOperationException("Someone else changed this sample a moment ago. Reload and try again.");
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
            finally
            {
                _db.ChangeTracker.Clear();
            }
        }

        private static void EnsureNotFinal(TempSample s)
        {
            if (s.Status is TempSampleStatus.Withdrawn or TempSampleStatus.Disposed)
                throw new InvalidOperationException($"The sample is {s.Status.ToString().ToLower()} — its record is closed.");
        }

        private static void ReleaseSlot(TempSample s, TempSampleStatus final)
        {
            s.BoxId = null; s.RowIndex = null; s.ColumnIndex = null;
            s.Status = final;
        }

        private async Task<string?> CurrentPathAsync(TempSample s)
        {
            if (s.BoxId == null) return null;
            var b = await _db.Boxes.AsNoTracking()
                .Where(x => x.Id == s.BoxId)
                .Select(x => new { Room = x.Shelf.StorageUnit.Room.Name, Unit = x.Shelf.StorageUnit.Name, Shelf = x.Shelf.Name, Box = x.Name })
                .FirstOrDefaultAsync();
            return b == null ? null : $"{b.Room} › {b.Unit} › {b.Shelf} › {b.Box} [R{s.RowIndex}·C{s.ColumnIndex}]";
        }

        /// <summary>Stores an on-screen signature as a Signature attachment; returns JSON for the event payload.</summary>
        private async Task<string?> SaveSignatureAsync(TempSample s, string? dataUrl, string userName)
        {
            if (string.IsNullOrWhiteSpace(dataUrl)) return null;
            const string prefix = "data:image/png;base64,";
            if (!dataUrl.StartsWith(prefix)) throw new InvalidOperationException("Invalid signature image.");
            var bytes = Convert.FromBase64String(dataUrl[prefix.Length..]);
            if (bytes.Length > 1_000_000) throw new InvalidOperationException("Signature image is too large.");

            var folder = Path.Combine(_uploadRoot, s.Id.ToString());
            Directory.CreateDirectory(folder);
            var stored = $"sig-{Guid.NewGuid():N}.png";
            await File.WriteAllBytesAsync(Path.Combine(folder, stored), bytes);
            var sha = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

            _db.TempSampleAttachments.Add(new TempSampleAttachment
            {
                TempSampleId = s.Id,
                Kind = AttachmentKind.Signature,
                OriginalFileName = $"signature-{DateTime.Now:yyyyMMdd-HHmm}.png",
                RelativePath = $"{s.Id}/{stored}",
                ContentType = "image/png",
                SizeBytes = bytes.Length,
                Sha256 = sha,
                UploadedBy = userName
            });
            return JsonSerializer.Serialize(new { signatureSha256 = sha });
        }

        private static int? VerifyLoaded(List<TempSampleCustodyEvent> events, string code)
        {
            var prev = GenesisHash; int expected = 1;
            foreach (var e in events)
            {
                if (e.Sequence != expected || e.PreviousHash != prev || e.Hash != ComputeHash(e, code)) return e.Sequence;
                prev = e.Hash; expected++;
            }
            return null;
        }

        // ══════════════════════════════════════════════════════════════
        //  HELPERS
        // ══════════════════════════════════════════════════════════════
        private async Task<string> ValidateSlotAsync(int boxId, int row, int col, int? excludeSampleId = null)
        {
            var box = await _db.Boxes.AsNoTracking()
                .Include(b => b.Shelf).ThenInclude(s => s.StorageUnit).ThenInclude(u => u.Room)
                .FirstOrDefaultAsync(b => b.Id == boxId)
                ?? throw new InvalidOperationException("Selected box not found.");

            if (box.Shelf.StorageUnit.Zone != StorageZone.TemporaryHolding)
                throw new InvalidOperationException("This box belongs to the Lots inventory zone. Choose a Temporary Holding unit.");

            if (row < 1 || col < 1 || row > box.RowsCount || col > box.ColumnsCount)
                throw new InvalidOperationException("Selected slot is outside the box grid.");

            bool taken = await _db.TempSamples.AnyAsync(s => s.BoxId == boxId && s.RowIndex == row && s.ColumnIndex == col && s.Id != excludeSampleId)
                      || await _db.Lots.AnyAsync(l => l.BoxId == boxId && l.RowIndex == row && l.ColumnIndex == col);
            if (taken) throw new InvalidOperationException("This slot was just taken by someone else. Pick another slot.");

            return $"{box.Shelf.StorageUnit.Room.Name} › {box.Shelf.StorageUnit.Name} › {box.Shelf.Name} › {box.Name} [R{row}·C{col}]";
        }

        /// <summary>Accepted arrival range per transport condition (°C). Null = not checked.</summary>
        public static (decimal Min, decimal Max)? RangeFor(TransportCondition c) => c switch
        {
            TransportCondition.Ambient => (15m, 25m),
            TransportCondition.Refrigerated => (2m, 8m),
            TransportCondition.Frozen => (-25m, -15m),
            TransportCondition.DeepFrozen => (-90m, -60m),
            TransportCondition.DryIce => (-90m, -60m),
            TransportCondition.LiquidNitrogen => (-200m, -135m),
            _ => null
        };

        private static bool IsOutOfRange(TransportCondition c, decimal? t)
        {
            var r = RangeFor(c);
            return t.HasValue && r.HasValue && (t < r.Value.Min || t > r.Value.Max);
        }

        private string GenerateQr(string code, List<string> written)
        {
            using var gen = new QRCodeGenerator();
            using var data = gen.CreateQrCode(code, QRCodeGenerator.ECCLevel.Q);
            var png = new PngByteQRCode(data).GetGraphic(10);
            var fileName = code + ".png";
            var full = Path.Combine(_qrRoot, fileName);
            File.WriteAllBytes(full, png);
            written.Add(full);
            return "/tempsample-qr/" + fileName;
        }

        private async Task<TempSampleAttachment> SaveFileAsync(TempSample sample, IFormFile file, AttachmentKind kind, string userName, List<string> written)
        {
            var folder = Path.Combine(_uploadRoot, sample.Id.ToString());
            Directory.CreateDirectory(folder);
            var stored = $"{Guid.NewGuid():N}{Path.GetExtension(file.FileName)}";
            var full = Path.Combine(folder, stored);

            string sha;
            await using (var fs = new FileStream(full, FileMode.CreateNew))
            {
                await file.CopyToAsync(fs);
            }
            written.Add(full);
            await using (var rs = File.OpenRead(full))
                sha = Convert.ToHexString(await SHA256.HashDataAsync(rs)).ToLowerInvariant();

            var att = new TempSampleAttachment
            {
                TempSampleId = sample.Id,
                Kind = kind,
                OriginalFileName = Path.GetFileName(file.FileName),
                RelativePath = $"{sample.Id}/{stored}",
                ContentType = string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType,
                SizeBytes = file.Length,
                Sha256 = sha,
                UploadedBy = userName
            };
            _db.TempSampleAttachments.Add(att);
            return att;
        }

        private void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch (Exception ex) { _logger.LogWarning(ex, "Could not delete orphan file {Path}", path); }
        }

        private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
    }
}
