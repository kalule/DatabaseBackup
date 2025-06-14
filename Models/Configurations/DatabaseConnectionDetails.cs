using System.Runtime.InteropServices;

namespace DatabaseBackup.Models.Configurations
{
    public class DatabaseConnectionDetails
    {
        public string Type { get; set; }
        public string HostWindows { get; set; }
        public string HostLinux { get; set; }
        public int Port { get; set; }
        public string Database { get; set; }
        public string User { get; set; }
        public string Password { get; set; }
        public string ResolvePlatformHost =>
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? HostWindows : HostLinux;
    }

}
