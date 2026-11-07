using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using X_ChemicalStorage.BioBank.DTOs;
using X_ChemicalStorage.BioBank.Services;

namespace X_ChemicalStorage.Controllers
{
    [Authorize]
    [Route("biobank/api")]
    public class IoTDashboardController : Controller
    {
        private readonly IIoTDashboardService _svc;
        public IoTDashboardController(IIoTDashboardService svc) => _svc = svc;

        private string CurrentUser() =>
            User?.Identity?.IsAuthenticated == true && !string.IsNullOrWhiteSpace(User.Identity!.Name)
                ? User.Identity.Name! : "unknown";

        /// عدّادات + اتجاه الإنذارات + أكتر 5 فريزرات + حالة كل فريزر (في طلب واحد)
        [HasPermission(Permissions.IoT.View)]
        [HttpGet("iot/dashboard")]
        public async Task<IActionResult> Dashboard([FromQuery] int days = 7)
            => Ok(await _svc.GetDashboardAsync(days));

        [HasPermission(Permissions.IoT.View)]
        [HttpGet("freezers/{id}/settings")]
        public async Task<IActionResult> GetSettings(string id)
        {
            var s = await _svc.GetSettingsAsync(id);
            return s == null ? NotFound(new { message = "Freezer not found." }) : Ok(s);
        }

        [HasPermission(Permissions.IoT.ManageSettings)]
        [HttpPut("freezers/{id}/settings")]
        public async Task<IActionResult> UpdateSettings(string id, [FromBody] UpdateFreezerSettingsDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(new { message = "All settings fields are required." });
            try
            {
                return Ok(await _svc.UpdateSettingsAsync(id, dto, CurrentUser()));
            }
            catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
            catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        }

        [HasPermission(Permissions.IoT.View)]
        [HttpGet("freezers/{id}/settings/audit")]
        public async Task<IActionResult> Audit(string id, [FromQuery] int take = 100)
            => Ok(await _svc.GetSettingsAuditAsync(id, take));
    }
}