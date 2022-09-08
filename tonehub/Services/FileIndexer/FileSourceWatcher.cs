using System.Collections.ObjectModel;
using Microsoft.EntityFrameworkCore;
using Sandreas.Files;
using tonehub.Database;
using tonehub.Database.Models;
using tonehub.Metadata;

namespace tonehub.Services.FileIndexer;

public class FileSourceWatcher
{
    private Collection<FileSource> _currentSources = new();
    private readonly Collection<FileWatcher> _fileWatchers = new();
    private readonly ILogger _logger;
    private readonly AppDbContext _db;
    private readonly FileWalker _fw;
    private readonly IFileLoader _tagLoader;

    public FileSourceWatcher(ILogger logger, AppDbContext db, FileWalker fw, IFileLoader tagLoader)
    {
        _logger = logger;
        _db = db;
        _fw = fw;
        _tagLoader = tagLoader;
    }

    public async Task Start(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await _updateFileWatchers();
            
            /*
             *
             * 
             */
            
            Thread.Sleep(30000);
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
        
        

        var needsInsert = inserted.Concat(updated);
        var fileWatchersToInsert = needsInsert.Select(_createFileWatcher);


        foreach (var fw in fileWatchersToInsert)
        {
            fw.Start();
            _fileWatchers.Add(fw);
        }

        _currentSources.Clear();
        foreach (var a in all)
        {
            _currentSources.Add(a);
        }
    }

    private FileWatcher _createFileWatcher(FileSource source)
    {
        return new FileWatcher(_fw, _tagLoader, source.Location);
    }

    private async Task<(
        IReadOnlyCollection<FileSource> all,
        IReadOnlyCollection<FileSource> inserted,
        IReadOnlyCollection<FileSource> updated,
        IReadOnlyCollection<FileSource> deleted
        )> _compareSourcesAsync()
    {
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
}