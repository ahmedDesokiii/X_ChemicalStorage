using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using X_ChemicalStorage.Data;
using X_ChemicalStorage.IRepository;
using X_ChemicalStorage.Models;
using X_ChemicalStorage.Models.TempSamples;
using X_ChemicalStorage.ViewModels.TempSamples;

namespace X_ChemicalStorage.Controllers
{
    [Authorize]   // swap for Permissions.TempSamples.* once those constants exist
    public class TempSamplesController : Controller
    {
        private readonly ITempSampleService _service;
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<TempSamplesController> _logger;

        public TempSamplesController(ITempSampleService service, ApplicationDbContext context,
            UserManager<ApplicationUser> userManager, ILogger<TempSamplesController> logger)
        {
            _service = service;
            _context = context;
            _userManager = userManager;
            _logger = logger;
        }

        // ═════════════════════════ Register ═════════════════════════
        [HttpGet]
        public async Task<IActionResult> Index(TempSampleStatus? status, string? q)
        {
            // Exact scan of a sample code / QR → jump straight to its dossier
            if (!string.IsNullOrWhiteSpace(q))
            {
                var exact = await _context.TempSamples.AsNoTracking()
                    .Where(s => s.SampleCode == q.Trim() || s.ExternalBarcode == q.Trim())
                    .Select(s => (int?)s.Id).FirstOrDefaultAsync();
                if (exact != null) return RedirectToAction(nameof(Details), new { id = exact });
            }

            var vm = new TempSampleIndexViewModel
            {
                Stats = await _service.GetStatsAsync(),
                Rows = await _service.GetListAsync(status, q),
                Filter = status,
                Search = q
            };
            return View(vm);
        }

        // ═════════════════════════ Reception ═════════════════════════
        [HttpGet]
        public async Task<IActionResult> Receive()
        {
            var vm = new TempSampleReceiveViewModel();
            await FillReceiveListsAsync(vm);
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(120L * 1024 * 1024)]
        public async Task<IActionResult> Receive(TempSampleReceiveViewModel vm)
        {
            if (!ModelState.IsValid)
            {
                await FillReceiveListsAsync(vm);
                TempData["Error"] = "Please complete the required fields.";
                return View(vm);
            }

            try
            {
                var actor = await ActorAsync();
                var sample = await _service.ReceiveAsync(vm, actor.UserId ?? 0, actor.Name, actor.ClientInfo);

                TempData["Success"] = $"Sample {sample.SampleCode} received successfully.";
                TempData["NewSampleId"] = sample.Id;
                TempData["NewSampleCode"] = sample.SampleCode;
                TempData["NewSampleQr"] = sample.QrImagePath;
                return RedirectToAction(nameof(Index));
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                TempData["Error"] = ex.Message;
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Receive temp sample failed (DB)");
                TempData["Error"] = "The selected slot is no longer available, or a database constraint failed.";
            }

            await FillReceiveListsAsync(vm);
            return View(vm);
        }

        // ═════════════════════════ Dossier ═════════════════════════
        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var vm = await _service.GetDossierAsync(id);
            if (vm == null) return NotFound();

