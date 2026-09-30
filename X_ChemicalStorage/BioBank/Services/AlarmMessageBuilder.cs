namespace X_ChemicalStorage.BioBank.Services
{
    public static class AlarmMessageBuilder
    {
        private static string Friendly(string type) => type switch
        {
            "HighTemp" => "High temperature",
            "LowTemp" => "Low temperature",
            "Offline" => "Offline (no data)",
            "DoorOpen" => "Door open",
            "PowerLoss" => "Power failure",
            "SensorError" => "Sensor error",
            _ => type
        };

        /// رسالة قصيرة بحروف GSM العادية (بدون ° عشان الـ SMS ما يتحولش لـ Unicode ويبقى 70 حرف بدل 160).
        public static (string Subject, string Body) Build(AlarmNotification n, DateTime localTime)
        {
            var t = localTime.ToString("dd/MM HH:mm");
            var msg = (n.Message ?? "").Replace("°C", "C").Replace("°", "");

            string subject, body;
            switch (n.Kind)
            {
                case AlarmEventKind.Escalated:
                    subject = $"[BioBank] ESCALATED {n.FreezerId}: {Friendly(n.AlarmType)}";
                    body = $"[BioBank] ESCALATED L{n.Level} {n.FreezerId} {Friendly(n.AlarmType)} - not acknowledged. {msg} ({t})";
                    break;
                case AlarmEventKind.Cleared:
                    subject = $"[BioBank] Cleared {n.FreezerId}: {Friendly(n.AlarmType)}";
                    body = $"[BioBank] CLEARED {n.FreezerId} {Friendly(n.AlarmType)} - back to normal ({t})";
                    break;
                default:
                    subject = $"[BioBank] ALARM {n.FreezerId}: {Friendly(n.AlarmType)}";
                    body = $"[BioBank] ALARM L{n.Level} {n.FreezerId} {Friendly(n.AlarmType)}: {msg} ({t})";
                    break;
            }
            return (subject, Str.Trunc(body, 300));
        }
    }
}