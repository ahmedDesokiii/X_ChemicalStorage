using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using X_ChemicalStorage.BioBank.Services;

namespace X_ChemicalStorage.Controllers
{
    /// Settings → Backup: whole application + Bio Bank data, automatic and manual, upload, download, restore.
    [Authorize]
    public class BackupCenterController : Controller
    {
        private readonly BackupCenterService _center;
        private readonly IBackupService _full;
        private readonly ILogger<BackupCenterController> _log;

        public BackupCenterController(BackupCenterService center, IBackupService full, ILogger<BackupCenterController> log)
        { _center = center; _full = full; _log = log; }

        private string Who() => User?.Identity?.Name is { Length: > 0 } n ? n : "unknown";

        [HasPermission(Permissions.Backup.View)]
        [HttpGet("biobank/backup-settings")]
        public IActionResult Index() => View("~/Views/SiteSettings/Backup.cshtml");

        [HasPermission(Permissions.Backup.View)]
        [HttpGet("biobank/api/backup/center")]
        public async Task<IActionResult> Overview() => Ok(await _center.OverviewAsync());

        [HasPermission(Permissions.Backup.Run)]
        [HttpPost("biobank/api/backup/{scope}/run")]
        public async Task<IActionResult> Run(string scope)
        {
            if (!BackupCenterService.IsScope(scope)) return NotFound();
            var r = scope == BackupCenterService.Full
                ? await _full.RunAsync("Manual", Who(), CancellationToken.None)
                : await _center.RunBioBankAsync("Manual", Who(), CancellationToken.None);
            if (r.Success) return Ok(r);
            return r.Code is "busy" or "cooldown" ? Conflict(new { message = r.Message, code = r.Code }) : StatusCode(500, new { message = r.Message, code = r.Code });
        }

        [HasPermission(Permissions.Backup.View)]
        [HttpGet("biobank/api/backup/{scope}/download")]
        public IActionResult Download(string scope, [FromQuery] string file)
        {
            if (!BackupCenterService.IsScope(scope)) return NotFound();
            var path = _center.ResolveFile(scope, file);
            if (path == null) return NotFound(new { message = "Backup file not found." });
            _log.LogInformation("Backup {File} downloaded by {User}", file, Who());
            return PhysicalFile(path, "application/octet-stream", Path.GetFileName(path), enableRangeProcessing: true);
        }

        [HasPermission(Permissions.Backup.Restore)]
        [HttpPost("biobank/api/backup/{scope}/upload")]
        [RequestSizeLimit(long.MaxValue)]
        [RequestFormLimits(MultipartBodyLengthLimit = long.MaxValue, ValueLengthLimit = int.MaxValue)]
        public async Task<IActionResult> Upload(string scope, IFormFile? file)
        {
            if (!BackupCenterService.IsScope(scope)) return NotFound();
            if (file == null) return BadRequest(new { message = "Choose a backup file first." });
            var (ok, msg, name) = await _center.SaveUploadAsync(scope, file, Who(), HttpContext.RequestAborted);
            return ok ? Ok(new { message = msg, fileName = name }) : BadRequest(new { message = msg });
        }

        public class RestoreRequest
        {
            public string? File { get; set; }
            public string? Confirm { get; set; }
        }

        [HasPermission(Permissions.Backup.Restore)]
        [HttpPost("biobank/api/backup/{scope}/restore")]
        public async Task<IActionResult> Restore(string scope, [FromBody] RestoreRequest req)
        {
            if (!BackupCenterService.IsScope(scope)) return NotFound();
            if (req == null || string.IsNullOrWhiteSpace(req.File)) return BadRequest(new { message = "Choose a backup first." });
            if (!string.Equals(req.Confirm?.Trim(), "RESTORE", StringComparison.Ordinal))
                return BadRequest(new { message = "Type RESTORE to confirm." });

            // never cancelled by the browser: a half-finished restore is worse than a finished one
            var (ok, msg, details) = await _center.RestoreAsync(scope, req.File!, Who(), CancellationToken.None);
            return ok ? Ok(new { message = msg, details }) : StatusCode(409, new { message = msg });
        }
    }
}
