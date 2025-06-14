namespace DatabaseBackup.Models.Configurations
{
    public class HostedServiceConfigurations
    {
        public Dictionary<string, HostedServiceConfig> Services { get; set; } =
            new(StringComparer.OrdinalIgnoreCase); // Enables case-insensitive lookup
    }

    public class HostedServiceConfig
    {
        public string StartTime { get; set; }
        public int PollIntervalMinutes { get; set; }
        public bool RunOnStartUp { get; set; }
    }
}
