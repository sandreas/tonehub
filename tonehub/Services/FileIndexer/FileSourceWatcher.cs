using System.Collections.ObjectModel;
using System.IO.Abstractions;
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
    private readonly Collection<FileWatcher> _fileWatchers = new();
    private readonly ILogger _logger;
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly FileWalker _fw;
    private readonly IFileLoader _tagLoader;
    private readonly FileExtensionContentTypeProvider _mimeDetector;

    public FileSourceWatcher(ILogger logger, IDbContextFactory<AppDbContext> dbFactory, FileWalker fw, AudioFileLoader tagLoader,  FileExtensionContentTypeProvider mimeDetector)
    {
        _logger = logger;
        _dbFactory = dbFactory;
        _fw = fw;
        _tagLoader = tagLoader;
        _mimeDetector = mimeDetector;
    }

    public async Task StartAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await _updateFileWatchers();
            // await _processNextBatch();
            await Task.Delay(30000, stoppingToken);
        }
    }

    private async Task _updateFileWatchers()
    {
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
            
            // todo:
            // - start file watcher
            // - fullIndexOnce
            // - await ProcessNextBatch => only one db task at a time
            
            await fw.StartAsync();
            _fileWatchers.Add(fw);
        }

        // todo: 

    }

    private FileWatcher _createFileWatcher(FileSource source)
    {
        
        return new FileWatcher(_fw, source.Location, async items =>
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            var dbUpdater = new FileDatabaseUpdater(db, _tagLoader, _mimeDetector);
            await dbUpdater.ProcessBatchAsync(source.Id, items);
        }, _tagLoader.Supports);
    }

    private async Task<(
        IReadOnlyCollection<FileSource> all,
        IReadOnlyCollection<FileSource> inserted,
        IReadOnlyCollection<FileSource> updated,
        IReadOnlyCollection<FileSource> deleted
        )> _compareSourcesAsync()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var dbSources = db.FileSources.AsNoTracking().ToList();
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
}