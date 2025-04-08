namespace DatabaseBackup.Models.Configurations
{
    public class HostedServiceConfig
    {
        public string StartTime { get; set; }
        public int PollIntervalMinutes { get; set; }
        public bool RunOnStartUp { get; set; }
    }
}
