using Microsoft.EntityFrameworkCore;
using Sandreas.Files;
using tonehub.Database;
using tonehub.Metadata;
using tonehub.Services;
using FileModel = tonehub.Database.Models.File;

namespace tonehub.HostedServices;

public class BackgroundFileIndexerService: IHostedService, IDisposable
{
    private readonly Timer _timer;
    private readonly DatabaseSettingsService _settings;
    private readonly FileIndexerService _fileIndexer;

    public BackgroundFileIndexerService(DatabaseSettingsService settings, FileIndexerService fileIndexer)
    {
        _settings = settings;
        _fileIndexer = fileIndexer;
        _timer = new Timer(PerformIndexUpdate);
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {

        _timer.Change(TimeSpan.Zero,TimeSpan.FromMilliseconds(2000));
        Console.WriteLine("start file indexer");
        return Task.CompletedTask;
    }

    private void PerformIndexUpdate(object? state)
    {
        try
        {
            if( !_settings.TryGet<string>("mediaPath", out var mediaPath) || mediaPath == null)
            {
                Console.WriteLine("invalid media path: " + mediaPath);
                _timer.Change(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
                return;
            }
            
            if(_fileIndexer.IsRunning){
                Console.WriteLine("fileIndexer is still running");
                _timer.Change(TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(60));

                return;
            }
            
            Console.WriteLine("setting: " + mediaPath);
            Console.WriteLine("perform index update");
            if(!_fileIndexer.Run(mediaPath))
            {
                // todo remove
                Console.WriteLine("failed fileindexer run");
                _timer.Change(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
            } else {
                // todo remove
                Console.WriteLine("successful fileindexer run");
            }
            
            // todo: introduce a setting for rerunning a timer?! or just add filesystem watchers
            _timer.Change(TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(60));
        }
        catch(Exception e)
        {
            Console.WriteLine(e);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _timer.Change(Timeout.Infinite, 0);
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _timer.Dispose();
    }
}