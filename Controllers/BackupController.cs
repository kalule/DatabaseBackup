using DatabaseBackup.Dto;
using DatabaseBackup.Extensions;
using DatabaseBackup.Services;
using Dawn;
using Microsoft.AspNetCore.Mvc;

namespace DatabaseBackup.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class BackupController : ControllerBase
    {
        private readonly DatabaseBackupRunner _backupService;
        private readonly ILogger<BackupController> _logger;

        public BackupController(DatabaseBackupRunner backupService, ILogger<BackupController> logger)
        {
            _backupService = Guard.Argument(backupService, nameof(backupService)).NotDefault().Value;
            _logger = Guard.Argument(logger, nameof(logger)).NotDefault().Value;
        }

        [HttpPost("start")]
        public async Task<IActionResult> StartBackup([FromQuery(Name = "reason")] string? reason = null)
        {
            try
            {
                reason = string.IsNullOrWhiteSpace(reason) ? "Manual" : reason;

                _logger.LogInformation("Backup initiated. Reason: {Reason}", reason);

                var executedBackups = await _backupService.CreateBackupAsync(reason);

                var backupResults = executedBackups .Select(r => new BackupResultDto
                {
                    Success = r.Success,
                    FilePath = r.FilePath,
                    Message = r.Message,
                    CompletedAt = r.CompletedAt
                }).ToList();

                if (backupResults.All(r => r.Success))
                {
                    _logger.LogInformation("All database backups completed successfully.");
                    _logger.LogInformation("Backup file paths: {Paths}", backupResults.Select(r => r.FilePath).ToList());

                    return StatusCode(StatusCodes.Status200OK, (new BackupResponseDto
                    {
                        Message = "All backups completed successfully.",
                        Backups = backupResults 
                    }));
                }
                else
                {
                    var failed = backupResults.Where(r => !r.Success).ToList();

                    _logger.LogWarning("Some database backups failed. Failures: {Failures}",
                        failed.Select(f => f.Message).ToList());

                    return StatusCode(StatusCodes.Status500InternalServerError, new
                    {
                        message = "Some backups failed.",
                        results = backupResults
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during backup.");
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    message = "An unexpected error occurred while processing the backup."
                });
            }
        }
    }
}
