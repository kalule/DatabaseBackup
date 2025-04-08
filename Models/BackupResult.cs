namespace DatabaseBackup.Models
{
    public class BackupResult
    {
        public bool Success { get; set; }
        public string FilePath { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public DateTime CompletedAt { get; set; } // Set this explicitly when backup finishes
    }


   


}
