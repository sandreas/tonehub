using System.IO.Abstractions;
using Newtonsoft.Json.Linq;
using Sandreas.AudioMetadata;

namespace tonehub.Metadata;

public class AudioFileTagLoader : IFileTagLoader
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

    public string Namespace => "audio";

    public bool Supports(IFileInfo path)
    {
        return path.Extension == ".m4b";
    }

    public GlobalFilterType LoadGlobalFilterType(IFileInfo path)
    {
        var track = new MetadataTrack(path);
        if(track.ItunesMediaType == null)    {
            return GlobalFilterType.Unspecified;
        }

        var intMediaType = (int)track.ItunesMediaType;
        if(Enum.IsDefined(typeof(GlobalFilterType), intMediaType)){
            return (GlobalFilterType)intMediaType;
        } 
        return GlobalFilterType.Unspecified;
    }


    public IEnumerable<(string Namespace, uint Type, string Value)> LoadTags(IFileInfo path)
    {
        var track = new MetadataTrack(path);

        // maybe track.GetMetadataPropertyType(p).IsValueType?
        // skip Chapters, EmbeddedPictures, AdditionalFields
        var stringConvertibleProperties = MetadataExtensions.MetadataProperties.Where(p => !JsonProperties.Contains(p));
        foreach (var p in stringConvertibleProperties)
        {
            var value = track.GetMetadataPropertyValue(p);
            
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
        // if(track.EmbeddedPictures.Count > 0)
    }

    public IEnumerable<(string Namespace, uint Type, JToken Value)> LoadJsonValues(IFileInfo path)
    {
        var track = new MetadataTrack(path);
        var unmappedAdditionalFields =track.AdditionalFields.Where(kvp => !track.MappedAdditionalFields.ContainsKey(kvp.Key)).ToDictionary(kvp=>kvp.Key, kvp => kvp.Value);
        if (unmappedAdditionalFields.Count > 0)
        {
            yield return (Namespace, (uint)MetadataProperty.AdditionalFields, JObject.FromObject(unmappedAdditionalFields));
        }

        
        if(track.LongDescription?.Length > 0){
            yield return (Namespace, (uint)MetadataProperty.LongDescription, new JValue(track.LongDescription));
        }
        
        if (track.Chapters.Count > 0)
        {
            // todo: find better represenation of chapters and lyrics
            yield return (Namespace,(uint)MetadataProperty.Chapters, JArray.FromObject(track.Chapters.Select(c =>
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
/*
    public async IAsyncEnumerable<ModelBase> LoadAsync(System.IO.Abstractions.IFileInfo path)
    {
        var tags = new List<Tag>();
        var track = new MetadataTrack(path);

        foreach (var p in MetadataExtensions.MetadataProperties)
        {
            // maybe track.GetMetadataPropertyType(p).IsValueType?
            // skip Chapters, EmbeddedPictures, AdditionalFields
            if (JsonProperties.Contains(p))
            {
                continue;
            }

            var stringValue = track.GetMetadataPropertyValue(p)?.ToString();
            if (stringValue == null)
            {
                continue;
            }

            var tag = tags.FirstOrDefault(t => t.Value == stringValue) ?? new Tag
            {
                Value = stringValue
            };
            var fileTag = new FileTag
            {
                Namespace = Namespace,
                Type = (uint)p,
                Tag = tag
            };
            tags.Add(tag);
            yield return fileTag;
        }

        foreach (var tag in tags)
        {
            yield return tag;
        }


        if (track.AdditionalFields.Count > 0)
        {
            foreach (var (key, value) in track.AdditionalFields)
            {
            }


            var additionalFieldsObject = JObject.FromObject(track.AdditionalFields);
            yield return new FileJsonValue
            {
                Namespace = Namespace,
                Type = (uint)MetadataProperty.AdditionalFields,
                Value = additionalFieldsObject
            };
        }
    }
    */
}