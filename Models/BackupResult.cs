namespace DatabaseBackup.Models
{
    public class BackupResult
    {
        public bool Success { get; set; }
        public string FilePath { get; set; }
        public string Message { get; set; }
        public DateTime CompletedAt { get; set; }
    }

}
