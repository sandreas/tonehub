using System.Collections.Concurrent;
using System.IO.Abstractions;
using System.Text;
using JsonApiDotNetCore.Services;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;
using tonehub.Database;
using tonehub.Database.Models;
using tonehub.Extensions;
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

        var dbChangedFiles = _db
            .Files
            .Where(f => f.Source == source && locations.Contains(f.Location))
            .Include(f => f.FileJsonValues)
            .Include(f => f.FileTags)
            .ThenInclude(ft => ft.Tag)
            .AsSplitQuery();
        
        var newFiles = FilterNewFiles(locations, dbChangedFiles, filesArray);
        var dbNewFiles = CreateNewFileEntities(source, newFiles);
        _db.UpdateRange(dbNewFiles);
        _db.UpdateRange(dbChangedFiles);

        await UpdateChangedFileEntities(dbChangedFiles, locationFileMapping);
    }

    private IEnumerable<File> CreateNewFileEntities(FileSource fileSource, List<IFileInfo> newFiles)
    {
        return newFiles.Select(f => CreateNewFileEntity(fileSource, f));
    }

    private File CreateNewFileEntity(FileSource source, IFileInfo file)
    {
        var newFileRecord = new File
        {
            Source = source,
            IsNew = true,
            GlobalFilterType = _tagLoader.LoadGlobalFilterType()
        };
        return UpdateFileEntity(newFileRecord, file, NormalizeLocationFromPath(source.Location, file), "");
    }

    private async Task UpdateChangedFileEntities(IQueryable<File> dbChangedFiles,
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
                await UpdateFileRecordTagsAndJsonValues(existingFileModel);
            }
            else
            {
                existingFileModel.LastCheckDate = DateTimeOffset.UtcNow;
            }
        }
    }

    private async Task UpdateFileRecordTagsAndJsonValues(File fileRecord)
    {
        var loadedRawTags = _tagLoader.LoadTags().Select(t =>
        {
            t.Value = ShortenOverlongTagValue(t.Value);
            return t;
        }).ToList();

        var tagValues = loadedRawTags.Select(t => t.Value);
        var trackedTags = _db.ChangeTracker.Entries<Tag>().Where(t => tagValues.Contains(t.Entity.Value))
            .Select(te => te.Entity);
        var untrackedTagValues = tagValues.Where(v => trackedTags.All(tt => tt.Value != v));
        var untrackedTags = _db.Tags.Where(t => untrackedTagValues.Contains(t.Value));
        var newTagValues = untrackedTagValues.Where(utv => untrackedTags.All(ut => ut.Value != utv));
        var newTags = newTagValues.Select(v => new Tag
        {
            Value = v
        }).ToList();


        ConcurrentDictionary<string, Tag> fileTagReference =
            new(trackedTags.Concat(untrackedTags).Concat(newTags).ToDictionary(t => t.Value, t => t));

        var newFileTags = new List<FileTag>();
        var keepFileTags = new List<FileTag>();
        foreach (var (ns, type, value) in loadedRawTags)
        {
            var fileTag = fileRecord.FileTags.FirstOrDefault(ft =>
                ns == ft.Namespace
                && type == ft.Type
                && value == ft.Tag.Value);

            if (fileTag == null)
            {
                // here every tag entity should already be in the reference and the new Tag branch should never happen
                var tag = fileTagReference.ContainsKey(value)
                    ? fileTagReference[value]
                    : new Tag
                    {
                        Value = value
                    };

                var newFileTag = new FileTag()
                {
                    Namespace = ns,
                    Type = type,
                    File = fileRecord,
                    Tag = tag
                };
                newFileTags.Add(newFileTag);
            }
            else
            { 
                keepFileTags.Add(fileTag);
            }
        }
        // todo: maybe this must be done before the foreach loop
        await _db.Tags.AddRangeAsync(newTags);
        await _db.FileTags.AddRangeAsync(newFileTags);
        await fileRecord.FileTags.AddRangeAsync(newFileTags.ToAsyncEnumerable());
        
        // keep custom tags, that are not automatically defined by tagloaders
        var fileTagsToRemove =
            fileRecord.FileTags.Where(f => !keepFileTags.Contains(f) && f.Type < IFileLoader.CustomTagTypeStart);
        foreach (var fileTagToRemove in fileTagsToRemove)
        {
            fileRecord.FileTags.Remove(fileTagToRemove);
        }
        
        var loadedRawJsonValues = _tagLoader.LoadJsonValues();
        
        var keep = new List<FileJsonValue>();
        var add = new List<FileJsonValue>();
        foreach(var (ns, type, jtoken) in loadedRawJsonValues)
        {
            var fileJsonValue = fileRecord.FileJsonValues.FirstOrDefault(ft =>
                ns == ft.Namespace
                && type == ft.Type
                && JToken.DeepEquals(jtoken, ft.Value));
            if(fileJsonValue == null){
                add.Add(new FileJsonValue
                {
                    Namespace = ns,
                    Type = type,
                    File = fileRecord,
                    Value = jtoken
                });
            } else {
                keep.Add(fileJsonValue);
            }
        }
        await _db.FileJsonValues.AddRangeAsync(add);
        await fileRecord.FileJsonValues.AddRangeAsync(add.ToAsyncEnumerable());

        var fileJsonValuesToRemove =
            fileRecord.FileJsonValues.Where(f => !keep.Contains(f) && f.Type < IFileLoader.CustomTagTypeStart).ToList();
        foreach (var fileTagToRemove in fileJsonValuesToRemove)
        {
            fileRecord.FileJsonValues.Remove(fileTagToRemove);
        }
    }
    
    private static string ShortenOverlongTagValue(string tagValue)
    {
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