# DatabaseBackup

DatabaseBackup is a cross-platform .NET utility designed to automate scheduled backups for multiple databases (PostgreSQL and SQL Server). It supports retries, structured logging, and customizable configurations.

## Features

- ⏱️ Schedule automated backups using a background worker
- 💾 Backup PostgreSQL (`pg_dump`) and SQL Server (`sqlcmd`) databases
- 🔁 Retry failed backups with configurable delay and retry count
- 📁 Save backup files and audit logs in customizable locations
- 📜 Serialize audit logs in JSON for transparency and traceability
- 🧪 Easy-to-extend and testable architecture

## Project Structure

- `DatabaseBackupRunner`: Core service that performs backups using external tools.
- `BackgroundDatabaseBackupService`: Hosted service that manages scheduling and timing.
- `DatabaseConfigurations`: Defines database connection details and tools.
- `HostedServiceConfigurations`: Controls service start time, interval, and options.

## Configuration Example

```
{
  "DatabaseConfigurations": {
    "BackupDirectory": "C:\Backups",
    "PgDumpPath": "C:\Program Files\PostgreSQL\pg_dump.exe",
    "SqlCmdPath": "C:\Tools\sqlcmd.exe",
    "MaxRetries": 3,
    "RetryDelayMilliseconds": 5000,
    "Connections": [
      {
        "Type": "Postgres",
        "Host": "localhost",
        "Port": 5432,
        "Database": "mydb",
        "User": "postgres",
        "Password": "password"
      },
      {
        "Type": "SqlServer",
        "Host": "localhost",
        "Port": 1433,
        "Database": "mydb2",
        "User": "sa",
        "Password": "yourStrong(!)Password"
      }
    ]
  },
  "HostedServiceConfigurations": {
    "Services": {
      "BackgroundDatabaseBackupService": {
        "StartTime": "02:00:00",
        "PollIntervalMinutes": 1440,
        "RunOnStartUp": true
      }
    }
  }
}
```

## How It Works

1. The hosted service initializes based on the configured `StartTime` or runs immediately if `RunOnStartUp` is true.
2. It waits until the scheduled time, then calls `DatabaseBackupRunner`.
3. `DatabaseBackupRunner` runs either `pg_dump` or `sqlcmd` for each database.
4. Output and audit logs are saved to disk.
5. Failed backups are retried based on settings.

## Future Enhancements

- Upload to cloud storage (Azure Blob, S3)
- Notify via email or webhook after backup
- Dead-letter support for consistently failing backups
- Database restore support

## License

MIT