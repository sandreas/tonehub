using System.IO.Abstractions;
using Newtonsoft.Json.Linq;
using Sandreas.AudioMetadata;
using tonehub.Services;
using tonehub.StreamUtils;

namespace tonehub.Metadata;

public class AudioFileLoader : FileLoaderBase
{
    private readonly string[] _supportedExtensions = { "m4b", "mp3" };

    public static readonly MetadataProperty[] JsonProperties =
    {
        MetadataProperty.LongDescription,
        MetadataProperty.Lyrics,
        MetadataProperty.Chapters,
        MetadataProperty.EmbeddedPictures,
        MetadataProperty.Chapters,
        MetadataProperty.AdditionalFields
    };

    private Lazy<MetadataTrack> _track = null!;
    private IFileInfo _file = null!;
    private readonly long _maxHashBytes;


    public override string Namespace => "audio";

    public AudioFileLoader(DatabaseSettingsService settings)
    {
        if(!settings.TryGet<long>("maxHashBytes", out var maxHashBytes))
        {
            maxHashBytes = long.MaxValue;
        }

        _maxHashBytes = maxHashBytes;
    }
    public override void Initialize(IFileInfo file)
    {
        _file = file;
        _track = new Lazy<MetadataTrack>(() => new MetadataTrack(file));
    }

    public override GlobalFilterType LoadGlobalFilterType(){
        if (_track.Value.ItunesMediaType == null)
        {
            return GlobalFilterType.Unspecified;
        }

        var intMediaType = (int)_track.Value.ItunesMediaType;
        if (Enum.IsDefined(typeof(GlobalFilterType), intMediaType))
        {
            return (GlobalFilterType)intMediaType;
        }

        return GlobalFilterType.Unspecified;
    }

    
    public override IEnumerable<(string Namespace, uint Type, string Value)> LoadTags()
    {
        // maybe _track.Value.GetMetadataPropertyType(p).IsValueType?
        // skip Chapters, EmbeddedPictures, AdditionalFields
        var stringConvertibleProperties = MetadataExtensions.MetadataProperties.Where(p => !JsonProperties.Contains(p));
        foreach (var p in stringConvertibleProperties)
        {
            var value = _track.Value.GetMetadataPropertyValue(p);
            
            if(MetadataExtensions.IsEmpty(value))            {
                continue;
            }
            // convert dates into a culture invariant format, that can be used for "between"
            var stringValue = value switch  {
                    DateTime d => d.ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fff'Z'"),
                _ => value?.ToString()
            }
            ;
            
            if (string.IsNullOrEmpty(stringValue))
            {
                continue;
            }

            yield return (Namespace, (uint)p, stringValue);
        }
        // todo: add tag for picture count?
        // if(_track.Value.EmbeddedPictures.Count > 0)
    }

    public override IEnumerable<(string Namespace, uint Type, JToken Value)> LoadJsonValues()
    {
        var unmappedAdditionalFields =_track.Value.AdditionalFields.Where(kvp => !_track.Value.MappedAdditionalFields.ContainsKey(kvp.Key)).ToDictionary(kvp=>kvp.Key, kvp => kvp.Value);
        if (unmappedAdditionalFields.Count > 0)
        {
            yield return (Namespace, (uint)MetadataProperty.AdditionalFields, JObject.FromObject(unmappedAdditionalFields));
        }

        
        if(_track.Value.LongDescription?.Length > 0){
            yield return (Namespace, (uint)MetadataProperty.LongDescription, new JValue(_track.Value.LongDescription));
        }
        
        if (_track.Value.Chapters.Count > 0)
        {
            // todo: find better represenation of chapters and lyrics
            yield return (Namespace,(uint)MetadataProperty.Chapters, JArray.FromObject(_track.Value.Chapters.Select(c =>
                    JObject.FromObject(new
                    {
                        start = c.StartTime,
                        length = c.EndTime - c.StartTime,
                        title = c.Title,
                        //subtitle = c.Subtitle
                    })))
                );
        }
    }
    
    public override byte[] BuildHash()
    {
        var (offset, length) = CalculateMidpointOffsetLength(_track.Value.TechnicalInformation.AudioDataOffset,
            _track.Value.TechnicalInformation.AudioDataSize, _maxHashBytes);
        using var fileStream = _file.OpenRead();
        using var input = new StreamLimiter(fileStream, offset, length);
        return HashFunction(input);
    }
    
    private (long offset, long limit) CalculateMidpointOffsetLength(long dataOffset, long dataSize, long maxHashBytes){
        var dataMidpoint = Convert.ToInt64(Math.Floor((dataOffset + dataSize) / (decimal)2));
        var offset = Convert.ToInt64(Math.Max(dataMidpoint - maxHashBytes/(decimal)2, dataOffset));
        var length = Math.Min(maxHashBytes, dataSize);
        return (offset, length);
    }

    public override bool Supports(IFileInfo file)
    {
        return _supportedExtensions.Contains(file.Extension.TrimStart('.').ToLowerInvariant());
    }
}