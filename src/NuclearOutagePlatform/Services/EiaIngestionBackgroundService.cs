using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MVC_EF_Start_8.Services
{
    /// <summary>
    /// Runs EiaIngestionService on a recurring schedule for the app's
    /// lifetime, replacing the original "fetch on page load if the table
    /// looks empty" trigger. Runs once shortly after startup (so the app
    /// has real data without anyone needing to load a page first), then
    /// repeats every Eia:IngestionIntervalHours (default: 6).
    ///
    /// Lifetime note: BackgroundService/IHostedService instances are
    /// registered as Singletons by the framework, but EiaIngestionService
    /// (and the DbContext underneath it, via OutageService) are Scoped.
    /// This creates a fresh DI scope on every run via IServiceScopeFactory
    /// rather than holding one long-lived instance for the app's entire
    /// life -- the same category of Singleton/Scoped lifetime mismatch
    /// documented as a bug in the original codebase (see README), handled
    /// correctly here from the start.
    /// </summary>
    public class EiaIngestionBackgroundService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<EiaIngestionBackgroundService> _logger;
        private readonly TimeSpan _interval;
        private static readonly TimeSpan InitialDelay = TimeSpan.FromSeconds(15);

        public EiaIngestionBackgroundService(
            IServiceScopeFactory scopeFactory,
            IConfiguration configuration,
            ILogger<EiaIngestionBackgroundService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;

            var hours = configuration.GetValue<double?>("Eia:IngestionIntervalHours") ?? 6.0;
            _interval = TimeSpan.FromHours(hours);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation(
                "EIA background ingestion started. Interval: every {Hours} hour(s).",
                _interval.TotalHours);

            try
            {
                await Task.Delay(InitialDelay, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                return;
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                await RunOnceAsync();

                try
                {
                    await Task.Delay(_interval, stoppingToken);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
            }
        }

        private async Task RunOnceAsync()
        {
            // Scoped services (EiaIngestionService -> OutageService ->
            // ApplicationDbContext) can't be injected directly into a
            // Singleton -- a new scope has to be created for each run.
            using var scope = _scopeFactory.CreateScope();
            var ingestionService = scope.ServiceProvider.GetRequiredService<EiaIngestionService>();

            try
            {
                await ingestionService.IngestAsync();
            }
            catch (Exception ex)
            {
                // A failed run shouldn't crash the background service or
                // the app -- log it and try again next interval.
                _logger.LogError(ex, "EIA ingestion run failed.");
            }
        }
    }
}
