namespace DatabaseBackup.Dto
{
    public class BackupResultDto
    {
        public bool Success { get; set; }
        public string? FilePath { get; set; }
        public string? Message { get; set; }
        public DateTime CompletedAt { get; set; }
    }
}
