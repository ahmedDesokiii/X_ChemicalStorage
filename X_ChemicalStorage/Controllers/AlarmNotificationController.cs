using System.ComponentModel.DataAnnotations;
using System.Net.Mail;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using X_ChemicalStorage.BioBank.Services;
using X_ChemicalStorage.Data;
using X_ChemicalStorage.Models;

namespace X_ChemicalStorage.Controllers
{
    public class AlarmContactDto
    {
        [Required, MaxLength(100)] public string Name { get; set; } = null!;
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? TelegramChatId { get; set; }
        public int Tier { get; set; } = 1;
        public string? FreezerId { get; set; }
        public bool IsActive { get; set; } = true;
    }

    [Authorize]
    [Route("biobank/api")]
    public class AlarmNotificationController : Controller
    {
        private readonly ApplicationDbContext _db;
        public AlarmNotificationController(ApplicationDbContext db) => _db = db;

        // ---------------------------------------------------------------- contacts
        [HttpGet("alarm-contacts")]
        public async Task<IActionResult> GetContacts()
            => Ok((await _db.AlarmContacts.OrderBy(c => c.Tier).ThenBy(c => c.Name).ToListAsync()));

        [HttpPost("alarm-contacts")]
        public async Task<IActionResult> Create([FromBody] AlarmContactDto dto)
        {
            var c = new Bio_AlarmContact();
            var err = Apply(c, dto);
            if (err != null) return BadRequest(new { message = err });
            _db.AlarmContacts.Add(c);
            await _db.SaveChangesAsync();
            return Ok(c);
        }

        [HttpPut("alarm-contacts/{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] AlarmContactDto dto)
        {
            var c = await _db.AlarmContacts.FirstOrDefaultAsync(x => x.Id == id);
            if (c == null) return NotFound(new { message = "Contact not found." });
            var err = Apply(c, dto);
            if (err != null) return BadRequest(new { message = err });
            await _db.SaveChangesAsync();
            return Ok(c);
        }

        [HttpDelete("alarm-contacts/{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var c = await _db.AlarmContacts.FirstOrDefaultAsync(x => x.Id == id);
            if (c == null) return NotFound(new { message = "Contact not found." });
            _db.AlarmContacts.Remove(c);
            await _db.SaveChangesAsync();
            return Ok(new { message = "Deleted" });
        }

        /// يبعت رسالة اختبار فعلية لجهة الاتصال على كل قنواتها المفعّلة، ويرجّع النتيجة.
        [HttpPost("alarm-contacts/{id:int}/test")]
        public async Task<IActionResult> Test(int id, [FromServices] NotificationDispatcher dispatcher,
            [FromServices] IEnumerable<INotificationChannel> channels)
        {
            var c = await _db.AlarmContacts.FirstOrDefaultAsync(x => x.Id == id);
            if (c == null) return NotFound(new { message = "Contact not found." });

            var now = DateTime.UtcNow;
            var rows = new List<Bio_AlarmNotification>();
            foreach (var (channel, address) in OutboxAlarmNotifier.Targets(c))
            {
                var ch = channels.FirstOrDefault(x => x.Name == channel);
                if (ch == null || !ch.IsEnabled) continue;
                var row = new Bio_AlarmNotification
                {
                    AlarmId = 0,
                    FreezerId = "-",
                    ContactId = c.Id,
                    ContactName = c.Name,
                    Channel = channel,
                    Kind = "Test",
                    Recipient = address,
                    Subject = "[BioBank] Test message",
                    Message = "[BioBank] Test message - if you received this, alarm notifications work.",
                    Status = Bio_NotificationStatus.Pending,
                    CreatedAt = now,
                    NextAttemptAt = now
                };
                _db.AlarmNotifications.Add(row);
                rows.Add(row);
            }

            if (rows.Count == 0)
                return BadRequest(new { message = "No enabled channel matches this contact's details (check Notifications settings)." });

            await _db.SaveChangesAsync();
            await dispatcher.ProcessPendingAsync(DateTime.UtcNow);

            return Ok(rows.Select(r => new { r.Channel, r.Recipient, r.Status, r.Error }));
        }

        // ---------------------------------------------------------------- log
        [HttpGet("notifications")]
        public async Task<IActionResult> Log([FromQuery] string? freezerId, [FromQuery] DateTime? from,
            [FromQuery] DateTime? to, [FromQuery] int take = 200)
        {
            take = Math.Clamp(take, 1, 1000);
            var q = _db.AlarmNotifications.AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(freezerId)) q = q.Where(n => n.FreezerId == freezerId);
            if (from.HasValue) q = q.Where(n => n.CreatedAt >= from.Value);
            if (to.HasValue) q = q.Where(n => n.CreatedAt <= to.Value);
            return Ok(await q.OrderByDescending(n => n.Id).Take(take).ToListAsync());
        }

        // ---------------------------------------------------------------- helpers
        private static string? Apply(Bio_AlarmContact c, AlarmContactDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Name)) return "Name is required.";
            if (dto.Tier != 1 && dto.Tier != 2) return "Tier must be 1 or 2.";

            string? phone = null;
            if (!string.IsNullOrWhiteSpace(dto.Phone))
            {
                phone = PhoneNormalizer.Normalize(dto.Phone);
                if (phone == null) return "Phone number is not valid (example: 01012345678 or +201012345678).";
            }

            string? email = null;
            if (!string.IsNullOrWhiteSpace(dto.Email))
            {
                try { email = new MailAddress(dto.Email.Trim()).Address; }
                catch { return "Email is not valid."; }
            }

            var chat = string.IsNullOrWhiteSpace(dto.TelegramChatId) ? null : dto.TelegramChatId.Trim();
            if (phone == null && email == null && chat == null)
                return "At least one of Phone, Email or TelegramChatId is required.";

            c.Name = dto.Name.Trim();
            c.Phone = phone;
            c.Email = email;
            c.TelegramChatId = chat;
            c.Tier = dto.Tier;
            c.FreezerId = string.IsNullOrWhiteSpace(dto.FreezerId) ? null : dto.FreezerId.Trim();
            c.IsActive = dto.IsActive;
            return null;
        }
    }
}