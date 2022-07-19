using System.IO.Abstractions;
using Newtonsoft.Json.Linq;
using Sandreas.AudioMetadata;
using tonehub.Database.Models;
using File = System.IO.File;

namespace tonehub.Metadata;

public class AudioFileTagLoader : IFileTagLoader
{
    public static readonly MetadataProperty[] JsonProperties =
    {
        MetadataProperty.Chapters,
        MetadataProperty.EmbeddedPictures,
        MetadataProperty.Chapters,
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


    public IEnumerable<(uint type, string value)> LoadTags(System.IO.Abstractions.IFileInfo path)
    {
        var track = new MetadataTrack(path);

        // maybe track.GetMetadataPropertyType(p).IsValueType?
        // skip Chapters, EmbeddedPictures, AdditionalFields
        var stringConvertibleProperties = MetadataExtensions.MetadataProperties.Where(p => !JsonProperties.Contains(p));
        foreach (var p in stringConvertibleProperties)
        {
            var stringValue = track.GetMetadataPropertyValue(p)?.ToString();
            if (string.IsNullOrEmpty(stringValue))
            {
                continue;
            }

            yield return ((uint)p, stringValue);
        }
        // todo: add tag for picture count?
        // if(track.EmbeddedPictures.Count > 0)
    }

    public IEnumerable<(uint type, JToken value)> LoadJsonValues(System.IO.Abstractions.IFileInfo path)
    {
        var track = new MetadataTrack(path);
        if (track.AdditionalFields.Count > 0)
        {
            yield return ((uint)MetadataProperty.AdditionalFields, JObject.FromObject(track.AdditionalFields));
        }

        if (track.Chapters.Count > 0)
        {
            yield return ((uint)MetadataProperty.Chapters, JArray.FromObject(track.Chapters.Select(c =>
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