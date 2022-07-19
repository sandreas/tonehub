using System.IO.Abstractions;
using System.Text;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Sandreas.Files;
using tonehub.Database;
using tonehub.Database.Models;
using tonehub.Metadata;
using tonehub.Mime;
using tonehub.StreamUtils;
using FileModel = tonehub.Database.Models.File;

namespace tonehub.Services;

public class FileIndexerService
{
    private readonly FileWalker _fileWalker;
    private readonly IFileTagLoader _tagLoader;
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    
    private bool _indexingInProgress;
    private DateTime _lastSuccessfulRun;
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
            if(_indexingInProgress)
            {
                return;
            }
            _indexingInProgress = true;
            var files = _fileWalker.WalkRecursive(mediaPath).SelectFileInfo().Where(_tagLoader.Supports).ToArray();
            using var db = _dbFactory.CreateDbContext();
            UpdateFileTags(db, files, mediaPath);
            DeleteOrphanedFileTags(db, files);
            _lastSuccessfulRun = DateTime.Now;
        } catch(Exception e)        {
            _indexingInProgress = false;
        }
        finally
        {
            _indexingInProgress = false;
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
                HandleMissingFile(db, file, normalizedLocation);
            } else {
                HandleExistingFile(db, file, normalizedLocation, existingFile);
            }
        }
    }

    private void HandleExistingFile(AppDbContext db, IFileInfo file, string normalizedLocation, FileModel existingFileModel)
    {
        if(existingFileModel.UpdatedDate < file.LastWriteTime){
            UpdateFileRecord(db, file, normalizedLocation, existingFileModel);
        }
    }

    private void HandleMissingFile(AppDbContext db, IFileInfo file, string normalizedLocation)
    {
        /*
   at tonehub.StreamUtils.StreamLimiter.SetLength(Int64 value) in /home/mediacenter/projects/tonehub/tonehub/StreamUtils/StreamLimiter.cs:line 46
   at tonehub.Metadata.HashBuilderBase.BuildPartialHash(Stream input, Int64 offset, Int64 length) in /home/mediacenter/projects/tonehub/tonehub/Metadata/HashBuilderBase.cs:line 51
   at tonehub.Metadata.HashBuilderBase.BuildPartialHash(Stream input, Int64 centerWindowSize) in /home/mediacenter/projects/tonehub/tonehub/Metadata/HashBuilderBase.cs:line 34
   at tonehub.Metadata.AudioHashBuilder.BuildPartialHash(IFileInfo file) in /home/mediacenter/projects/tonehub/tonehub/Metadata/AudioHashBuilder.cs:line 18
   at tonehub.Services.FileIndexerService.HandleMissingFile(AppDbContext db, IFileInfo file, String normalizedLocation) in /home/mediacenter/projects/tonehub/tonehub/Services/FileIndexerService.cs:line 85 */
        try
        {
            
            /*

            */
            
            var partialHash = BuildPartialHash(file);
            var fileModel = db.Files.FirstOrDefault(f => f.PartialHash == partialHash);
            // todo: append hash parameters to CreateNewFileRecord and UpdateFileRecord to prevent recalculation
            if(fileModel == null)
            {
                CreateNewFileRecord(db, file, normalizedLocation, partialHash);
            } else
            {
                UpdateFileRecord(db, file, normalizedLocation, fileModel);
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }

    }

    private string BuildPartialHash(IFileInfo file)
    {
        return Convert.ToHexString(_hashBuilder.BuildPartialHash(file));
    }
    
    private string BuildFullHash(IFileInfo file)
    {
        return Convert.ToHexString(_hashBuilder.BuildFullHash(file));
    }
    
    private void CreateNewFileRecord(AppDbContext db, IFileInfo file, string normalizedLocation, string partialHash)
    {
        var newFileRecord = new FileModel {IsDirty = true};
        FillRecordBasics(newFileRecord, file, normalizedLocation, partialHash);
    }

    private void FillRecordBasics(FileModel newFileRecord, IFileInfo file, string normalizedLocation, string? partialHash=null, string? fullHash=null)
    {
        if(!TryLoadFileMimeType(file.FullName, out var mimeType))
        {
            throw new Exception("Could not fill record basics: MimeType fail");
        }

        newFileRecord.MimeMediaType = mimeType.MediaType;
        newFileRecord.MimeSubType = mimeType.SubType;
        newFileRecord.LastCheckDate = DateTimeOffset.Now;
        newFileRecord.Size = file.Length;
        newFileRecord.Location = normalizedLocation;
        newFileRecord.PartialHash = partialHash ?? BuildPartialHash(file);
        newFileRecord.FullHash = fullHash ?? BuildFullHash(file);
    }

    private void UpdateFileRecord(AppDbContext db, IFileInfo file, string normalizedLocation, FileModel fileModel)
    {
        fileModel.LastCheckDate = DateTimeOffset.Now;
        fileModel.Location = normalizedLocation;
        
        throw new NotImplementedException();

        
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