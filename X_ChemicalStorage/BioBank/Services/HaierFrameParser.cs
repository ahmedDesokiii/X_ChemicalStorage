using X_ChemicalStorage.BioBank.DTOs;

namespace X_ChemicalStorage.BioBank.Services
{
    /// <summary>
    /// Binary frames sent by Haier's MobiusHaier app on the InHand IG502 gateway
    /// (topic status/handle/GF502/&lt;collector&gt;). Decoded from captures of FZ80-01 on site, 2026-11-11.
    ///
    ///   [0..1]  AF AF            magic
    ///   [2]     frame type       80 heartbeat · 81 periodic data (every 5 min) · 83 event log (JSON) · 84 status (on change)
    ///   [3..4]  01 E0
    ///   [5..6]  body length, little-endian (bytes after this field, without the checksum)
    ///   body    81/84 start with FF, then yy MM dd HH mm ss (gateway clock), then the data
    ///   [last]  checksum = sum of all previous bytes, mod 256
    ///
    /// 81 data (int16 LE, from offset 14):  +4 cabinet temp x10 (-802 = -80.2 °C) · +6 ambient °C · +20 mains voltage V
    /// 84 data (from offset 14):            +0 set point x10 · +2 high alarm x10 · +4 low alarm x10 · +11 door (1 = open)
    /// </summary>
    public static class HaierFrameParser
    {
        public const byte Heartbeat = 0x80, Periodic = 0x81, EventLog = 0x83, Status = 0x84;

        public static bool IsHaierFrame(ReadOnlySpan<byte> b) => b.Length >= 8 && b[0] == 0xAF && b[1] == 0xAF;

        /// <returns>
        /// a reading (periodic: temperature + power; status: door; heartbeat: keep-alive only),
        /// or null when the frame should be ignored (event log, bad checksum) — reason in <paramref name="note"/>.
        /// </returns>
        public static IngestReadingDto? Parse(string topic, byte[] b, out string? note)
        {
            note = null;
            if (!IsHaierFrame(b)) { note = "not a Haier frame"; return null; }

            byte sum = 0;
            for (int i = 0; i < b.Length - 1; i++) sum += b[i];
            if (sum != b[^1]) { note = $"bad checksum ({sum:X2} != {b[^1]:X2})"; return null; }

            var collector = (topic ?? "").Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
            if (string.IsNullOrWhiteSpace(collector)) { note = "no collector code in topic"; return null; }

            var dto = new IngestReadingDto
            {
                CollectorCode = collector.Trim(),
                RawPayload = "haier:" + Convert.ToHexString(b)
            };

            switch (b[2])
            {
                case Heartbeat:
                    return dto;

                case Periodic:
                    if (b.Length < 37) { note = "short data frame"; return null; }
                    var t = I16(b, 18);
                    if (t is > -2000 and < 1000) dto.Temperature = t / 10m;      // -2000 = sensor not connected
                    else dto.TemperatureInvalid = true;
                    var amb = I16(b, 20);
                    if (amb is > -40 and < 80) dto.AmbientTemperature = amb;
                    var volts = I16(b, 34);
                    if (volts >= 0 && volts < 400) dto.PowerStatus = volts >= 150 ? "OK" : "Fail";
                    return dto;

                case Status:
                    if (b.Length < 27) { note = "short status frame"; return null; }
                    var sp = I16(b, 14);
                    if (sp is > -2000 and < 1000) dto.SetTemperature = sp / 10m;
                    dto.DoorStatus = b[25] == 1 ? "Open" : "Closed";
                    return dto;

                case EventLog:
                    note = "event log";
                    return null;

                default:
                    note = $"unknown frame type {b[2]:X2}";
                    return null;
            }
        }

        private static short I16(byte[] b, int at) => (short)(b[at] | (b[at + 1] << 8));
    }
}
