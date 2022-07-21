using ATL;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;
using tonehub.Database;
using tonehub.Services;
using FileModel = tonehub.Database.Models.File;
namespace tonehub.Controllers;

    [ApiController]
    [Route("api/[controller]/[action]")]
    public class BinariesController : ControllerBase
    {
        private const int BufferSize = 128000000;
        private AppDbContext _db;
        // private readonly DatabaseSettingsService _settings;
        private readonly string _cachePath;
        private readonly string _mediaPath = "";

        public BinariesController(DatabaseSettingsService settings, IDbContextFactory<AppDbContext> dbFactory)
        {
            // _settings = settings;
            _db = dbFactory.CreateDbContext();
            _cachePath = Path.Combine(Path.GetTempPath(), "tonehub");
            if(settings.TryGet<string>("cachePath", out var cachePath)) {
                _cachePath = cachePath ?? _cachePath;
            }
            
            if(settings.TryGet<string>("mediaPath", out var mediaPath)) {
                _mediaPath = mediaPath ?? _mediaPath;
            }
        }

        [Route("{fileId}")]
        [HttpGet]
        public async Task<IActionResult> StreamAsync(Guid fileId)
        {
            var (path, record) = await LoadRecordPath(fileId);
            if (path == null || record == null)
            {
                return NotFound();
            }

            return await CreateFileResult(System.IO.File.OpenRead(path), record);

        }

        [Route("{fileId}/{embeddedFileNumber}/{maxSize}")]
        [HttpGet]
        public async Task<IActionResult> EmbeddedAsync(Guid fileId, int embeddedFileNumber, int maxSize)
        {
            var cacheFile = $"{_cachePath}/images/{fileId}-{embeddedFileNumber}-{maxSize}.jpg";
            if (!System.IO.File.Exists(cacheFile))
            {
                var (path, record) = await LoadRecordPath(fileId);
                if (path == null || record == null)
                {
                    return NotFound();
                }

                try
                {
                    var track = new Track(path);
                    if (track.EmbeddedPictures.Count <= embeddedFileNumber)
                    {
                        return NotFound();
                    }

                    var requestedPicture = track.EmbeddedPictures[embeddedFileNumber];
                    
                    _ = await CreateThumbnail(requestedPicture.PictureData, maxSize, cacheFile).ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    return NotFound(e.Message);
                }
            }

            return await CreateFileResult(System.IO.File.OpenRead(cacheFile),
                "image/jpeg", "cover.jpg");
        }
        

        private static async Task<string> CreateThumbnail(byte[] byteArrayIn, int size, string outputPath)
        {
            await using var inStream = new MemoryStream(byteArrayIn);
            await using var outStream =  System.IO.File.OpenWrite(outputPath);
            using var image = await Image.LoadAsync(inStream);
            image.Mutate(x => x.Resize(size, 0, KnownResamplers.Lanczos3));
            var encoder = new JpegEncoder {Quality = 85};
            await image.SaveAsync(outStream, encoder);
            return outputPath;
        }
        
        

        private async Task<(string?, FileModel?)> LoadRecordPath(Guid fileId)
        {
            var record = await _db.Files.FirstOrDefaultAsync(f => f.Id == fileId);
            if(record == null){
                return (null, null);
            }
            var path = Path.Combine(_mediaPath, record.Location);
            return !System.IO.File.Exists(path) ? (null, null) : (path, record);
        }

        private async Task<IActionResult> CreateFileResult(Stream stream, FileModel record)
        {
            var downloadName = Path.GetFileName(record.Location);

            return await CreateFileResult(stream, MapMimeType(record.MimeMediaType + "/" + record.MimeSubType), downloadName);
        }
        
        private static string MapMimeType(string mimeType)
        {
            return mimeType switch
            {
                "audio/x-m4b" => "audio/mp4",
                _ => mimeType
            };
        }
        private async Task<IActionResult> CreateFileResult(Stream stream, string mimeType, string downloadName)
        {
            return await Task.FromResult(File(new BufferedStream(stream, BufferSize), mimeType, downloadName, true));
        }
    }