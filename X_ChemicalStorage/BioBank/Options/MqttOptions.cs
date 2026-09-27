// Options/MqttOptions.cs
namespace X_ChemicalStorage.BioBank.Options
{
    public class MqttOptions
    {
        public const string SectionName = "Mqtt";

        public string Host { get; set; } = "127.0.0.1";
        public int Port { get; set; } = 1883;
        public string ClientId { get; set; } = "BioBank-IoT-Service";
        public string Username { get; set; } = "";
        public string Password { get; set; } = "";
        public string Topic { get; set; } = "freezer/+/telemetry";
        public bool UseTls { get; set; } = false;
    }
}