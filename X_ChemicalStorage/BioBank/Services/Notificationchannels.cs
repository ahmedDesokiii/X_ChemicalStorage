using System.Net.Mail;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.Extensions.Options;
using X_ChemicalStorage.BioBank.Options;
using X_ChemicalStorage.Models;

namespace X_ChemicalStorage.BioBank.Services
{
    internal static class Str
    {
        public static string Trunc(string? s, int max) =>
            string.IsNullOrEmpty(s) ? "" : (s.Length <= max ? s : s.Substring(0, max));
    }

    // ============================================================ SMS عبر HTTP
    public class SmsHttpChannel : INotificationChannel
    {
        private readonly IHttpClientFactory _http;
        private readonly NotificationOptions _o;

        public SmsHttpChannel(IHttpClientFactory http, IOptions<NotificationOptions> o)
        {
            _http = http;
            _o = o.Value;
        }

        public string Name => Bio_NotificationChannels.Sms;
        public bool IsEnabled => _o.Sms.Enabled && !string.IsNullOrWhiteSpace(_o.Sms.Url);

        public async Task SendAsync(string recipient, string subject, string body, CancellationToken ct)
        {
            var o = _o.Sms;
            var isForm = string.Equals(o.BodyFormat, "form", StringComparison.OrdinalIgnoreCase);
            var method = new HttpMethod(string.IsNullOrWhiteSpace(o.Method) ? "POST" : o.Method.ToUpperInvariant());

            var url = Fill(o.Url, true, recipient, body);
            using var req = new HttpRequestMessage(method, url);
            foreach (var h in o.Headers)
                req.Headers.TryAddWithoutValidation(h.Key, h.Value);

            if (method != HttpMethod.Get)
            {
                var payload = Fill(o.BodyTemplate, isForm, recipient, body);
                req.Content = new StringContent(payload, Encoding.UTF8,
                    isForm ? "application/x-www-form-urlencoded" : "application/json");
            }

            var client = _http.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(Math.Max(3, o.TimeoutSeconds));
            using var resp = await client.SendAsync(req, ct);
            var text = await resp.Content.ReadAsStringAsync(ct);

            if (!resp.IsSuccessStatusCode)
                throw new InvalidOperationException($"SMS gateway HTTP {(int)resp.StatusCode}: {Str.Trunc(text, 200)}");

            if (!string.IsNullOrEmpty(o.SuccessContains) &&
                text.IndexOf(o.SuccessContains, StringComparison.OrdinalIgnoreCase) < 0)
                throw new InvalidOperationException($"SMS gateway rejected: {Str.Trunc(text, 200)}");
        }

        /// {to}/{message} بتتعوض بقيمة متهربة حسب نوع الجسم (json أو form/url).
        public static string Fill(string template, bool urlEncode, string to, string message)
        {
            string Esc(string v) => urlEncode
                ? Uri.EscapeDataString(v)
                : JsonEncodedText.Encode(v, JavaScriptEncoder.UnsafeRelaxedJsonEscaping).ToString();
            return (template ?? "").Replace("{to}", Esc(to)).Replace("{message}", Esc(message));
        }
    }

    // ============================================================ Email عبر SMTP
    public class EmailSmtpChannel : INotificationChannel
    {
        private readonly NotificationOptions _o;
        public EmailSmtpChannel(IOptions<NotificationOptions> o) => _o = o.Value;

        public string Name => Bio_NotificationChannels.Email;
        public bool IsEnabled => _o.Email.Enabled && !string.IsNullOrWhiteSpace(_o.Email.Host)
                                                   && !string.IsNullOrWhiteSpace(_o.Email.From);

        public async Task SendAsync(string recipient, string subject, string body, CancellationToken ct)
        {
            var o = _o.Email;
            using var msg = new MailMessage
            {
                From = new MailAddress(o.From, o.FromName),
                Subject = subject,
                Body = body,
                SubjectEncoding = Encoding.UTF8,
                BodyEncoding = Encoding.UTF8
            };
            msg.To.Add(recipient);

            using var smtp = new SmtpClient(o.Host, o.Port)
            {
                EnableSsl = o.EnableSsl,
                Timeout = 20000
            };
            if (!string.IsNullOrEmpty(o.User))
                smtp.Credentials = new System.Net.NetworkCredential(o.User, o.Password);

            await smtp.SendMailAsync(msg, ct);
        }
    }

    // ============================================================ Telegram
    public class TelegramChannel : INotificationChannel
    {
        private readonly IHttpClientFactory _http;
        private readonly NotificationOptions _o;

        public TelegramChannel(IHttpClientFactory http, IOptions<NotificationOptions> o)
        {
            _http = http;
            _o = o.Value;
        }

        public string Name => Bio_NotificationChannels.Telegram;
        public bool IsEnabled => _o.Telegram.Enabled && !string.IsNullOrWhiteSpace(_o.Telegram.BotToken);

        public async Task SendAsync(string recipient, string subject, string body, CancellationToken ct)
        {
            var o = _o.Telegram;
            var url = $"{o.ApiBaseUrl.TrimEnd('/')}/bot{o.BotToken}/sendMessage";
            var json = JsonSerializer.Serialize(new { chat_id = recipient, text = body });

            using var req = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            var client = _http.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(15);
            using var resp = await client.SendAsync(req, ct);
            var text = await resp.Content.ReadAsStringAsync(ct);

            if (!resp.IsSuccessStatusCode)
                throw new InvalidOperationException($"Telegram HTTP {(int)resp.StatusCode}: {Str.Trunc(text, 200)}");
        }
    }
}