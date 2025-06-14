using System.Runtime.InteropServices;

namespace DatabaseBackup.Helpers
{
    public static class BackupPaths
    {
        public const string WindowsBackupDirectory = @"C:\DatabaseBackups";
        public const string LinuxBackupDirectory = "/shared-backups";

        public static string GetPlatformPath()
        {
            return RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? WindowsBackupDirectory
                : LinuxBackupDirectory;
        }
    }

}
