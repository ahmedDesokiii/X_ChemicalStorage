// Services/FreezerMqttBackgroundService.cs
using System.Text;
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
        private readonly HashSet<string> _unknownLogged = new();
        private readonly string _clientId;
        private readonly IoTHealth _health;
        private readonly StationState _station;

        public FreezerMqttBackgroundService(
            IServiceScopeFactory scopeFactory,
            IOptions<MqttOptions> options,
            ILogger<FreezerMqttBackgroundService> logger,
            IoTHealth health,
            StationState station)
        {
            _station = station;
            _health = health;
            _scopeFactory = scopeFactory;
            _options = options.Value;
            _logger = logger;
            // ID واحد لكل تشغيل: يمنع تعارض لو فيه نسختين من التطبيق شغالين
            _clientId = _options.ClientId + "-" + Guid.NewGuid().ToString("N").Substring(0, 8);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _health.MqttEnabled = _options.Enabled;
            if (!_options.Enabled)
            {
                _logger.LogInformation("Freezer MQTT service is disabled (Mqtt:Enabled=false).");
                return;
            }

            _logger.LogInformation("Freezer MQTT service starting. Broker={Host}:{Port} Topic={Topic}",
                _options.Host, _options.Port, _options.Topic);

            var factory = new MqttFactory();
            using var client = factory.CreateMqttClient();
            client.ApplicationMessageReceivedAsync += OnMessageReceivedAsync;

            // حلقة واحدة مسؤولة عن الاتصال وإعادته (بدون Handlers متداخلة)
            int attempt = 0;
            bool wasConnected = false;

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    _health.MqttConnected = client.IsConnected;
                    if (!client.IsConnected)
                    {
                        if (wasConnected)
                        {
                            _logger.LogWarning("MQTT connection lost. Reconnecting...");
                            wasConnected = false;
                        }

                        await ConnectAndSubscribeAsync(client, stoppingToken);
                        attempt = 0;
                        wasConnected = true;
                        _health.MqttConnected = true;
                        _logger.LogInformation("MQTT connected to {Host}:{Port}, subscribed to {Topic}",
                            _options.Host, _options.Port, _options.Topic);
                    }

                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _health.MqttConnected = false;
                    attempt++;
                    var delay = attempt <= 1 ? 5 : attempt == 2 ? 10 : 30;
                    _logger.LogWarning("MQTT connect failed: {Reason}. Retrying in {Delay}s (attempt {Attempt})",
                        ex.GetBaseException().Message, delay, attempt);

                    try { await Task.Delay(TimeSpan.FromSeconds(delay), stoppingToken); }
                    catch (OperationCanceledException) { break; }
                }
            }

            try
            {
                if (client.IsConnected)
                    await client.DisconnectAsync();
            }
            catch { /* ignore */ }
        }

        private async Task ConnectAndSubscribeAsync(IMqttClient client, CancellationToken ct)
        {
            var builder = new MqttClientOptionsBuilder()
                .WithClientId(_clientId)
                .WithTcpServer(_options.Host, _options.Port)
                .WithCleanSession()
                .WithKeepAlivePeriod(TimeSpan.FromSeconds(30))
                .WithTimeout(TimeSpan.FromSeconds(10));

            if (!string.IsNullOrWhiteSpace(_options.Username))
                builder.WithCredentials(_options.Username, _options.Password);

            if (_options.UseTls)
                builder.WithTls();

            await client.ConnectAsync(builder.Build(), ct);

            foreach (var topic in SubscribedTopics())
            {
                var topicFilter = new MqttTopicFilterBuilder()
                    .WithTopic(topic)
                    .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                    .Build();
                await client.SubscribeAsync(topicFilter, ct);
            }
        }

        /// Topic (comma-separated) + Haier gateways' own topic.
        private IEnumerable<string> SubscribedTopics() =>
            (_options.Topic + "," + _options.HaierTopic)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.Ordinal);

        private async Task OnMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs e)
        {
            try
            {
                var topic = e.ApplicationMessage.Topic ?? "";
                var raw = e.ApplicationMessage.PayloadSegment.ToArray();

                // Haier gateway (MobiusHaier): binary frames
                if (HaierFrameParser.IsHaierFrame(raw))
                {
                    _health.MqttMessageReceived(DateTime.UtcNow);
                    var haier = HaierFrameParser.Parse(topic, raw, out var note);
                    if (haier == null)
                    {
                        if (note != "event log")
                            _logger.LogWarning("Haier frame ignored: {Note}. Topic={Topic}", note, topic);
                        return;
                    }
                    await IngestAsync(haier, topic);
                    return;
                }

                string payload;
                try
                {
                    payload = e.ApplicationMessage.ConvertPayloadToString() ?? "{}";
                }
                catch
                {
                    var bytes = e.ApplicationMessage.Payload;
                    payload = (bytes == null || bytes.Length == 0) ? "{}" : Encoding.UTF8.GetString(bytes);
                }

                _health.MqttMessageReceived(DateTime.UtcNow);
                _logger.LogDebug("MQTT {Topic}: {Payload}", topic, payload);

                var dto = FreezerPayloadParser.Parse(topic, payload, _options, out var error, out var warnings);
                if (dto == null)
                {
                    _logger.LogWarning("MQTT message ignored: {Error}. Topic={Topic}", error, topic);
                    return;
                }

                foreach (var w in warnings)
                    _logger.LogWarning("MQTT {Collector}: {Warning}", dto.CollectorCode, w);

                await IngestAsync(dto, topic);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing MQTT message");
            }
        }

        private async Task IngestAsync(IngestReadingDto dto, string topic)
        {
                using var scope = _scopeFactory.CreateScope();
                var iot = scope.ServiceProvider.GetRequiredService<IFreezerIoTService>();
                var result = await iot.ProcessIncomingReadingAsync(dto);

                switch (result)
                {
                    case IngestResult.UnknownDevice:
                        _station.UnlinkedCollectorSeen(dto.CollectorCode, topic, dto.Temperature, DateTime.UtcNow);
                        if (_unknownLogged.Add(dto.CollectorCode))
                            _logger.LogWarning(
                                "Data received from unregistered collector {Collector}. Link it to a freezer (api/iot/link).",
                                dto.CollectorCode);
                        break;

                    default:
                        _station.CollectorLinked(dto.CollectorCode);
                        _logger.LogDebug("Ingested {Collector}: {Temp}°C Result={Result}",
                            dto.CollectorCode, dto.Temperature, result);
                        break;
                }
        }
    }
}