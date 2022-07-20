using System.IO.Abstractions;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Sandreas.Files;
using tonehub.Database;
using tonehub.Database.Models;
using tonehub.Metadata;
using tonehub.Mime;
using FileModel = tonehub.Database.Models.File;

namespace tonehub.Services;

public class FileIndexerService
{
    private readonly FileWalker _fileWalker;
    private readonly IFileTagLoader _tagLoader;
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    
    private DateTimeOffset? _indexingInProgressSince;
    private DateTimeOffset? _lastSuccessfulRun;
    private readonly AudioHashBuilder _hashBuilder;
    private readonly FileExtensionContentTypeProvider _mimeDetector;

    public FileIndexerService(FileWalker fileWalker, FileExtensionContentTypeProvider mimeDetector, AudioFileTagLoader tagLoader, AudioHashBuilder hashBuilder, IDbContextFactory<AppDbContext> dbFactory)
    {
        _fileWalker = fileWalker;
        _tagLoader = tagLoader;
        _hashBuilder = hashBuilder;
        _dbFactory = dbFactory;
        _mimeDetector = mimeDetector;
    }
    
    public void Run(string mediaPath) {
        try
        {
            if(_indexingInProgressSince != null)
            {
                return;
            }
            _indexingInProgressSince = DateTimeOffset.UtcNow;
            
            var files = _fileWalker.WalkRecursive(mediaPath).SelectFileInfo().Where(_tagLoader.Supports).ToArray();
            using var db = _dbFactory.CreateDbContext();
            UpdateFileTags(db, files, mediaPath);
            DeleteOrphanedFileTags(db, files);
            
            _lastSuccessfulRun = DateTimeOffset.UtcNow;
        }
        finally
        {
            _indexingInProgressSince = null;
        }

    }
    
    private void UpdateFileTags(AppDbContext db, IEnumerable<IFileInfo> files, string mediaPath)
    {
        foreach(var file in files)
        {
            var normalizedLocation = NormalizeLocationFromPath(mediaPath, file);
            var existingFile = db.Files.FirstOrDefault(f => f.Location == normalizedLocation);
            if(existingFile == null)
            {
                existingFile = HandleMissingFile(db, file, normalizedLocation);
            } else {
                HandleExistingFile(file, normalizedLocation, existingFile);
            }
            
            if(existingFile.IsNew)
            {
                db.Files.Add(existingFile);
            } else {
                db.Files.Update(existingFile);
            }
            
            if(existingFile.HasChanged)    {
                UpdateFileRecordTagsAndJsonValues(db, existingFile, file);
            }
        }
    }

    private void HandleExistingFile(IFileInfo file, string normalizedLocation, FileModel existingFileModel)
    {

        // file must always be marked as changed because of orphan detection
        existingFileModel.HasChanged = true;
        if(existingFileModel.ModifiedDate < file.LastWriteTime)
        {
            // if file has been modified, hash has to be recalculated, GlobalFilterType reset
            // because file could have been replaced
            existingFileModel.GlobalFilterType = _tagLoader.LoadGlobalFilterType(file);
            UpdateFileRecord(existingFileModel, file, normalizedLocation, "");
        } else
        {
            existingFileModel.LastCheckDate = DateTimeOffset.UtcNow;
        }
    }

    private FileModel HandleMissingFile(AppDbContext db, IFileInfo file, string normalizedLocation)
    {
        try
        {
            var hash = BuildFullHashAsHexString(file);
            var existingRecord = db.Files.FirstOrDefault(f => f.Hash == hash);
            return existingRecord == null ? CreateNewFileRecord(file, normalizedLocation, hash) : UpdateFileRecord(existingRecord, file, normalizedLocation, existingRecord.Hash);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }

    }

    private void UpdateFileRecordTagsAndJsonValues(AppDbContext db, FileModel fileRecord, IFileInfo file)
    {
        fileRecord.FileTags = fileRecord.FileTags.Where(t => t.Type < IFileTagLoader.CustomTagTypeStart).ToList();

        var loadedRawTags = _tagLoader.LoadTags(file);
        
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
        
        fileRecord.FileJsonValues = fileRecord.FileJsonValues.Where(t => t.Type < IFileTagLoader.CustomTagTypeStart).ToList();

        var loadedRawJsonValues = _tagLoader.LoadJsonValues(file);
        
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

        var newTag = new Tag()
        {
            Value = tagValue
        };
        db.Tags.Add(newTag);
        db.SaveChanges();
        return newTag;
    }

