using DatabaseBackup.Enums;
using DatabaseBackup.Extensions;
using DatabaseBackup.Helpers;
using DatabaseBackup.Models;
using DatabaseBackup.Models.Configurations;
using Dawn;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Text.Json;

namespace DatabaseBackup.Services
{
    public class DatabaseBackupRunner
    {
        private readonly ILogger<DatabaseBackupRunner> _logger;
        private readonly DatabaseConfigurations _config;
        public DatabaseBackupRunner(IOptions<DatabaseConfigurations> config, ILogger<DatabaseBackupRunner> logger)
        {

            _config = Guard.Argument(config?.Value, nameof(config)).NotDefault().Value;
            _logger = Guard.Argument(logger, nameof(logger)).NotDefault().Value;
        }

        public async Task<List<BackupResult>> CreateBackupAsync(string reason, CancellationToken cancellationToken = default)
        {
            try
            {
                var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
                var baseBackupDir = _config.BackupDirectory;

                Directory.CreateDirectory(baseBackupDir);

                var safeReason = string.Join("_", reason.Split(Path.GetInvalidFileNameChars()));
                var results = new List<BackupResult>();

                foreach (var db in _config.Connections)
                {
                    var dbType = DatabaseTypeExtensions.ParseDatabaseType(db.Type);

                    if (dbType == DatabaseType.Unsupported)
                    {
                        var msg = $"Unsupported DB type '{db.Type}' for {db.Database}. Skipping backup.";
                        _logger.LogError(msg);
                        results.Add(new BackupResult { Success = false, FilePath = "", Message = msg, CompletedAt = DateTime.UtcNow });
                        continue;
                    }

                    string fileExtension = dbType == DatabaseType.SqlServer ? ".bak" : ".sql";
                    string filePath = Path.Combine(baseBackupDir, $"backup_{db.Database}_{safeReason}_{timestamp}{fileExtension}");

                    string toolPath;
                    string arguments;
                    Dictionary<string, string>? environmentVariables = null;

                    var dbHost = db.ResolvePlatformHost;

                    switch (dbType)
                    {
                        case DatabaseType.Postgres:
                            toolPath = _config.PgDumpPath;
                            environmentVariables = new Dictionary<string, string>
                            {
                                { "PGPASSWORD", db.Password }
                            };
                            arguments = BuildBackupArguments(dbType, dbHost, db.Port, db.Database, db.User, string.Empty, filePath);
                            break;

                        case DatabaseType.SqlServer:
                            toolPath = _config.SqlCmdPath;
                            arguments = BuildBackupArguments(dbType, dbHost, db.Port, db.Database, db.User, db.Password, filePath);
                            break;

                        default:
                            throw new InvalidOperationException($"Unhandled database type '{dbType}'. This should have been caught earlier.");
                    }

                    if (!File.Exists(toolPath))
                    {
                        var msg = $"Tool not found at '{toolPath}' for {db.Type} database. Skipping backup for {db.Database}.";
                        _logger.LogError(msg);
                        results.Add(new BackupResult { Success = false, FilePath = "", Message = msg, CompletedAt = DateTime.UtcNow });
                        continue;
                    }

                    // Use the retry mechanism for each individual backup operation
                    await TryBackupWithRetries(async () =>
                    {
                        // Execute the external process
                        var (procSuccess, output, error) = await RunProcessAsync(toolPath, arguments, environmentVariables, cancellationToken);

                        if (procSuccess)
                        {
                            var successMsg = $"Backup completed for {db.Database}";
                            _logger.LogInformation("{Message} | File: {FilePath}", successMsg, filePath);
                            _logger.LogTrace("Process Output:\n{Output}", output); // Use Trace for verbose output

                            results.Add(new BackupResult
                            {
                                Success = true,
                                FilePath = filePath,
                                Message = successMsg,
                                CompletedAt = DateTime.UtcNow
                            });
                        }
                        else
                        {
                            _logger.LogWarning("Backup failed for {Database}. Error: {Error}", db.Database, error);
                            _logger.LogTrace("Process Output:\n{Output}", output);
                        }

                        return procSuccess;
                    },
                    db.Database,
                    results,
                    filePath,
                    $"Backup failed for {db.Database} after {_config.MaxRetries} attempts.",
                    cancellationToken);
                }

                _logger.LogInformation("Backup summary: {Total} total, {Success} succeeded, {Failed} failed",
                    results.Count, results.Count(r => r.Success), results.Count(r => !r.Success));

                var auditLogDir = BackupPaths.GetPlatformPath();
                Directory.CreateDirectory(auditLogDir);

                var auditFile = Path.Combine(auditLogDir, $"backup_log_{timestamp}.json");

                await File.WriteAllTextAsync(auditFile, JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));
                _logger.LogInformation("Backup audit log written to: {AuditFile}", auditFile);

                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during backup operation: {Message}", ex.Message);
                return new List<BackupResult>
                {
                    new BackupResult
                    {
                        Success = false,
                        FilePath = "",
                        Message = $"Unexpected system error during backup: {ex.Message}",
                        CompletedAt = DateTime.UtcNow
                    }
                };
            }
        }
        private string BuildBackupArguments(DatabaseType dbType, string host, int port, string database, string user, string password, string filePath)
        {
            switch (dbType)
            {
                case DatabaseType.Postgres:
                    return $"--dbname=\"host={host} port={port} dbname={database} user={user}\" --file=\"{filePath}\"";

                case DatabaseType.SqlServer:
                    var useSqlAuth = !string.IsNullOrWhiteSpace(user) && !string.IsNullOrWhiteSpace(password);
                    var authPart = useSqlAuth
                        ? $"-U {user} -P {password}"
                        : "-E";

                    var safeFilePath = filePath.Replace("\\", "\\\\");
                    var sql = $"BACKUP DATABASE [{database}] TO DISK = N'{safeFilePath}' WITH INIT;";

                    return $"-S {host},{port} {authPart} -V 1 -Q \"{sql}\"";
                default:
                    throw new NotSupportedException($"Unsupported database type: {dbType}");
            }
        }

