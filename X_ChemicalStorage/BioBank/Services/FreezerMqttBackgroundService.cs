// Services/FreezerMqttBackgroundService.cs
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Protocol;
using X_ChemicalStorage.BioBank.DTOs;
using X_ChemicalStorage.BioBank.Options;

namespace X_ChemicalStorage.BioBank.Services
{
    public class FreezerMqttBackgroundService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly MqttOptions _options;
        private readonly ILogger<FreezerMqttBackgroundService> _logger;
        private IMqttClient? _mqttClient;

        public FreezerMqttBackgroundService(
            IServiceScopeFactory scopeFactory,
            IOptions<MqttOptions> options,
            ILogger<FreezerMqttBackgroundService> logger)
        {
            _scopeFactory = scopeFactory;
            _options = options.Value;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation(
                "Freezer MQTT Service starting... Broker={Host}:{Port} Topic={Topic}",
                _options.Host, _options.Port, _options.Topic);

            var factory = new MqttFactory();
            _mqttClient = factory.CreateMqttClient();

            _mqttClient.ApplicationMessageReceivedAsync += OnMessageReceivedAsync;

            _mqttClient.ConnectedAsync += async e =>
            {
                _logger.LogInformation("MQTT connected to {Host}:{Port}", _options.Host, _options.Port);

                var topicFilter = new MqttTopicFilterBuilder()
                    .WithTopic(_options.Topic)
                    .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                    .Build();

                await _mqttClient.SubscribeAsync(topicFilter, stoppingToken);
                _logger.LogInformation("Subscribed to topic: {Topic}", _options.Topic);
            };

            _mqttClient.DisconnectedAsync += async e =>
            {
                _logger.LogWarning("MQTT disconnected. Reason: {Reason}", e.Reason);
                if (stoppingToken.IsCancellationRequested) return;

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                    if (_mqttClient != null && !_mqttClient.IsConnected)
                        await ConnectAsync(stoppingToken);
                }
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "MQTT reconnect failed");
                }
            };

            // محاولة الاتصال الأولى (مع إعادة محاولة لو الـ Broker مش شغال)
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ConnectAsync(stoppingToken);
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "MQTT connect failed, retrying in 5s...");
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                }
            }

            try
            {
                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (OperationCanceledException) { }
        }

        private async Task ConnectAsync(CancellationToken ct)
        {
            if (_mqttClient == null) return;

            var builder = new MqttClientOptionsBuilder()
                .WithClientId(_options.ClientId + "-" + Guid.NewGuid().ToString("N").Substring(0, 8))
                .WithTcpServer(_options.Host, _options.Port)
                .WithCleanSession()
                .WithKeepAlivePeriod(TimeSpan.FromSeconds(30));

            if (!string.IsNullOrWhiteSpace(_options.Username))
                builder.WithCredentials(_options.Username, _options.Password);

            if (_options.UseTls)
                builder.WithTls();

            await _mqttClient.ConnectAsync(builder.Build(), ct);
        }

        private async Task OnMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs e)
        {
            try
            {
                var topic = e.ApplicationMessage.Topic ?? "";

                // تحويل الـ Payload بطريقة متوافقة مع كل الإصدارات
                string payload;
                try
                {
                    // MQTTnet 4.x
                    payload = e.ApplicationMessage.ConvertPayloadToString() ?? "{}";
                }
                catch
                {
                    // fallback
                    var bytes = e.ApplicationMessage.Payload;
                    payload = (bytes == null || bytes.Length == 0)
                        ? "{}"
                        : Encoding.UTF8.GetString(bytes);
                }

                _logger.LogDebug("MQTT message on {Topic}: {Payload}", topic, payload);

                string? collectorFromTopic = null;
                var parts = topic.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2)
                    collectorFromTopic = parts[1];

                using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(payload) ? "{}" : payload);
                var root = doc.RootElement;

                var dto = new IngestReadingDto
                {
                    CollectorCode = GetString(root, "collectorCode")
                                   ?? GetString(root, "collector")
                                   ?? GetString(root, "deviceId")
                                   ?? collectorFromTopic
                                   ?? "",
                    Temperature = GetDecimal(root, "temperature")
                                  ?? GetDecimal(root, "temp")
                                  ?? 0m,
                    IsOnline = GetBool(root, "isOnline")
                               ?? GetBool(root, "online")
                               ?? true,
                    DoorStatus = GetString(root, "doorStatus") ?? GetString(root, "door"),
                    PowerStatus = GetString(root, "powerStatus") ?? GetString(root, "power"),
                    RawPayload = payload
                };

                if (string.IsNullOrWhiteSpace(dto.CollectorCode))
                {
                    _logger.LogWarning("MQTT message ignored — no CollectorCode. Topic={Topic}", topic);
                    return;
                }

                using var scope = _scopeFactory.CreateScope();
                var iotService = scope.ServiceProvider.GetRequiredService<IFreezerIoTService>();
                await iotService.ProcessIncomingReadingAsync(dto);

                _logger.LogInformation(
                    "Ingested reading from {Collector}: {Temp}°C Online={Online}",
                    dto.CollectorCode, dto.Temperature, dto.IsOnline);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing MQTT message");
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            if (_mqttClient != null)
            {
                try
                {
                    if (_mqttClient.IsConnected)
                        await _mqttClient.DisconnectAsync();
                }
                catch { /* ignore */ }
                _mqttClient.Dispose();
            }
            await base.StopAsync(cancellationToken);
        }

        private static string? GetString(JsonElement root, string name)
        {
            if (root.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String)
                return p.GetString();
            foreach (var prop in root.EnumerateObject())
            {
                if (string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase)
                    && prop.Value.ValueKind == JsonValueKind.String)
                    return prop.Value.GetString();
            }
            return null;
        }

        private static decimal? GetDecimal(JsonElement root, string name)
        {
            if (root.TryGetProperty(name, out var p))
            {
                if (p.ValueKind == JsonValueKind.Number && p.TryGetDecimal(out var d)) return d;
                if (p.ValueKind == JsonValueKind.String && decimal.TryParse(p.GetString(), out var ds)) return ds;
            }
            foreach (var prop in root.EnumerateObject())
            {
                if (!string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase)) continue;
                if (prop.Value.ValueKind == JsonValueKind.Number && prop.Value.TryGetDecimal(out var d)) return d;
                if (prop.Value.ValueKind == JsonValueKind.String && decimal.TryParse(prop.Value.GetString(), out var ds)) return ds;
            }
            return null;
        }

        private static bool? GetBool(JsonElement root, string name)
        {
            if (root.TryGetProperty(name, out var p))
            {
                if (p.ValueKind == JsonValueKind.True) return true;
                if (p.ValueKind == JsonValueKind.False) return false;
                if (p.ValueKind == JsonValueKind.String && bool.TryParse(p.GetString(), out var b)) return b;
            }
            return null;
        }
    }
}