using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using X_ChemicalStorage.BioBank.Services;

namespace X_ChemicalStorage.Controllers
{
    [Authorize]
    [Route("biobank/api/backup")]
    public class BackupController : Controller
    {
        private readonly IBackupService _svc;
        public BackupController(IBackupService svc) => _svc = svc;

        private string CurrentUser() =>
            User?.Identity?.IsAuthenticated == true && !string.IsNullOrWhiteSpace(User.Identity!.Name) ? User.Identity.Name! : "unknown";

        [HttpGet("status")]
        public async Task<IActionResult> Status() => Ok(await _svc.GetStatusAsync());

        /// نسخة يدوية (الزر جنب Add Freezer)
        [HttpPost("run")]
        public async Task<IActionResult> Run()
        {
            var r = await _svc.RunAsync("Manual", CurrentUser(), CancellationToken.None);   // مش بنلغي النسخة لو المستخدم قفل الصفحة
            if (r.Success) return Ok(r);
            return r.Code switch
            {
                "busy" or "cooldown" => Conflict(new { message = r.Message, code = r.Code }),
                _ => StatusCode(500, new { message = r.Message, code = r.Code })
            };
        }
    }
}