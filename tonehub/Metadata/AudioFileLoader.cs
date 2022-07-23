using System.IO.Abstractions;
using Newtonsoft.Json.Linq;
using Sandreas.AudioMetadata;
using tonehub.StreamUtils;

namespace tonehub.Metadata;

public class AudioFileLoader : FileLoaderBase
{

    public static readonly MetadataProperty[] JsonProperties =
    {
        MetadataProperty.LongDescription,
        MetadataProperty.Lyrics,
        MetadataProperty.Chapters,
        MetadataProperty.EmbeddedPictures,
        MetadataProperty.Chapters,
        MetadataProperty.AdditionalFields
    };

    private MetadataTrack _track = null!;
    private IFileInfo _file = null!;
    

    public override string Namespace => "audio";
    public override void Initialize(IFileInfo file)
    {
        _file = file;
        _track = new MetadataTrack(file);
    }

    public override GlobalFilterType LoadGlobalFilterType(){
        if (_track.ItunesMediaType == null)
        {
            return GlobalFilterType.Unspecified;
        }

        var intMediaType = (int)_track.ItunesMediaType;
        if (Enum.IsDefined(typeof(GlobalFilterType), intMediaType))
        {
            return (GlobalFilterType)intMediaType;
        }

        return GlobalFilterType.Unspecified;
    }

    
    public override IEnumerable<(string Namespace, uint Type, string Value)> LoadTags()
    {
        // maybe _track.GetMetadataPropertyType(p).IsValueType?
        // skip Chapters, EmbeddedPictures, AdditionalFields
        var stringConvertibleProperties = MetadataExtensions.MetadataProperties.Where(p => !JsonProperties.Contains(p));
        foreach (var p in stringConvertibleProperties)
        {
            var value = _track.GetMetadataPropertyValue(p);
            
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
        // if(_track.EmbeddedPictures.Count > 0)
    }

    public override IEnumerable<(string Namespace, uint Type, JToken Value)> LoadJsonValues()
    {
        var unmappedAdditionalFields =_track.AdditionalFields.Where(kvp => !_track.MappedAdditionalFields.ContainsKey(kvp.Key)).ToDictionary(kvp=>kvp.Key, kvp => kvp.Value);
        if (unmappedAdditionalFields.Count > 0)
        {
            yield return (Namespace, (uint)MetadataProperty.AdditionalFields, JObject.FromObject(unmappedAdditionalFields));
        }

        
        if(_track.LongDescription?.Length > 0){
            yield return (Namespace, (uint)MetadataProperty.LongDescription, new JValue(_track.LongDescription));
        }
        
        if (_track.Chapters.Count > 0)
        {
            // todo: find better represenation of chapters and lyrics
            yield return (Namespace,(uint)MetadataProperty.Chapters, JArray.FromObject(_track.Chapters.Select(c =>
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
        using var fileStream = _file.OpenRead();
        using var input = new StreamLimiter(fileStream, _track.TechnicalInformation.AudioDataOffset, _track.TechnicalInformation.AudioDataSize);
        return HashFunction(input);
    }

    public override bool Supports(IFileInfo file)
    {
        return file.Extension == ".m4b";
    }
}