using System.Collections.Concurrent;
using System.IO.Abstractions;
using System.Text;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using tonehub.Database;
using tonehub.Database.Models;
using tonehub.Metadata;
using tonehub.Mime;
using File = tonehub.Database.Models.File;

namespace tonehub.Services.FileIndexer;

public class FileDatabaseUpdater
{
    private readonly AppDbContext _db;
    private readonly IFileLoader _tagLoader;
    private readonly FileExtensionContentTypeProvider _mimeDetector;

    public FileDatabaseUpdater(AppDbContext db, IFileLoader tagLoader, FileExtensionContentTypeProvider mimeDetector)
    {
        _db = db;
        _tagLoader = tagLoader;
        _mimeDetector = mimeDetector;
    }

    public async Task ProcessBatchAsync<TId>(TId sourceId, IEnumerable<IFileInfo> files)
    {
        var source = await _db.FileSources.FindAsync(sourceId);
        if (source == null)
        {
            return;
        }

        var filesArray = files.ToArray();
        var locationFileMapping =
            new ConcurrentDictionary<string, IFileInfo>(
                filesArray.ToDictionary(f => NormalizeLocationFromPath(source.Location, f), f => f));
        var locations = locationFileMapping.Keys.ToArray();
        // var locations = filesArray.Select(f =>  NormalizeLocationFromPath(source.Location, f)).ToArray();
        var dbChangedFiles = _db
            .Files
            .Where(f => f.Source == source && locations.Contains(f.Location))
            .Include(f => f.FileJsonValues)
            .Include(f => f.FileTags)
            .ThenInclude(ft => ft.Tag)
            .AsSplitQuery();

        // _db.UpdateRangeAsync(dbChangedFiles);
        // _db.U

        // for tags:
        // _db.ChangeTracker.Entries<Tag>().Where(t => t.Entity.Value == "test");
        /*
    {
        Console.WriteLine(
            $"Found {entityEntry.Metadata.Name} entity with ID {entityEntry.Property(e => e.Id).CurrentValue}");
    }
    */

        var newFiles = FilterNewFiles(locations, dbChangedFiles, filesArray);
        var dbNewFiles = CreateNewFileEntities(source, newFiles);
        _db.UpdateRange(dbNewFiles);
        _db.UpdateRange(dbChangedFiles);
        // var x = _db.FindAsync(1)

        UpdateChangedFileEntities(dbChangedFiles, locationFileMapping);
    }

    private  IEnumerable<File> CreateNewFileEntities(FileSource fileSource, List<IFileInfo> newFiles)
    {
        return newFiles.Select(f => CreateNewFileEntity(fileSource, f));
    }

    private  File CreateNewFileEntity(FileSource source, IFileInfo file)
    {
        var newFileRecord = new File
        {
            Source = source,
            IsNew = true,
            GlobalFilterType = _tagLoader.LoadGlobalFilterType()
        };
        return UpdateFileEntity(newFileRecord, file, NormalizeLocationFromPath(source.Location, file), "");
    }

    private void UpdateChangedFileEntities(IQueryable<File> dbChangedFiles,
        ConcurrentDictionary<string, IFileInfo> locationFileMapping)
    {
        foreach (var existingFileModel in dbChangedFiles)
        {
            var file = locationFileMapping[existingFileModel.Location];
            if (existingFileModel.ModifiedDate < file.LastWriteTime)
            {
                existingFileModel.HasChanged = true;
                // if file has been modified, hash has to be recalculated, GlobalFilterType reset
                // because file could have been replaced
                existingFileModel.GlobalFilterType = _tagLoader.LoadGlobalFilterType();
                UpdateFileEntity(existingFileModel, file, existingFileModel.Location, "");
                UpdateFileRecordTagsAndJsonValues(existingFileModel, file);
            }
            else
            {
                existingFileModel.LastCheckDate = DateTimeOffset.UtcNow;
            }
        }
    }

    private void UpdateFileRecordTagsAndJsonValues(File fileRecord, IFileInfo file)
    {
        
        // dbtodo: remove db parameter to prevent memory leak?
        var loadedRawTags = _tagLoader.LoadTags().Select(t =>
        {
            t.Value = ShortenOverlongTagValue(t.Value);
            return t;
        }).ToList();

        var add = new List<FileTag>();
        var keep = new List<FileTag>();
        foreach(var (ns, type, value) in loadedRawTags)
        {
            var fileTag = fileRecord.FileTags.FirstOrDefault(ft =>
                ns == ft.Namespace
                && type == ft.Type
                && value == ft.Tag.Value);
            
            if(fileTag == null){
                // new
                var newFileTag = new FileTag();
                add.Add(newFileTag);
            } else {
                // keep
                keep.Add(fileTag);
            }
        }

        // keep custom tags, that are not automatically loaded
        var fileTagsToRemove = fileRecord.FileTags.Where(f => !keep.Contains(f) && f.Type < IFileLoader.CustomTagTypeStart);
        foreach(var fileTagToRemove in fileTagsToRemove)
        {
            fileRecord.FileTags.Remove(fileTagToRemove);
        }
        
        // todo: 
        // addFileTags

        /*
        
        fileRecord.FileTags = fileRecord.FileTags.Where(t => t.Type < IFileLoader.CustomTagTypeStart).ToList();


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
*/
        // todo performance improvements
        // db.ChangeTracker.AutoDetectChangesEnabled = false;
    }
    private static string ShortenOverlongTagValue(string tagValue) {
        // limit value bytes to 2500 (due to some indexes like pgsql supporting only 2704 bytes)
        while (Encoding.UTF8.GetByteCount(tagValue) > 2500)
        {
            tagValue = tagValue.Substring(0, tagValue.Length - 1);
        }

        return tagValue;
    }

    private File UpdateFileEntity(File fileRecord, IFileInfo file, string normalizedLocation, string hash)
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

    private string BuildFullHashAsHexString()
    {
        return Convert.ToHexString(_tagLoader.BuildHash());
    }

    private static List<IFileInfo> FilterNewFiles(string[] locations, IQueryable<File> dbChangedFiles,
        IFileInfo[] filesArray)
    {
        var newFiles = new List<IFileInfo>();
        for (var i = 0; i < locations.Length; i++)
        {
            var location = locations[i];

            if (dbChangedFiles.Any(f => f.Location == location))
            {
                continue;
            }

            newFiles.Add(filesArray[i]);
        }

        return newFiles;
    }

    private static string NormalizeLocationFromPath(string mediaPath, IFileSystemInfo file)
    {
        var relPath = file.FullName.StartsWith(mediaPath)
            ? file.FullName[mediaPath.Length..]
            : file.FullName;
        return relPath.Replace(Path.DirectorySeparatorChar, '/').TrimStart('/');
    }
}