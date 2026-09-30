namespace X_ChemicalStorage.BioBank.DTOs
{
    public enum IngestResult
    {
        Stored,          // اتخزنت قراءة + اتحدثت الحالة
        Live,            // اتحدثت الحالة الحية بس (القراءة اتخطت بسبب فاصل التخزين)
        Heartbeat,       // رسالة بدون حرارة صالحة: اتسجل إن الجهاز حي
        UnknownDevice    // الـ Collector مش مربوط بأي فريزر
    }
}