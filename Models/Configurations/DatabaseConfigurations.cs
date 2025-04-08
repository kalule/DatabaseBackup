namespace DatabaseBackup.Models.Configurations
{
    public class DatabaseConfigurations
    {
        public string BackupDirectory { get; set; }
        public string PgDumpPath { get; set; }
        public string SqlCmdPath { get; set; }
        public int MaxRetries { get; set; }
        public int  RetryDelayMilliseconds { get; set; }
        public List<DatabaseConnectionDetails> Connections { get; set; } = new();
    }
}
