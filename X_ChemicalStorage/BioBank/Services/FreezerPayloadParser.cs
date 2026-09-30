using System.Globalization;
using System.Text.Json;
using X_ChemicalStorage.BioBank.DTOs;
using X_ChemicalStorage.BioBank.Options;

namespace X_ChemicalStorage.BioBank.Services
{
    /// يحوّل رسالة MQTT (topic + JSON) إلى IngestReadingDto. لا يعتمد على MQTTnet ولا قاعدة البيانات.
    public static class FreezerPayloadParser
    {
        private static readonly string[] DefCollector = { "collectorcode", "collector", "deviceid", "device_id", "sn", "serial" };
        private static readonly string[] DefTemp = { "temperature", "temp", "current_temp", "temp_live", "tem" };
        private static readonly string[] DefSet = { "set_temp", "settemp", "set_temperature", "setpoint" };
        private static readonly string[] DefAmbient = { "ambient_temp", "ambient_temperature", "ambient", "env_temp", "room_temp" };
        private static readonly string[] DefDoor = { "door", "door_status", "doorstatus", "door_alarm" };
        private static readonly string[] DefPower = { "power", "power_status", "powerstatus", "voltage_status" };
        private static readonly string[] DefOnline = { "isonline", "online" };
        private static readonly string[] DefHighComp = { "high_compressor", "high_temp_compressor", "compressor_high" };
        private static readonly string[] DefCryoComp = { "cryo_compressor", "cryogenic_compressor", "compressor_low" };

        private static readonly string[] DefDoorOpen = { "1", "true", "open", "opened", "ajar", "alarm" };
        private static readonly string[] DefDoorClosed = { "0", "false", "close", "closed", "normal" };
        private static readonly string[] DefPowerOk = { "1", "true", "ok", "normal" };
        private static readonly string[] DefPowerFail = { "0", "false", "fail", "failure", "lost", "off", "alarm" };

        /// <returns>null لو الرسالة مش صالحة (والسبب في error). warnings = ملاحظات مش قاتلة.</returns>
        public static IngestReadingDto? Parse(string topic, string payload, MqttOptions opt,
            out string? error, out List<string> warnings)
        {
            error = null;
            warnings = new List<string>();
            var f = opt.Fields ?? new MqttFieldMap();

            Dictionary<string, JsonElement> map;
            try
            {
                using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(payload) ? "{}" : payload);
                map = Flatten(doc.RootElement);
            }
            catch (JsonException ex)
            {
                error = "Payload is not valid JSON: " + ex.Message;
                return null;
            }

            // ===== Collector: من الـ Topic الأول، وبعدين من الـ Payload =====
            string? collector = null;
            var parts = (topic ?? "").Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (opt.TopicCollectorIndex >= 0 && opt.TopicCollectorIndex < parts.Length)
                collector = parts[opt.TopicCollectorIndex];
            if (string.IsNullOrWhiteSpace(collector))
                collector = Find(map, f.Collector ?? DefCollector, AsString);
            if (string.IsNullOrWhiteSpace(collector))
            {
                error = "No collector code in topic or payload";
                return null;
            }

            var dto = new IngestReadingDto
            {
                CollectorCode = collector.Trim(),
                RawPayload = payload
            };

            // ===== الحرارة =====
            var temp = Find(map, f.Temperature ?? DefTemp, AsDecimal);
            if (temp.HasValue)
            {
                var t = temp.Value * (f.TemperatureScale == 0 ? 1m : f.TemperatureScale);
                if (t < f.MinPlausibleTemp || t > f.MaxPlausibleTemp)
                    warnings.Add($"Temperature {t} outside plausible range [{f.MinPlausibleTemp},{f.MaxPlausibleTemp}] - ignored");
                else
                    dto.Temperature = t;
            }
            else
            {
                warnings.Add("No temperature field found");
            }

            dto.SetTemperature = Scaled(Find(map, f.SetTemperature ?? DefSet, AsDecimal), f);
            dto.AmbientTemperature = Scaled(Find(map, f.AmbientTemperature ?? DefAmbient, AsDecimal), f);

            // ===== الباب والكهرباء =====
            var doorRaw = Find(map, f.Door ?? DefDoor, AsString);
            if (doorRaw != null)
            {
                var v = doorRaw.Trim().ToLowerInvariant();
                if (In(v, f.DoorOpenValues ?? DefDoorOpen)) dto.DoorStatus = "Open";
                else if (In(v, f.DoorClosedValues ?? DefDoorClosed)) dto.DoorStatus = "Closed";
                else warnings.Add($"Unrecognized door value '{doorRaw}'");
            }

