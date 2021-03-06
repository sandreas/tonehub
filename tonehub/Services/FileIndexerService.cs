using System.Diagnostics;
using System.IO.Abstractions;
using System.Text;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Sandreas.Files;
using tonehub.Database;
using tonehub.Database.Models;
using tonehub.Metadata;
using tonehub.Mime;
using tonehub.Settings;
using FileModel = tonehub.Database.Models.File;

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
    private List<string> _debugList = new();
    private Stopwatch _stopWatch;

    public FileIndexerService(FileWalker fileWalker, FileExtensionContentTypeProvider mimeDetector,
        AudioFileLoader tagLoader, IDbContextFactory<AppDbContext> dbFactory,
        FileIndexerSettings settings)
    {
        _fileWalker = fileWalker;
        _tagLoader = tagLoader;
        _dbFactory = dbFactory;
        _mimeDetector = mimeDetector;
        _settings = settings;
        _stopWatch = new Stopwatch();
    }

    public bool IsRunning => _indexingInProgressSince != null;
    
    public bool Run(string mediaPath)
    {
        try
        {
            if (_indexingInProgressSince != null)
            {
                return true;
            }

            _indexingInProgressSince = DateTimeOffset.UtcNow;

            var files = _fileWalker.WalkRecursive(mediaPath).SelectFileInfo().Where(_tagLoader.Supports).ToArray();
            if (UpdateFileTags(files, mediaPath) && DeleteOrphanedFileTags())
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

    private bool UpdateFileTags(IEnumerable<IFileInfo> files, string mediaPath)
    {

        var overallCounter = 0;
        foreach (var file in files)
        {
            overallCounter++;
            _stopWatch.Reset();
            _stopWatch.Start();
            _debugList = new List<string>();
            var normalizedLocation = NormalizeLocationFromPath(mediaPath, file);
            FileModel? fileRecord;
            try
            {
                
                _tagLoader.Initialize(file);
                _debugList.Add($"==> _tagLoader.Initialize: {_stopWatch.Elapsed.TotalMilliseconds}");
                using var db = _dbFactory.CreateDbContext();
                
                fileRecord = db.Files.FirstOrDefault(f => f.Location == normalizedLocation);
                if (fileRecord == null)
                {
                    fileRecord = HandleMissingFile(db, file, normalizedLocation);
                }
                else
                {
                    HandleExistingFile(file, normalizedLocation, fileRecord);
                }
                _debugList.Add($"==> fileRecord handling: {_stopWatch.Elapsed.TotalMilliseconds}");


                if (fileRecord.IsNew)
                {
                    db.Files.Add(fileRecord);
                }
                else
                {
                    db.Files.Update(fileRecord);
                }
                
                _debugList.Add($"==> files.Add/Update: {_stopWatch.Elapsed.TotalMilliseconds}");


                if (fileRecord.HasChanged)
                {
                    UpdateFileRecordTagsAndJsonValues(db, fileRecord);
                } else
                {
                    db.SaveChanges();
                }
                _debugList.Add($"==> files.UpdateTags: {_stopWatch.Elapsed.TotalMilliseconds} (HasChanged={fileRecord.HasChanged.ToString()}, IsNew={fileRecord.IsNew.ToString()})");
                _debugList.Add($"==> overall count: {overallCounter}");
                _stopWatch.Stop();

                Console.WriteLine(string.Join("\n", _debugList));
                
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return false;
            }
        }

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

    private FileModel HandleMissingFile(AppDbContext db, IFileInfo file, string normalizedLocation)
    {
        try
        {
            _debugList.Add($"==> filesize: {file.Length * 1e-6}MB");

            var hash = BuildFullHashAsHexString();
            var existingRecord = db.Files.FirstOrDefault(f => f.Hash == hash);
            return existingRecord == null
                ? CreateNewFileRecord(file, normalizedLocation, hash)
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
        // db.ChangeTracker.AutoDetectChangesEnabled = false;
        
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
        // db.ChangeTracker.AutoDetectChangesEnabled = false;
        
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
        _debugList.Add($"==> begin hashing: {_stopWatch.Elapsed.TotalMilliseconds}");
        var result = Convert.ToHexString(_tagLoader.BuildHash());
        _debugList.Add($"==> finish hashing: {_stopWatch.Elapsed.TotalMilliseconds}");
        return result;
    }

    private FileModel CreateNewFileRecord(IFileInfo file, string normalizedLocation, string hash)
    {
        var newFileRecord = new FileModel
        {
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

    private bool DeleteOrphanedFileTags()
    {
        try
        {
            using var db = _dbFactory.CreateDbContext();

            var enabledFromDate = _indexingInProgressSince;
            // todo: sqlite does not allow query evaluation... must be client side.
            var disableFiles = db.Files.AsEnumerable().Where(f => f.LastCheckDate < enabledFromDate).ToList();
            foreach (var fileRecord in disableFiles)
            {
                fileRecord.Disabled = true;
            }

            db.Files.UpdateRange(disableFiles);

            var keepFromDate = _indexingInProgressSince?.Subtract(_settings.DeleteOrphansAfter) ?? DateTime.MinValue;
            var deleteFiles = db.Files.AsEnumerable().Where(f => f.LastCheckDate < keepFromDate).ToList();
            foreach (var fileRecord in deleteFiles)
            {
                db.FileTags.RemoveRange(fileRecord.FileTags);
                db.FileJsonValues.RemoveRange(fileRecord.FileJsonValues);
            }

            db.RemoveRange(deleteFiles);
            db.SaveChanges();

            var orphanTags = db.Tags.Where(t => t.FileTags.Count == 0);
            db.Tags.RemoveRange(orphanTags);
            db.SaveChanges();
        }
        catch (Exception e)
        {
            return false;
        }

        return true;
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