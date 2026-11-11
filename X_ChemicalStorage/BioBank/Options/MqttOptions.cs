// Options/MqttOptions.cs
namespace X_ChemicalStorage.BioBank.Options
{
    public class MqttOptions
    {
        public const string SectionName = "Mqtt";

        public bool Enabled { get; set; } = true;
        public string Host { get; set; } = "127.0.0.1";
        public int Port { get; set; } = 1883;
        public string ClientId { get; set; } = "BioBank-IoT-Service";
        public string Username { get; set; } = "";
        public string Password { get; set; } = "";
        public string Topic { get; set; } = "freezer/+/telemetry";
        public bool UseTls { get; set; } = false;

        /// Topic بوابات هاير (MobiusHaier بعد توجيهها لجهاز المعمل). رسايل Binary، الكود آخر جزء في الـ Topic. فاضي = متتسمعش.
        public string HaierTopic { get; set; } = "status/handle/+/+";

        /// ترتيب جزء كود الـ Collector في الـ Topic (0-based). freezer/<code>/telemetry => 1
        public int TopicCollectorIndex { get; set; } = 1;

        /// أقل فاصل (ثواني) بين حفظ قراءتين لنفس الفريزر في الجدول. الحالة الحية بتتحدث دايماً.
        public int StoreIntervalSeconds { get; set; } = 60;

        public MqttFieldMap Fields { get; set; } = new();
    }

    /// أسماء الحقول المحتملة في الـ Payload. أي قائمة تسيبها null بتاخد القيم الافتراضية.
    /// تعدّلها في appsettings بعد ما نشوف الرسايل الحقيقية (mosquitto_sub).
    public class MqttFieldMap
    {
        public string[]? Collector { get; set; }
        public string[]? Temperature { get; set; }
        public string[]? SetTemperature { get; set; }
        public string[]? AmbientTemperature { get; set; }
        public string[]? Door { get; set; }
        public string[]? Power { get; set; }
        public string[]? Online { get; set; }
        public string[]? HighCompressor { get; set; }
        public string[]? CryoCompressor { get; set; }

        /// لو البوابة بتبعت الحرارة مضروبة (مثلاً -802 بدل -80.2) حط 0.1
        public decimal TemperatureScale { get; set; } = 1m;

        /// أي قراءة خارج المدى ده بتتعامل كقراءة غير صالحة (مش بتتخزن كحرارة)
        public decimal MinPlausibleTemp { get; set; } = -200m;
        public decimal MaxPlausibleTemp { get; set; } = 100m;

        public string[]? DoorOpenValues { get; set; }
        public string[]? DoorClosedValues { get; set; }
        public string[]? PowerOkValues { get; set; }
        public string[]? PowerFailValues { get; set; }
    }
}