using System.IO.Abstractions;
using Microsoft.EntityFrameworkCore;
using Sandreas.Files;
using tonehub.Database;
using tonehub.Database.Models;
using tonehub.Metadata;
using tonehub.Services;
using FileModel = tonehub.Database.Models.File;

namespace tonehub.HostedServices;

public class BackgroundFileIndexerService: IHostedService, IDisposable
{
    private Timer? _timer;
    private bool _indexingInProgress;
    private readonly DatabaseSettingsService _settings;
    private readonly IFileTagLoader _tagLoader;
    private readonly FileWalker _fileWalker;
    private readonly AppDbContext _db;
    private readonly FileIndexerService _fileIndexer;

    public BackgroundFileIndexerService(DatabaseSettingsService settings, FileWalker fileWalker, AudioFileTagLoader tagLoader, IDbContextFactory<AppDbContext> dbFactory, FileIndexerService fileIndexer)
    {
        _settings = settings;
        _fileWalker = fileWalker;
        _tagLoader = tagLoader;
        _db = dbFactory.CreateDbContext();
        _fileIndexer = fileIndexer;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _timer = new Timer(PerformIndexUpdate, null, TimeSpan.Zero,
            TimeSpan.FromMilliseconds(5000));
        
        Console.WriteLine("start file indexer");
        return Task.CompletedTask;
    }

    private void PerformIndexUpdate(object? state)
    {
        try
        {
            if(_indexingInProgress)
            {
                return;
            }
            _indexingInProgress = true;

            if( !_settings.TryGet<string>("mediaPath", out var mediaPath) || mediaPath == null)
            {
                Console.WriteLine("invalid media path: " + mediaPath);
                return;
            }

            _fileIndexer.Run(mediaPath);
            /*
            var files = _fileWalker.WalkRecursive(mediaPath).SelectFileInfo().Where(_tagLoader.Supports);
            
            foreach(var file in files)
            {
                var normalizedLocation = NormalizeLocationFromPath(mediaPath, file);
                var tags = _tagLoader.LoadTags(file);
                var jsonValues = _tagLoader.LoadJsonValues(file);
                
                
                var fileRecord = _db.Files.FirstOrDefault(s => s.Location == normalizedLocation);
                if (fileRecord == null)
                {
                    fileRecord = new FileModel()
                    {
                        
                        IsDirty = true
                    };
                }
                else
                {
                    UpdateFileRecord(_db, file, fileRecord);
                }
                
            }
                
            // walk over media path
            // normalize locations (remove mediaPath from it)
            // 
            */
            
            Console.WriteLine("setting: " + mediaPath);
            Console.WriteLine("perform index update");

        }
        finally
        {
            _indexingInProgress = false;
        }
    }

    
 /*   
    private static string NormalizeLocationFromPath(string mediaPath, IFileSystemInfo file)
    {
        var relPath = file.FullName.StartsWith(mediaPath)
            ? file.FullName.Substring(mediaPath.Length)
            : file.FullName;
        return relPath.Replace(Path.DirectorySeparatorChar, '/').TrimStart('/');
    }
*/
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _timer?.Change(Timeout.Infinite, 0);
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        // throw new NotImplementedException();
        _timer?.Dispose();
    }
}