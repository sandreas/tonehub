using System.IO.Abstractions;
using System.Text;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Sandreas.Files;
using SerilogTimings.Extensions;
using tonehub.Database;
using tonehub.Database.Models;
using tonehub.Metadata;
using tonehub.Mime;
using tonehub.Settings;
using FileModel = tonehub.Database.Models.File;
using ILogger = Serilog.ILogger;

namespace tonehub.Services;

public class FileIndexerService
{
    private readonly FileWalker _fileWalker;
    private readonly IFileLoader _tagLoader;
    private readonly IDbContextFactory<AppDbContext> _dbFactory;

    private DateTimeOffset? _indexingInProgressSince;
    private DateTimeOffset? _lastSuccessfulRun;
    private readonly FileExtensionContentTypeProvider _mimeDetector;
    private readonly FileIndexerSettings _settings;
    private readonly ILogger _logger;

    public FileIndexerService(ILogger logger, FileWalker fileWalker, FileExtensionContentTypeProvider mimeDetector,
        AudioFileLoader tagLoader, IDbContextFactory<AppDbContext> dbFactory,
        FileIndexerSettings settings)
    {
        _logger = logger;
        _fileWalker = fileWalker;
        _tagLoader = tagLoader;
        _dbFactory = dbFactory;
        _mimeDetector = mimeDetector;
        _settings = settings;
    }

    public bool IsRunning => _indexingInProgressSince != null;

    public bool Run(FileSource source, CancellationToken cancellationToken)
    {
        try
        {
            if (_indexingInProgressSince != null)
            {
                return true;
            }

            _indexingInProgressSince = DateTimeOffset.UtcNow;

            var files = _fileWalker.WalkRecursive(source.Location).SelectFileInfo().Where(_tagLoader.Supports)
                .ToArray();
            if (UpdateFileTags(files, source, cancellationToken) && DeleteOrphanedFileTags(source, cancellationToken))
            {
                _lastSuccessfulRun = DateTimeOffset.UtcNow;
                return true;
            }
            else
            {
                return false;
            }
        }
        finally
        {
            _indexingInProgressSince = null;
        }
    }

    private bool UpdateFileTags(IEnumerable<IFileInfo> files, FileSource source, CancellationToken cancellationToken)
    {
        using var operationIndexAllFiles =
            _logger.BeginOperation($"indexing files for source -  Id={source.Id}, Location={source.Location}");

        var i = 0;
        foreach (var file in files)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                _logger.Information("cancellation requested: file indexer update file tags");
                return false;
            }

            i++;
            var normalizedLocation = NormalizeLocationFromPath(source.Location, file);
            FileModel? fileRecord;
            using var operationIndexFile =
                _logger.BeginOperation("indexing file number {FileNumber}: {File}", i, normalizedLocation);