            vm.ListHoldingRooms = await HoldingRoomsAsync();
            vm.ListUnits = await UnitsAsync();
            return View(vm);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(TempSampleEditViewModel vm)
        {
            if (!ModelState.IsValid)
            {
                TempData["Error"] = string.Join(" ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).Distinct());
                return RedirectToAction(nameof(Details), new { id = vm.Id });
            }
            return await Run(vm.Id, a => _service.UpdateMetadataAsync(vm, a), "Metadata updated — the change is recorded in the custody chain.");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public Task<IActionResult> Place(TempSampleActionInput input) =>
            Run(input.Id, a => _service.PlaceAsync(input, a), "Sample placed / relocated.");

        [HttpPost, ValidateAntiForgeryToken]
        public Task<IActionResult> CheckOut(TempSampleActionInput input) =>
            Run(input.Id, a => _service.CheckOutAsync(input, a), "Sample checked out — its slot stays reserved.");

        [HttpPost, ValidateAntiForgeryToken]
        public Task<IActionResult> Return(TempSampleActionInput input) =>
            Run(input.Id, a => _service.ReturnAsync(input, a), "Sample returned to storage.");

        [HttpPost, ValidateAntiForgeryToken]
        [RequestSizeLimit(5L * 1024 * 1024)]
        public Task<IActionResult> Withdraw(TempSampleActionInput input) =>
            Run(input.Id, a => _service.WithdrawAsync(input, a), "Withdrawal recorded.");

        [HttpPost, ValidateAntiForgeryToken]
        [RequestSizeLimit(5L * 1024 * 1024)]
        public Task<IActionResult> DisposeSample(TempSampleActionInput input) =>
            Run(input.Id, a => _service.DisposeAsync(input, a), "Disposal recorded — the record is now closed.");

        [HttpPost, ValidateAntiForgeryToken]
        public Task<IActionResult> Note(TempSampleActionInput input) =>
            Run(input.Id, a => _service.AddNoteAsync(input, a), "Note added to the custody chain.");

        [HttpPost, ValidateAntiForgeryToken]
        [RequestSizeLimit(120L * 1024 * 1024)]
        public Task<IActionResult> Upload(int id, List<IFormFile> files, List<AttachmentKind>? fileKinds) =>
            Run(id, a => _service.AddAttachmentsAsync(id, files, fileKinds, a), "Document(s) attached.");

        [HttpGet]
        public async Task<IActionResult> Attachment(int id, bool inline = false)
        {
            var f = await _service.OpenAttachmentAsync(id);
            if (f == null) return NotFound();
            return inline ? File(f.Value.Stream, f.Value.ContentType) : File(f.Value.Stream, f.Value.ContentType, f.Value.FileName);
        }

        // ═════════════════════════ Printing / integrity ═════════════════════════
        [HttpGet]
        public async Task<IActionResult> PrintLabel(int id)
        {
            var s = await _context.TempSamples.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
            if (s == null) return NotFound();
            return View(s);
        }

        /// <summary>Printable Chain-of-Custody certificate with every sealed event.</summary>
        [HttpGet]
        public async Task<IActionResult> CustodyReport(int id)
        {
            var vm = await _service.GetDossierAsync(id);
            if (vm == null) return NotFound();
            return View(vm);
        }

        [HttpGet]
        public async Task<IActionResult> VerifyChain(int id)
        {
            var broken = await _service.VerifyChainAsync(id);
            return Json(new { intact = broken == null, brokenAt = broken });
        }

        // ═════════════════════════ Lookups ═════════════════════════
        [HttpGet]
        public async Task<JsonResult> GetHoldingUnitsByRoom(int roomId)
        {
            var units = await _context.StorageUnits.AsNoTracking()
                .Where(u => u.RoomId == roomId && u.Zone == StorageZone.TemporaryHolding)
                .OrderBy(u => u.Name)
                .Select(u => new { u.Id, u.Name, type = u.StorageUnitType.Name })
                .ToListAsync();
            return Json(units);
        }

        // ═════════════════════════ Helpers ═════════════════════════
        private async Task<IActionResult> Run(int id, Func<CustodyActor, Task> action, string ok)
        {
            try
            {
                await action(await ActorAsync());
                TempData["Success"] = ok;
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = ex.Message;
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Temp sample action failed for {Id}", id);
                TempData["Error"] = "That slot was just taken, or a database constraint failed. Reload and try again.";
            }
            return RedirectToAction(nameof(Details), new { id });
        }

        private async Task<CustodyActor> ActorAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            var ua = Request.Headers.UserAgent.ToString();
            var client = $"{HttpContext.Connection.RemoteIpAddress} | {ua[..Math.Min(60, ua.Length)]}";
            return new CustodyActor(user?.Id, user?.UserName ?? User.Identity?.Name ?? "unknown", client);
        }

        private Task<List<SelectListItem>> HoldingRoomsAsync() =>
            _context.Rooms.AsNoTracking()
                .Where(r => r.StorageUnits.Any(u => u.Zone == StorageZone.TemporaryHolding))
                .OrderBy(r => r.Name)
                .Select(r => new SelectListItem { Value = r.Id.ToString(), Text = r.Name })
                .ToListAsync();

        private Task<List<SelectListItem>> UnitsAsync() =>
            _context.Units.AsNoTracking()
                .Where(u => u.CurrentState > 0)
                .Select(u => new SelectListItem { Value = u.Id.ToString(), Text = u.Name })
                .ToListAsync();

        private async Task FillReceiveListsAsync(TempSampleReceiveViewModel vm)
        {
            vm.ListRooms = await HoldingRoomsAsync();
            vm.ListUnits = await UnitsAsync();

            var used = await _context.TempSamples.AsNoTracking().Select(s => s.SampleType).Distinct().Take(30).ToListAsync();
            vm.SampleTypeSuggestions = used
                .Union(new[] { "Whole Blood", "Serum", "Plasma", "Urine", "Swab", "Tissue", "DNA/RNA Extract", "Culture", "Chemical Reagent", "Water", "Food" })
                .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }
    }
}
