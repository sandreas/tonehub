using Microsoft.EntityFrameworkCore;
using tonehub.Database;
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
    private bool _indexingInProgress = false;
    private readonly CancellationTokenSource _cts;
    private readonly IDbContextFactory<AppDbContext> _dbFactory;

    public BackgroundFileIndexerService(ILogger logger, IDbContextFactory<AppDbContext> dbFactory, FileIndexerService fileIndexer)
    {
        _logger = logger;
        _dbFactory = dbFactory;
        _fileIndexer = fileIndexer;
        //_timer = new Timer(PerformIndexUpdate);
        _cts = new CancellationTokenSource();
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.Information("BackgroundFileIndexer StartAsync");
        // _timer.Change(TimeSpan.Zero,TimeSpan.FromSeconds(2));
        PerformIndexUpdate();
        return Task.CompletedTask;
    }

    private void PerformIndexUpdate()
    {
        try
        {
            _logger.Information("BackgroundFileIndexer PerformIndexUpdate");

            if(_indexingInProgress){
                _logger.Information("Indexing is still in progress, no index update required");
                return;
            }
            _indexingInProgress = true;

            using var db = _dbFactory.CreateDbContext();

            var sources = db.FileSources.Where(s => !s.Disabled).ToList();
            if (!sources.Any())
            {
                _logger.Information("No file sources configured or enabled - nothing to do");
                return;
            }
            

            foreach (var source in sources)
            {
                _logger.Information("perform index update for source - Id={SourceId}, Location={SourceLocation}", source.Id, source.Location);
                ;
                if (!_fileIndexer.Run(source, _cts.Token))
                {
                    if(_cts.IsCancellationRequested){
                        _logger.Information("fileIndexer run cancelled");
                    } else {
                        _logger.Warning("fileIndexer run failed");
                    }
                }
                else
                {
                    _logger.Information("fileIndexer run succeeded");
                }
            }
            
        }
        catch (Exception e)
        {
            _logger.Error(e, "error running fileIndexer");
        }
        finally
        {
            _indexingInProgress = false;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.Information("BackgroundFileIndexer StopAsync");
        // _timer.Change(Timeout.Infinite, 0);
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        // _timer.Dispose();
    }
}