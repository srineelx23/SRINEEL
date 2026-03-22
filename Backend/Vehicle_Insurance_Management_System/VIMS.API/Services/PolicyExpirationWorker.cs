using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;
using VIMS.Application.Interfaces.Services;

namespace VIMS.API.Services
{
    public class PolicyExpirationWorker : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<PolicyExpirationWorker> _logger;

        public PolicyExpirationWorker(IServiceProvider serviceProvider, ILogger<PolicyExpirationWorker> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("PolicyExpirationWorker is starting.");

            while (!stoppingToken.IsCancellationRequested)
            {
                // Run once a day at midnight? Or just run and wait 24h.
                // For simplicity, run every 24 hours.
                try
                {
                    _logger.LogInformation("Checking for expiring policies...");
                    
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
                        await notificationService.CheckAndNotifyExpiringPoliciesAsync();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred while checking for expiring policies.");
                }

                await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
            }

            _logger.LogInformation("PolicyExpirationWorker is stopping.");
        }
    }
}
