using System.IO.Abstractions;
using Microsoft.EntityFrameworkCore;
using tonehub.Database;
using tonehub.Database.Models;
using tonehub.Services;
using File = tonehub.Database.Models.File;

namespace tonehub.HostedServices.Scoped;

using ILogger = Serilog.ILogger;
using FileModel = File;

internal interface IScopedFileIndexerService
{
    Task DoWork(CancellationToken stoppingToken);
}

internal class ScopedFileIndexerService : IScopedFileIndexerService
{
    private readonly ILogger _logger;
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private CancellationTokenSource _cts;
    private List<FileSource> _lastQueuedSources = new();
    private List<IFileSystemWatcher> _watchers = new();

    private Queue<(FileSource Source, string? Path)> _todo = new();
    private bool _processQueuesRunning;
    private readonly FileIndexerService _fileIndexer;
    private readonly FileSystem _fs;

    public ScopedFileIndexerService(ILogger logger, IDbContextFactory<AppDbContext> dbFactory,
        FileIndexerService fileIndexer, FileSystem fs)
    {
        _logger = logger;
        _dbFactory = dbFactory;
        _fileIndexer = fileIndexer;
        _fs = fs;
        _cts = new CancellationTokenSource();
    }

    public async Task DoWork(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            _logger.Information("ScopedFileIndexerService is checking for FileSource changes");

            var dbSources = _loadFileSources();
            var (added, changed, removed) = CompareLists(dbSources, _lastQueuedSources);

            if (_updateQueues(dbSources, added, changed, removed))
            {
                _logger.Information("Changes detected - added:{AddedCount}, changed:{ChangedCount}, removed:{RemovedCount}, total:{DbSourcesCount}", added.Count, changed.Count, removed.Count, dbSources.Count);

                _processQueues(dbSources);
            }


            await Task.Delay(30000, stoppingToken);
        }
    }

    /*
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

        //_enqueueFileSource(source, true);
        //_processQueue();
    }
    */
    
    private void _processQueues(List<FileSource> dbSources)
    {
        _lastQueuedSources = dbSources;
        if (_processQueuesRunning)
        {
            return;
        }

        //Task.Run(() => {
            try
            {
                _processQueuesRunning = true;
                _fileIndexer.CleanUp(dbSources);
                while (!_cts.Token.IsCancellationRequested)
                {
                    _processNextTodoItem(_cts.Token);
                }
            }
            finally
            {
                _processQueuesRunning = false;
            }
        //}, _cts.Token);
    }

    private void _processNextTodoItem(CancellationToken ctsToken)
    {
        var (source, path) = _todo.Dequeue();
        _fileIndexer.Run(source, ctsToken, path);
    }

    private bool _updateQueues(List<FileSource> dbSources, List<FileSource> added, List<FileSource> changed,
        List<FileSource> removed)
    {
        var returnValue = false;
        // if a source has been added, current indexing does not need to be stopped but only appended
        if (added.Count > 0)
        {
            var nonExistingAdded =
                added.Where(source => !_todo.Any(t => t.Source.Id == source.Id && t.Path == null));
            foreach (var source in nonExistingAdded)
            {
                _todo.Enqueue((source, null));
            }

            returnValue = true;
        }

        // if a source has been changed or removed, all sources have to  be re-indexed
        if (changed.Count > 0 || removed.Count > 0)
        {
            // todo: changed only has to be cancelled, if not in todo but work in progress
            _cts.Cancel();
            _cts = new CancellationTokenSource();
            _todo.Clear();
            foreach (var source in dbSources)
            {
                _todo.Enqueue((source, null));
            }

            returnValue = true;
        }

        return returnValue;
    }

    private static (List<FileSource> added, List<FileSource> changed, List<FileSource> removed) CompareLists(
        List<FileSource> dbSources, List<FileSource> lastIndexedSources)
    {
        var added = new List<FileSource>();
        var changed = new List<FileSource>();
        var removed = lastIndexedSources.Where(source => dbSources.All(s => s.Id != source.Id)).ToList();

        foreach (var dbSource in dbSources)
        {
            var existing = lastIndexedSources.FirstOrDefault(s => s.Id == dbSource.Id);

            if (existing == null)
            {
                added.Add(dbSource);
                continue;
            }

            if (existing.Disabled != dbSource.Disabled || existing.Location != dbSource.Location ||
                existing.GlobalFilterType != dbSource.GlobalFilterType)
            {
                changed.Add(dbSource);
            }
        }

        return (added, changed, removed);
    }

    private List<FileSource> _loadFileSources()
    {
        using var db = _dbFactory.CreateDbContext();
        return db.FileSources.AsNoTracking().ToList();
    }
}