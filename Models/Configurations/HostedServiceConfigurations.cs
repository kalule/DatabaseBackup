namespace DatabaseBackup.Models.Configurations
{
    public class HostedServiceConfigurations
    {
        public Dictionary<string, HostedServiceConfig> Services { get; set; } =
            new(StringComparer.OrdinalIgnoreCase); // Enables case-insensitive lookup
    }
}
