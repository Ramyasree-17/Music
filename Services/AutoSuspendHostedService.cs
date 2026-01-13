using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace TunewaveAPIDB1.Services
{
    public class AutoSuspendHostedService : BackgroundService
    {
        private readonly IServiceProvider _provider;

        public AutoSuspendHostedService(IServiceProvider provider)
        {
            _provider = provider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                using var scope = _provider.CreateScope();
                var zoho = scope.ServiceProvider.GetRequiredService<ZohoBooksService>();

                await zoho.AutoSuspendOverdueInvoices();
                await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
            }
        }
    }
}



