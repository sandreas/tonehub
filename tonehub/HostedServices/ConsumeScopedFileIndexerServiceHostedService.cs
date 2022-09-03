using tonehub.HostedServices.Scoped;

namespace tonehub.HostedServices;
using ILogger = Serilog.ILogger;

public class ConsumeScopedFileIndexerServiceHostedService: BackgroundService
{
    public IServiceProvider Services { get; }
    private readonly ILogger _logger;

    public ConsumeScopedFileIndexerServiceHostedService(IServiceProvider services, 
        ILogger logger)
    {
        Services = services;
        _logger = logger;
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.Information("Consume Scoped Service Hosted Service running");

        await DoWork(stoppingToken);
    }
    
    private async Task DoWork(CancellationToken stoppingToken)
    {
        _logger.Information(  "Consume Scoped Service Hosted Service is working");

        using var scope = Services.CreateScope();
        var scopedProcessingService = 
            scope.ServiceProvider.GetRequiredService<IScopedFileIndexerService>();
        await scopedProcessingService.DoWork(stoppingToken);
    }
    
    public override async Task StopAsync(CancellationToken stoppingToken)
    {
        _logger.Information("Consume Scoped Service Hosted Service is stopping");
        await base.StopAsync(stoppingToken);
    }
}

