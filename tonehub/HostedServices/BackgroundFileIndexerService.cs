using tonehub.Services;
using FileModel = tonehub.Database.Models.File;
using ILogger = Serilog.ILogger;

namespace tonehub.HostedServices;

public class BackgroundFileIndexerService: IHostedService, IDisposable
{
    private readonly Timer _timer;
    private readonly DatabaseSettingsService _settings;
    private readonly FileIndexerService _fileIndexer;
    private readonly ILogger _logger;

    public BackgroundFileIndexerService(ILogger logger, DatabaseSettingsService settings, FileIndexerService fileIndexer)
    {
        _logger = logger;
        _settings = settings;
        _fileIndexer = fileIndexer;
        _timer = new Timer(PerformIndexUpdate);
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.Information("BackgroundFileIndexer StartAsync");

        _timer.Change(TimeSpan.Zero,TimeSpan.FromMilliseconds(2000));
        return Task.CompletedTask;
    }

    private void PerformIndexUpdate(object? state)
    {
        _logger.Information("BackgroundFileIndexer PerformIndexUpdate");
        try
        {
            if( !_settings.TryGet<string>("mediaPath", out var mediaPath) || mediaPath == null)
            {
                _logger.Warning("invalid media path: {@MediaPath}", mediaPath);
                _timer.Change(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
                return;
            }
            
            if(_fileIndexer.IsRunning){
                _logger.Information("fileIndexer is still running");
                _timer.Change(TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(60));

                return;
            }

            _logger.Information("perform index update for path: {@MediaPath}", mediaPath);
            if(!_fileIndexer.Run(mediaPath))
            {
                _logger.Warning("fileIndexer run failed");
                _timer.Change(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
            } else {
                _logger.Information("fileIndexer run succeeded");
            }
            
            // todo: introduce a setting for rerunning a timer?! or just add filesystem watchers
            _timer.Change(TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(60));
        }
        catch(Exception e)
        {
            _logger.Error(e, "error running fileIndexer");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.Information("BackgroundFileIndexer StopAsync");
        _timer.Change(Timeout.Infinite, 0);
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _timer.Dispose();
    }
}