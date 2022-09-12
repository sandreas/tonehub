using System.Collections.ObjectModel;
using System.Diagnostics;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Sandreas.Files;
using tonehub.Database;
using tonehub.Database.Models;
using tonehub.Metadata;
using ILogger = Serilog.ILogger;


namespace tonehub.Services.FileIndexer;

public class FileSourceWatcher
{
    private Collection<FileSource> _currentSources = new();
    private readonly Collection<FileWatcher<Guid>> _fileWatchers = new();
    private readonly ILogger _logger;
    private readonly FileDatabaseUpdater _databaseUpdater;
    private readonly FileWalker _fw;
    private readonly IFileLoader _tagLoader;
    private readonly FileExtensionContentTypeProvider _mimeDetector;
    //private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly AppDbContext _db;

    public FileSourceWatcher(ILogger logger, AppDbContext db/*, IDbContextFactory<AppDbContext> dbFactory*/, FileDatabaseUpdater databaseUpdater, FileWalker fw, AudioFileLoader tagLoader,  FileExtensionContentTypeProvider mimeDetector)
    {
        _logger = logger;
        _databaseUpdater = databaseUpdater;
        _fw = fw;
        _tagLoader = tagLoader;
        _mimeDetector = mimeDetector;
        // _dbFactory = dbFactory;
        _db = db;
    }

    public async Task StartAsync(CancellationToken stoppingToken)
    {
        var stopWatch = new Stopwatch();
        
        while (!stoppingToken.IsCancellationRequested)
        {
            await _updateFileWatchers(stopWatch);
            await _processNextBatches();
            // await Task.Delay(1000, stoppingToken);
        }
        stopWatch.Stop();
    }



    private async Task _updateFileWatchers(Stopwatch stopWatch)
    {
        // refresh file watchers on first run and every x seconds
        if(stopWatch.IsRunning && stopWatch.Elapsed < TimeSpan.FromSeconds(30))
        {
            return;
        }
        stopWatch.Restart();
        
        var (all, inserted, updated, deleted) = await _compareSourcesAsync();

        var needsCancellation = updated.Concat(deleted).ToList();
        var fileWatchersToRemove = _fileWatchers.Where(f => needsCancellation.Any(s => s.Location == f.Location));

        foreach (var fw in fileWatchersToRemove)
        {
            fw.Cancel();
            _fileWatchers.Remove(fw);
        }
        
        _currentSources.Clear();
        foreach (var a in all)
        {
            _currentSources.Add(a);
        }
        
        var needsInsert = inserted.Concat(updated);
        var fileWatchersToInsert = needsInsert.Select(_createFileWatcher);
        
        foreach (var fw in fileWatchersToInsert)
        {
            await fw.StartAsync();
            _fileWatchers.Add(fw);
        }
    }

    private FileWatcher<Guid> _createFileWatcher(FileSource source)
    {
        return new FileWatcher<Guid>(_fw, source.Id, source.Location, _tagLoader.Supports);
    }

    private async Task<(
        IReadOnlyCollection<FileSource> all,
        IReadOnlyCollection<FileSource> inserted,
        IReadOnlyCollection<FileSource> updated,
        IReadOnlyCollection<FileSource> deleted
        )> _compareSourcesAsync()
    {
        //await using var db = await _dbFactory.CreateDbContextAsync();
        //var dbSources = db.FileSources.AsNoTracking().ToList();
        var dbSources = _db.FileSources.AsNoTracking().ToList();
        return await Task.FromResult(CompareLists(dbSources, _currentSources));
    }

    private static (
        IReadOnlyCollection<FileSource> all,
        IReadOnlyCollection<FileSource> inserted,
        IReadOnlyCollection<FileSource> updated,
        IReadOnlyCollection<FileSource> deleted
        ) CompareLists(
            IReadOnlyCollection<FileSource> newValues, IReadOnlyCollection<FileSource> oldValues)
    {
        var added = new List<FileSource>();
        var changed = new List<FileSource>();
        var removed = oldValues.Where(source => newValues.All(s => s.Id != source.Id)).ToList();

        foreach (var dbSource in newValues)
        {
            var existing = oldValues.FirstOrDefault(s => s.Id == dbSource.Id);

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

        return (newValues, added, changed, removed);
    }
    
    private async Task<bool> _processNextBatches()
    {
        var returnValue = false;
        foreach(var watcher in _fileWatchers)
        {
            // todo: should BatchSize be defined in FileWatcher? Or rather in _databaseUpdater?
            // Note: Database is the limiting factor here
            var nextBatch = (await watcher.GetNextBatchAsync()).ToArray();
            returnValue |= nextBatch.Any();
            await _databaseUpdater.ProcessBatchAsync(watcher.SourceId, nextBatch);
        }
        
        return returnValue;
    }
}