        private async Task<bool> TryBackupWithRetries(Func<Task<bool>> action, string dbName, List<BackupResult> results, string filePath, string errorMessage, CancellationToken cancellationToken)
        {
            for (int attempt = 1; attempt <= _config.MaxRetries; attempt++)
            {
                _logger.LogInformation("Attempt {Attempt}/{MaxRetries} for backup: {Database}", attempt, _config.MaxRetries, dbName);

                try
                {
                    if (await action())
                    {
                        return true;
                    }
                    else
                    {
                        _logger.LogWarning("Backup attempt {Attempt} for {Database} failed (process exited non-zero or reported failure).", attempt, dbName);
                    }

                    if (attempt < _config.MaxRetries)
                    {
                        _logger.LogInformation("Retrying in {Delay}ms...", _config.RetryDelayMilliseconds);
                        await Task.Delay(_config.RetryDelayMilliseconds, cancellationToken);
                    }
                }
                catch (OperationCanceledException)
                {
                    _logger.LogWarning("Backup for {Database} was cancelled.", dbName);
                    results.Add(new BackupResult
                    {
                        Success = false,
                        FilePath = filePath,
                        Message = $"Backup for {dbName} was cancelled.",
                        CompletedAt = DateTime.UtcNow
                    });
                    throw;
                }
                catch (Exception ex)
                {

                    _logger.LogError(ex, "Exception during attempt {Attempt} for {Database}: {Message}", attempt, dbName, ex.Message);
                    if (attempt < _config.MaxRetries)
                    {
                        _logger.LogInformation("Retrying in {Delay}ms...", _config.RetryDelayMilliseconds);
                        await Task.Delay(_config.RetryDelayMilliseconds, cancellationToken);
                    }
                }
            }

            results.Add(new BackupResult
            {
                Success = false,
                FilePath = filePath,
                Message = errorMessage,
                CompletedAt = DateTime.UtcNow
            });

            return false;
        }
        private async Task<(bool Success, string Output, string Error)> RunProcessAsync(
                string toolPath,
                string arguments,
                Dictionary<string, string>? environmentVariables,
                CancellationToken cancellationToken)
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = toolPath,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            if (environmentVariables != null)
            {
                foreach (var kvp in environmentVariables)
                {
                    processInfo.EnvironmentVariables[kvp.Key] = kvp.Value;
                }
            }

            using var process = Process.Start(processInfo);
            if (process == null)
            {
                throw new InvalidOperationException($"Failed to start process: {toolPath}");
            }

            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();

            try
            {
                await process.WaitForExitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                if (!process.HasExited)
                {
                    try { process.Kill(); }
                    catch (InvalidOperationException) { }
                    catch (Exception ex) { _logger.LogWarning(ex, "Failed to kill process {ProcessId} after cancellation.", process.Id); }
                }
                throw;
            }
            var output = await outputTask;
            var error = await errorTask;

            return (process.ExitCode == 0, output, error);
        }
    }
}