            try
            {

                _tagLoader.Initialize(file);

                using var operationHandle = _logger.BeginOperation("- handle file");
                // _debugList.Add($"==> _tagLoader.Initialize: {_stopWatch.Elapsed.TotalMilliseconds}");
                using var db = _dbFactory.CreateDbContext();
                db.FileSources.Attach(source);
                fileRecord = db.Files.FirstOrDefault(f => f.Location == normalizedLocation && f.Source.Id == source.Id);
                if (fileRecord == null)
                {
                    fileRecord = HandleMissingFile(db, source, file, normalizedLocation);
                }
                else
                {
                    HandleExistingFile(file, normalizedLocation, fileRecord);
                }

                operationHandle.Complete();

                //_debugList.Add($"==> fileRecord handling: {_stopWatch.Elapsed.TotalMilliseconds}");

                using var operationSave = _logger.BeginOperation("- save changes");

                // fileRecord.Source = source;
                if (fileRecord.IsNew)
                {
                    db.Files.Add(fileRecord);
                }
                else
                {
                    db.Files.Update(fileRecord);
                }

                //_debugList.Add($"==> files.Add/Update: {_stopWatch.Elapsed.TotalMilliseconds}");


                if (fileRecord.HasChanged)
                {
                    UpdateFileRecordTagsAndJsonValues(db, fileRecord);
                }
                else
                {
                    db.SaveChanges(); // here is the problem (allocation)
                }

                
                db.Dispose();

                operationSave.Complete();

                // _debugList.Add($"==> files.UpdateTags: {_stopWatch.Elapsed.TotalMilliseconds} (HasChanged={fileRecord.HasChanged.ToString()}, IsNew={fileRecord.IsNew.ToString()})");
                // _debugList.Add($"==> overall count: {overallCounter}");
                // _stopWatch.Stop();

                // Console.WriteLine(string.Join("\n", _debugList));
                operationIndexFile.Complete();
            }
            catch (Exception e)
            {
                // Console.WriteLine(e.Message);
                // op.Cancel(); // Cancel would suppress warning, so do not call it
                _logger.Warning(e, "file {File} could not be indexed", normalizedLocation);
                return false;
            }
        }


        operationIndexAllFiles.Complete("files", i);

        return true;
    }

    private void HandleExistingFile(IFileInfo file, string normalizedLocation, FileModel existingFileModel)
    {
        // file must always be marked as changed because of orphan detection
        if (existingFileModel.ModifiedDate < file.LastWriteTime)
        {
            existingFileModel.HasChanged = true;
            // if file has been modified, hash has to be recalculated, GlobalFilterType reset
            // because file could have been replaced
            existingFileModel.GlobalFilterType = _tagLoader.LoadGlobalFilterType();
            UpdateFileRecord(existingFileModel, file, normalizedLocation, "");
        }
        else
        {
            existingFileModel.LastCheckDate = DateTimeOffset.UtcNow;
        }
    }

    private FileModel HandleMissingFile(AppDbContext db, FileSource source, IFileInfo file, string normalizedLocation)
    {
        try
        {
            _logger.Information(" file {File},  size: {FileSize}MB", file.Name, Math.Round(file.Length * 1e-6, 3));
            var hash = BuildFullHashAsHexString();
            var existingRecord = db.Files.FirstOrDefault(f => f.Hash == hash && f.Source.Id == source.Id);
            return existingRecord == null
                ? CreateNewFileRecord(/*db.FileSources.Find(source.Id) ?? */ source, file, normalizedLocation, hash)
                : UpdateFileRecord(existingRecord, file, normalizedLocation, existingRecord.Hash);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }

    private void UpdateFileRecordTagsAndJsonValues(AppDbContext db, FileModel fileRecord)
    {
        fileRecord.FileTags = fileRecord.FileTags.Where(t => t.Type < IFileLoader.CustomTagTypeStart).ToList();

        var loadedRawTags = _tagLoader.LoadTags();

        // todo: performance improvements
        db.ChangeTracker.AutoDetectChangesEnabled = false;

        var tagsToStore = loadedRawTags.Select(t => new FileTag()
        {
            Namespace = t.Namespace,
            Type = t.Type,
            File = fileRecord,
            Tag = FindOrCreateTag(db, t.Value)
        });

        foreach (var t in tagsToStore)
        {
            fileRecord.FileTags.Add(t);
        }

        fileRecord.FileJsonValues =
            fileRecord.FileJsonValues.Where(t => t.Type < IFileLoader.CustomTagTypeStart).ToList();

        var loadedRawJsonValues = _tagLoader.LoadJsonValues();

        var jsonValuesToStore = loadedRawJsonValues.Select(t => new FileJsonValue()
        {
            Namespace = t.Namespace,
            Type = t.Type,
            File = fileRecord,
            Value = t.Value
        });
        foreach (var t in jsonValuesToStore)
        {
            fileRecord.FileJsonValues.Add(t);
        }

        // todo performance improvements
        db.ChangeTracker.AutoDetectChangesEnabled = false;

        db.SaveChanges();
    }


    private static Tag FindOrCreateTag(AppDbContext db, string tagValue)
    {
        // _logger.Information("Adding tag {TagValue}", tagValue.ToString());
        var existing = db.Tags.FirstOrDefault(t =>
            tagValue.Equals(t.Value)
        );
        if (existing != null)
        {
            // _logger.Information("Already exists");
            return existing;
        }

        // limit value bytes to 2500 (due to some indexes like pgsql supporting only 2704 bytes)
        while (Encoding.UTF8.GetByteCount(tagValue) > 2500)
        {
            tagValue = tagValue.Substring(0, tagValue.Length - 1);
        }

        var newTag = new Tag()
        {
            Value = tagValue
        };
        db.Tags.Add(newTag);
        db.SaveChanges();
        return newTag;
    }

    private string BuildFullHashAsHexString()
    {
        return Convert.ToHexString(_tagLoader.BuildHash());
    }

    private FileModel CreateNewFileRecord(FileSource source, IFileInfo file, string normalizedLocation, string hash)
    {
        var newFileRecord = new FileModel
        {
            Source = source,
            IsNew = true,
            GlobalFilterType = _tagLoader.LoadGlobalFilterType()
        };
        return UpdateFileRecord(newFileRecord, file, normalizedLocation, hash);
    }

    private FileModel UpdateFileRecord(FileModel fileRecord, IFileInfo file, string normalizedLocation, string hash)
    {
        if (fileRecord.MimeMediaType == "" || fileRecord.MimeSubType == "")
        {
            if (!TryLoadFileMimeType(file.FullName, out var mimeType))
            {
                throw new Exception("Could not fill record basics: MimeType fail");
            }

            fileRecord.MimeMediaType = mimeType.MediaType;
            fileRecord.MimeSubType = mimeType.SubType;
        }

        fileRecord.Hash = hash == "" ? BuildFullHashAsHexString() : hash;
        fileRecord.Location = normalizedLocation;
        fileRecord.Size = file.Length;
        fileRecord.ModifiedDate = DateTime.SpecifyKind(file.LastWriteTime, DateTimeKind.Utc);
        fileRecord.LastCheckDate = DateTimeOffset.UtcNow;
        fileRecord.Disabled = false;
        return fileRecord;
    }

    private static string NormalizeLocationFromPath(string mediaPath, IFileSystemInfo file)
    {
        var relPath = file.FullName.StartsWith(mediaPath)
            ? file.FullName.Substring(mediaPath.Length)
            : file.FullName;
        return relPath.Replace(Path.DirectorySeparatorChar, '/').TrimStart('/');
    }

    private bool DeleteOrphanedFileTags(FileSource source, CancellationToken cancellationToken)
    {
        using var db = _dbFactory.CreateDbContext();

        try
        {
            var enabledFromDate = _indexingInProgressSince;
            // todo: sqlite does not allow query evaluation... must be client side.
            var disableFiles = db.Files.AsEnumerable().Where(f => f.LastCheckDate < enabledFromDate && f.Source == source).ToList();
            foreach (var fileRecord in disableFiles)
            {
                fileRecord.Disabled = true;
            }

            db.Files.UpdateRange(disableFiles);

            var keepFromDate = _indexingInProgressSince?.Subtract(_settings.DeleteOrphansAfter) ?? DateTime.MinValue;
            var deleteFiles = db.Files.AsEnumerable().Where(f => f.LastCheckDate < keepFromDate && f.Source == source).ToList();
            _deleteFiles(db, deleteFiles);
            if (cancellationToken.IsCancellationRequested)
            {
                _logger.Information("cancellation requested: deleting orphaned files (entities)");
            }
            db.SaveChanges();

            var orphanTags = db.Tags.Where(t => t.FileTags.Count == 0);
            db.Tags.RemoveRange(orphanTags);
            if (cancellationToken.IsCancellationRequested)
            {
                _logger.Information("cancellation requested: deleting orphaned files (tags)");
            }

            db.SaveChanges();
        }
        catch (Exception e)
        {
            _logger.Warning(e, "error while deleting orphaned files");
            return false;
        }

        return true;
    }

    private void _deleteFiles(AppDbContext db, List<FileModel> deleteFiles)
    {
        foreach (var fileRecord in deleteFiles)
        {
            db.FileTags.RemoveRange(fileRecord.FileTags);
            db.FileJsonValues.RemoveRange(fileRecord.FileJsonValues);
        }

        db.RemoveRange(deleteFiles);
    }

    
    

    private bool TryLoadFileMimeType(string path, out MimeType mimeType)
    {
        mimeType = new MimeType();
        if (!_mimeDetector.TryGetContentType(path, out string? contentType))
        {
            // _logger.Warning("Could not get content type of path {Path}", path);
            return false;
        }

        if (mimeType.TryParseString(contentType))
        {
            return true;
        }

        // _logger.Warning("Invalid mimetype value {ContentType}", contentType);
        return false;
    }
}