using System.IO.Abstractions;
using Microsoft.EntityFrameworkCore;
using tonehub.Database;
using tonehub.Database.Models;
using tonehub.Services;
using FileModel = tonehub.Database.Models.File;
using ILogger = Serilog.ILogger;

namespace tonehub.HostedServices;

public class BackgroundFileIndexerService : IHostedService, IDisposable
{
    private readonly Timer _timer;

    // private readonly DatabaseSettingsService _settings;
    private readonly FileIndexerService _fileIndexer;
    private readonly ILogger _logger;
    private CancellationTokenSource _cts;
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private List<IFileSystemWatcher> _watchers = new();
    private readonly FileSystem _fs;

    private Queue<FileSource> _todo = new();
    private Queue<FileSource> _workInProgress = new();

    public BackgroundFileIndexerService(ILogger logger, FileSystem fs, IDbContextFactory<AppDbContext> dbFactory,
        FileIndexerService fileIndexer)
    {
        _logger = logger;
        _fs = fs;
        _dbFactory = dbFactory;
        _fileIndexer = fileIndexer;
        _timer = new Timer(ExecuteTimer);
        _cts = new CancellationTokenSource();
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.Information("BackgroundFileIndexer StartAsync");
        _timer.Change(TimeSpan.Zero, TimeSpan.FromMinutes(20));
        return Task.CompletedTask;
    }

    private List<FileSource> _loadFileSources()
    {
        using var db = _dbFactory.CreateDbContext();
        return db.FileSources.AsNoTracking().Where(s => !s.Disabled).ToList();
    }

    private void ExecuteTimer(object? state)
    {
        var sources = _loadFileSources();
        if (!sources.Any())
        {
            _logger.Information("No file sources configured or enabled - nothing to do");
            return;
        }

        _updateFilesystemWatchers(sources);
        _updateWorkQueues(sources);
        _processQueue();
    }

    private void _updateFilesystemWatchers(List<FileSource> sources)
    {
        foreach (var watcher in _watchers)
        {
            if (sources.All(s => s.Location != watcher.Path))
            {
                _watchers.Remove(watcher);
                watcher.Dispose();
            }
        }

        foreach (var source in sources)
        {
            if (_watchers.All(w => w.Path != source.Location))
            {
                var watcher = _fs.FileSystemWatcher.CreateNew();
                watcher.Path = source.Location;
                watcher.IncludeSubdirectories = true;
                watcher.Filter = "*.*";
                watcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.CreationTime |
                                       NotifyFilters.DirectoryName;

                // watcher.Created += new FileSystemEventHandler(OnCreated);
                // var changeHandler = (object sender, FileSystemEventArgs e) => ReindexSource(source);
                void OnWatcherDetectedChange(object sender, FileSystemEventArgs fileSystemEventArgs) =>
                    OnFileSystemChange(source, sender, fileSystemEventArgs);


                watcher.Changed += OnWatcherDetectedChange;
                watcher.Created += OnWatcherDetectedChange;
                watcher.Deleted += OnWatcherDetectedChange;
                watcher.Renamed += OnWatcherDetectedChange;
                watcher.EnableRaisingEvents = true;
                _logger.Information("adding filesystem watcher for {SourceLocation}", source.Location);
                _watchers.Add(watcher);
            }
        }
    }

    private void OnFileSystemChange(FileSource source, object sender, FileSystemEventArgs fileSystemEventArgs)
    {
        _logger.Information("Detected filesystem change in {SourceLocation}: {Sender}, {Type}, {Name}, {FullPath}, ",
           sender, source.Location, fileSystemEventArgs.ChangeType, fileSystemEventArgs.Name, fileSystemEventArgs.FullPath);

        _enqueueFileSource(source, true);
        _processQueue();
    }


    private void _updateWorkQueues(List<FileSource> sources)
    {
        foreach (var source in sources)
        {
            _enqueueFileSource(source);
        }
    }


    private void _enqueueFileSource(FileSource source, bool force = false)
    {
        if (_todo.Contains(source))
        {
            return;
        }

        if (_workInProgress.Contains(source))
        {
            // if still running by a periodic timer, usually it is not a good idea to cancel
            // only detected changes should end in a cancel of the current run
            if (!force)
            {
                return;
            }

            _cts.Cancel();
            _ = _workInProgress.Dequeue();
            _cts = new CancellationTokenSource();
        }

        _todo.Enqueue(source);
    }


    private void _processQueue()
    {
        if (_workInProgress.Count > 0)
        {
            _logger.Information("!!!! workInProgress > 0");

            return;
        }

        while (_todo.Count > 0)
        {
            try
            {
                var source = _todo.Dequeue();
                _workInProgress.Enqueue(source);

                _logger.Information("perform index update for source - Id={SourceId}, Location={SourceLocation}",
                    source.Id, source.Location);
                if (!_fileIndexer.Run(source, _cts.Token))
                {
                    if (_cts.IsCancellationRequested)
                    {
                        _logger.Information("fileIndexer run cancelled");
                    }
                    else
                    {
                        _logger.Warning("fileIndexer run failed");
                    }
                }
                else
                {
                    _logger.Information("fileIndexer run succeeded");
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
            finally
            {
                _ = _workInProgress.Dequeue();
            }
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