            var powerRaw = Find(map, f.Power ?? DefPower, AsString);
            if (powerRaw != null)
            {
                var v = powerRaw.Trim().ToLowerInvariant();
                if (In(v, f.PowerOkValues ?? DefPowerOk)) dto.PowerStatus = "OK";
                else if (In(v, f.PowerFailValues ?? DefPowerFail)) dto.PowerStatus = "Fail";
                else warnings.Add($"Unrecognized power value '{powerRaw}'");
            }

            dto.IsOnline = Find(map, f.Online ?? DefOnline, AsBool) ?? true;
            dto.HighCompressorRunning = Find(map, f.HighCompressor ?? DefHighComp, AsBool);
            dto.CryoCompressorRunning = Find(map, f.CryoCompressor ?? DefCryoComp, AsBool);

            return dto;
        }

        // ---------------------------------------------------------------- helpers

        private static decimal? Scaled(decimal? v, MqttFieldMap f) =>
            v.HasValue ? v.Value * (f.TemperatureScale == 0 ? 1m : f.TemperatureScale) : null;

        private static bool In(string v, string[] list) =>
            list.Any(x => string.Equals(x, v, StringComparison.OrdinalIgnoreCase));

        private static T? Find<T>(Dictionary<string, JsonElement> map, string[] names, Func<JsonElement, T?> conv)
        {
            foreach (var n in names)
            {
                if (map.TryGetValue(n.ToLowerInvariant(), out var el))
                {
                    var r = conv(el);
                    if (r != null) return r;
                }
            }
            return default;
        }

        private static string? AsString(JsonElement e) => e.ValueKind switch
        {
            JsonValueKind.String => e.GetString(),
            JsonValueKind.Number => e.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => null
        };

        private static decimal? AsDecimal(JsonElement e)
        {
            if (e.ValueKind == JsonValueKind.Number && e.TryGetDecimal(out var d)) return d;
            if (e.ValueKind == JsonValueKind.String &&
                decimal.TryParse(e.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var ds)) return ds;
            return null;
        }

        private static bool? AsBool(JsonElement e)
        {
            switch (e.ValueKind)
            {
                case JsonValueKind.True: return true;
                case JsonValueKind.False: return false;
                case JsonValueKind.Number: return e.TryGetDecimal(out var d) ? d != 0 : null;
                case JsonValueKind.String:
                    var s = e.GetString()?.Trim().ToLowerInvariant();
                    if (s is "true" or "1" or "yes" or "on" or "online" or "run" or "running") return true;
                    if (s is "false" or "0" or "no" or "off" or "offline" or "stop" or "stopped") return false;
                    return null;
                default: return null;
            }
        }

        /// يسطّح الـ JSON: كل مفتاح متاح باسمه الكامل (a.b) وباسم الورقة (b). أول ظهور بيكسب.
        /// بيدعم كمان صيغة [{"name":"temp","value":-80}] .
        private static Dictionary<string, JsonElement> Flatten(JsonElement root)
        {
            var map = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
            Walk(root, "", map);
            return map;
        }

        private static void Add(Dictionary<string, JsonElement> map, string key, JsonElement v)
        {
            key = key.ToLowerInvariant();
            if (!map.ContainsKey(key)) map[key] = v.Clone();
        }

        private static void Walk(JsonElement el, string path, Dictionary<string, JsonElement> map)
        {
            switch (el.ValueKind)
            {
                case JsonValueKind.Object:
                    foreach (var p in el.EnumerateObject())
                    {
                        var full = path.Length == 0 ? p.Name : path + "." + p.Name;
                        if (p.Value.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
                            Walk(p.Value, full, map);
                        else
                        {
                            Add(map, full, p.Value);
                            Add(map, p.Name, p.Value);
                        }
                    }
                    break;

                case JsonValueKind.Array:
                    int i = 0;
                    foreach (var item in el.EnumerateArray())
                    {
                        if (item.ValueKind == JsonValueKind.Object && TryNameValue(item, out var name, out var val))
                        {
                            Add(map, name, val);
                            if (path.Length > 0) Add(map, path + "." + name, val);
                        }
                        else
                            Walk(item, path + "[" + i + "]", map);
                        i++;
                    }
                    break;
            }
        }

        private static bool TryNameValue(JsonElement item, out string name, out JsonElement value)
        {
            name = ""; value = default;
            string? n = null; JsonElement? v = null;
            foreach (var p in item.EnumerateObject())
            {
                var k = p.Name.ToLowerInvariant();
                if ((k is "name" or "key" or "identifier" or "tag") && p.Value.ValueKind == JsonValueKind.String)
                    n = p.Value.GetString();
                else if (k is "value" or "val" or "v")
                    v = p.Value;
            }
            if (n == null || v == null || v.Value.ValueKind is JsonValueKind.Object or JsonValueKind.Array) return false;
            name = n; value = v.Value;
            return true;
        }
    }
}