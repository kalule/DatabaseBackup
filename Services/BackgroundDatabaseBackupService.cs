using Dawn;
using Microsoft.Extensions.Options;
using DatabaseBackup.Extensions;
using DatabaseBackup.Models.Configurations;
using DatabaseBackup.Constants.ServiceConstants;

namespace DatabaseBackup.Services
{
    public class BackgroundDatabaseBackupService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<BackgroundDatabaseBackupService> _logger;
        private readonly HostedServiceConfig _serviceConfig;

        private DateTime _startDateTime;
        private readonly string _serviceName = ServiceNameConstants.BackgroundDatabaseBackupService;

        public BackgroundDatabaseBackupService(ILogger<BackgroundDatabaseBackupService> logger, IServiceProvider serviceProvider,IOptions<HostedServiceConfigurations> serviceConfigOptions, IConfiguration configuration)
        {
            Guard.Argument(logger, nameof(logger)).NotDefault();
            Guard.Argument(serviceProvider, nameof(serviceProvider)).NotDefault();
            Guard.Argument(serviceConfigOptions, nameof(serviceConfigOptions)).NotDefault();
            Guard.Argument(configuration, nameof(configuration)).NotDefault();

            _logger = logger;
            _serviceProvider = serviceProvider;

            var serviceConfigMap = serviceConfigOptions.Value.Services;
            _serviceConfig = Guard.Argument(
                    serviceConfigMap.FirstOrDefault(x =>
                        x.Key.Equals(_serviceName, StringComparison.OrdinalIgnoreCase)).Value,
                    nameof(serviceConfigMap))
                .NotDefault().Value;

            _startDateTime = GetDateTime(null, _serviceConfig.StartTime);
            CalculateNextRunDate(_serviceConfig.RunOnStartUp);

            _logger.LogInformation("Backup schedule initialized. First run at: {Time}", _startDateTime);
        }

        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogInformation("Backup Worker heartbeat at: {Time}", DateTime.Now);

                try
                {
                    if (_startDateTime <= DateTime.Now)
                    {
                        await DoWork(cancellationToken);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred during backup process: {Message}", ex.Message);
                }

                await Task.Delay(GetDelayTime(), cancellationToken);
            }
        }

        private async Task DoWork(CancellationToken cancellationToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var backupService = scope.ServiceProvider.GetRequiredService<DatabaseBackupRunner>();

            _logger.LogInformation("Running automated backup process...");

            if (cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning("Backup canceled before it started.");
                return;
            }

            await backupService.CreateBackupAsync("automated", cancellationToken);
            _logger.LogInformation("Backup process completed.");
        }

        private TimeSpan GetDelayTime()
        {
            CalculateNextRunDate(false);
            _logger.LogInformation("Next backup scheduled for: {Time}", _startDateTime);
            return _startDateTime.Subtract(DateTime.Now);
        }

        private void CalculateNextRunDate(bool runNow)
        {
            if (runNow)
            {
                _startDateTime = DateTime.Now;
            }
            else
            {
                while (_startDateTime < DateTime.Now)
                {
                    _startDateTime = _startDateTime.AddMinutes(_serviceConfig.PollIntervalMinutes);
                }
            }
        }

        private DateTime GetDateTime(string? dayValue, string timeValue = null)
        {
            var now = DateTime.Now;
            if (string.IsNullOrWhiteSpace(timeValue)) return now;

            var units = timeValue.Split(":");

            int.TryParse(units.ElementAtOrDefault(0), out var hh);
            int.TryParse(units.ElementAtOrDefault(1), out var mm);
            int.TryParse(units.ElementAtOrDefault(2), out var ss);
            int.TryParse(dayValue, out var dd);
            dd = dd == 0 ? now.Day : dd;

            return new DateTime(now.Year, now.Month, dd, hh, mm, ss);
        }
    }
}
