using X_ChemicalStorage.BioBank.Services;

namespace X_ChemicalStorage.BioBank.Services
{
    /// كل 30 ثانية: كشف انقطاع الاتصال + التصعيد. شغال حتى لو خدمة MQTT متوقفة.
    public class FreezerWatchdogService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<FreezerWatchdogService> _logger;

        public FreezerWatchdogService(IServiceScopeFactory scopeFactory, ILogger<FreezerWatchdogService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // مهلة بسيطة لحد ما التطبيق و الـ Migration يخلصوا
            try { await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken); }
            catch (OperationCanceledException) { return; }

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var engine = scope.ServiceProvider.GetRequiredService<IFreezerAlarmEngine>();
                    await engine.RunPeriodicChecksAsync(DateTime.UtcNow, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Freezer watchdog cycle failed");
                }

                try { await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken); }
                catch (OperationCanceledException) { break; }
            }
        }
    }
}