using DatabaseBackup.Models;
using DatabaseBackup.Models.Configurations;
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
            _config = config.Value ?? throw new ArgumentNullException(nameof(config));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<List<BackupResult>> CreateBackupAsync(string reason, CancellationToken cancellationToken = default)
        {
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            var backupDir = _config.BackupDirectory;
            Directory.CreateDirectory(backupDir);

            var safeReason = string.Concat(reason.Split(Path.GetInvalidFileNameChars()));
            var results = new List<BackupResult>();

            foreach (var db in _config.Connections)
            {
                string fileExtension = db.Type.ToLowerInvariant() == "sqlserver" ? ".bak" : ".sql";
                string filePath = Path.Combine(backupDir, $"backup_{db.Database}_{safeReason}_{timestamp}{fileExtension}");
                string toolPath;
                string arguments;
                var dbHost = db.ResolvePlatformHost; 

                switch (db.Type.ToLowerInvariant())
                {
                    case "postgres":
                        toolPath = _config.PgDumpPath;
                        var pgConnectionString = $"host={dbHost} port={db.Port} dbname={db.Database} user={db.User} password={db.Password}";
                        arguments = $"--dbname=\"{pgConnectionString}\" --file=\"{filePath}\"";
                        break;

                    case "sqlserver":
                        toolPath = _config.SqlCmdPath;
                        var sql = $"BACKUP DATABASE [{db.Database}] TO DISK = N'{filePath}' WITH INIT;";
                        var authPart = string.IsNullOrWhiteSpace(db.User) || string.IsNullOrWhiteSpace(db.Password)
                                         ? "-E"
                                         : $"-U {db.User} -P {db.Password}";

                        arguments = $"-S {dbHost} {authPart} -Q \"{sql}\"";
                        break;

                    default:
                        var msg = $"[ERROR] Unsupported DB type '{db.Type}' for {db.Database}.";
                        _logger.LogError(msg);
                        results.Add(new BackupResult { Success = false, FilePath = "", Message = msg });
                        continue;
                }

                if (!File.Exists(toolPath))
                {
                    var msg = $"[ERROR] Tool not found at '{toolPath}' for {db.Type} database.";
                    _logger.LogError(msg);
                    results.Add(new BackupResult { Success = false, FilePath = "", Message = msg });
                    continue;
                }

                bool success = false;
                int attempt = 0;

                while (attempt < _config.MaxRetries && !success)
                {
                    attempt++;
                    _logger.LogInformation("Attempt {Attempt} for backup: {Database}", attempt, db.Database);

                    try
                    {
                        var (procSuccess, output, error) = await RunProcessAsync(toolPath, arguments, cancellationToken);

                        if (procSuccess)
                        {
                            var successMsg = $"Backup completed for {db.Database} on attempt {attempt}";
                            _logger.LogInformation("{Message} | File: {FilePath}", successMsg, filePath);
                            _logger.LogDebug("Output:\n{Output}", output);

                            results.Add(new BackupResult
                            {
                                Success = true,
                                FilePath = filePath,
                                Message = successMsg,
                                CompletedAt = DateTime.UtcNow
                            });

                            success = true;
                        }
                        else
                        {
                            _logger.LogWarning("Attempt {Attempt} failed for {Database}. Error: {Error}", attempt, db.Database, error);
                            _logger.LogDebug("Output:\n{Output}", output);

                            if (attempt < _config.MaxRetries)
                            {
                                _logger.LogInformation("🔁 Retrying in {Delay}ms...", _config.RetryDelayMilliseconds);
                                await Task.Delay(_config.RetryDelayMilliseconds, cancellationToken);
                            }
                            else
                            {
                                results.Add(new BackupResult
                                {
                                    Success = false,
                                    FilePath = "",
                                    Message = $"Backup failed for {db.Database} after {_config.MaxRetries} attempts.",
                                    CompletedAt = DateTime.UtcNow
                                });
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Exception during attempt {Attempt} for {Database}: {Message}", attempt, db.Database, ex.Message);

                        if (attempt < _config.MaxRetries)
                        {
                            _logger.LogInformation("Retrying in {Delay}ms...", _config.RetryDelayMilliseconds);
                            await Task.Delay(_config.RetryDelayMilliseconds, cancellationToken);
                        }
                        else
                        {
                            results.Add(new BackupResult
                            {
                                Success = false,
                                FilePath = "",
                                Message = $"Exception during backup of {db.Database} after {_config.MaxRetries} attempts: {ex.Message}",
                                CompletedAt = DateTime.UtcNow
                            });
                        }
                    }
                }
            }

            _logger.LogInformation("Backup summary: {Total} total, {Success} succeeded, {Failed} failed",
                results.Count, results.Count(r => r.Success), results.Count(r => !r.Success));

            var auditFile = Path.Combine(backupDir, $"backup_log_{timestamp}.json");
            await File.WriteAllTextAsync(auditFile, JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));
            _logger.LogInformation("Backup audit log written to: {AuditFile}", auditFile);

            return results;
        }

        private async Task<(bool Success, string Output, string Error)> RunProcessAsync(string toolPath, string arguments, CancellationToken cancellationToken)
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

            using var process = Process.Start(processInfo);
            if (process == null)
                throw new InvalidOperationException($"Failed to start process: {toolPath}");

            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();

            await process.WaitForExitAsync(cancellationToken);

            var output = await outputTask;
            var error = await errorTask;

            return (process.ExitCode == 0, output, error);
        }
    }
}