    private string BuildFullHashAsHexString(IFileInfo file)
    {
        return Convert.ToHexString(_hashBuilder.BuildFullHash(file));
    }
    
    private FileModel CreateNewFileRecord(IFileInfo file, string normalizedLocation, string hash)
    {
        var newFileRecord = new FileModel {
            IsNew = true,
            GlobalFilterType = _tagLoader.LoadGlobalFilterType(file)
        };
        return UpdateFileRecord(newFileRecord, file, normalizedLocation, hash);
    }

    private FileModel UpdateFileRecord(FileModel fileRecord, IFileInfo file, string normalizedLocation, string hash)
    {
        if(fileRecord.MimeMediaType == "" || fileRecord.MimeSubType == ""){
            if(!TryLoadFileMimeType(file.FullName, out var mimeType))
            {
                throw new Exception("Could not fill record basics: MimeType fail");
            }
            fileRecord.MimeMediaType = mimeType.MediaType;
            fileRecord.MimeSubType = mimeType.SubType;
        }
        
        fileRecord.Hash = hash == "" ? BuildFullHashAsHexString(file) : hash;
        fileRecord.Location = normalizedLocation;
        fileRecord.Size = file.Length;
        fileRecord.ModifiedDate = file.LastWriteTime;
        fileRecord.LastCheckDate = DateTimeOffset.UtcNow;
        return fileRecord;
    }
    
    private static string NormalizeLocationFromPath(string mediaPath, IFileSystemInfo file)
    {
        var relPath = file.FullName.StartsWith(mediaPath)
            ? file.FullName.Substring(mediaPath.Length)
            : file.FullName;
        return relPath.Replace(Path.DirectorySeparatorChar, '/').TrimStart('/');
    }
    
    private void DeleteOrphanedFileTags(AppDbContext db, IFileInfo[] files)
    {
        // todo: Disable files first, dependant on setting: deleteOrphansAfterSeconds
        // var deleteFiles = db.Files.Where(f => f.UpdatedDate <= _indexingInProgressSince).ToList();
        var deleteFiles = db.Files.AsEnumerable().Where(f => f.LastCheckDate < _indexingInProgressSince).ToList();
        foreach(var fileRecord in deleteFiles){
            db.FileTags.RemoveRange(fileRecord.FileTags);
            db.FileJsonValues.RemoveRange(fileRecord.FileJsonValues);
        }
        db.RemoveRange(deleteFiles);
        db.SaveChanges();
        
        var orphanTags = db.Tags.Where(t => t.FileTags.Count == 0);
        db.Tags.RemoveRange(orphanTags);
        db.SaveChanges();
        /*
        var param = new NpgsqlParameter("@LastIndexerRun", LastIndexerRun);
            
        
        var sql = "DELETE FROM FileTag WHERE FileId IN (SELECT Id FROM Files WHERE UpdateDate < @LastIndexerRun)";
        _ = db.Database.ExecuteSqlRaw(sql, param);
        
        sql = "DELETE FROM Files WHERE UpdateDate < @LastIndexerRun";
        var removedItemCount = db.Database.ExecuteSqlRaw(sql, param);
*/

        // _logger.Information("found {DirtyRecordCount} to remove from database", removedItemCount);
        //db.RemoveRange(toRemove);
        //db.SaveChanges();
        /*
        var toRemove = db.Files.Where(f => f.UpdatedDate < _indexingInProgressSince);
        foreach(var fileRecord in toRemove)        {
            db.FileTags.RemoveRange(fileRecord.FileTags);
            db.FileJsonValues.RemoveRange(fileRecord.FileJsonValues);
        }
        // _logger.Information("found {DirtyRecordCount} to remove from database", removedItemCount);
        db.RemoveRange(toRemove);
        db.SaveChanges();

        var orphanTags = db.Tags.Where(t => t.FileTags.Count == 0);
        db.Tags.RemoveRange(orphanTags);
        db.SaveChanges();
        */
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