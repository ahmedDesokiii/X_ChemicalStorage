namespace X_ChemicalStorage.BioBank.DTOs
{
    public class BackupResultDto
    {
        public bool Success { get; set; }
        public string Code { get; set; } = "ok";        // ok | busy | cooldown | disabled | failed
        public string Message { get; set; } = "";
        public string? FileName { get; set; }
        public string? FilePath { get; set; }
        public long? SizeBytes { get; set; }
        public double DurationSeconds { get; set; }
        public bool Verified { get; set; }
        public bool CopiedToSecondary { get; set; }
        public string? AttachmentsFileName { get; set; }
    }

    public class BackupLogDto
    {
        public long Id { get; set; }
        public DateTime StartedAt { get; set; }
        public string Type { get; set; } = "";
        public string Status { get; set; } = "";
        public string? FileName { get; set; }
        public long? SizeBytes { get; set; }
        public int DurationMs { get; set; }
        public bool Verified { get; set; }
        public string? AttachmentsFileName { get; set; }
        public string? Error { get; set; }
        public string? TriggeredBy { get; set; }
    }

    public class BackupStatusDto
    {
        public bool Enabled { get; set; }
        public string DatabaseName { get; set; } = "";
        public string Directory { get; set; } = "";
        public string? SecondaryDirectory { get; set; }
        public bool IsLocalServer { get; set; }
        public string DailyTime { get; set; } = "";
        public int KeepDays { get; set; }
        public DateTime? NextRunLocal { get; set; }
        public DateTime? LastSuccessAt { get; set; }
        public double? HoursSinceLastSuccess { get; set; }
        public string? Warning { get; set; }
        public BackupLogDto? LastBackup { get; set; }
        public List<BackupLogDto> History { get; set; } = new();
    }
}