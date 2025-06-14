using DatabaseBackup.Enums;

namespace DatabaseBackup.Extensions
{
    public static class DatabaseTypeExtensions
    {
        public static DatabaseType ParseDatabaseType(this string type) 
        {
            if (Enum.TryParse<DatabaseType>(type, true, out var dbType))
                return dbType;

            throw new NotSupportedException($"Unsupported database type: {type}");
        }
    }
}
