using System.ComponentModel.DataAnnotations;
using JsonApiDotNetCore.Resources.Annotations;
using Newtonsoft.Json.Linq;

namespace tonehub.Database.Models;

public class FileJsonValue : ModelBaseDated
{
    [StringLength(255)] [Attr] public string Namespace { get; set; } = "default";
    [Attr] public uint Type { get; set; } = 0;
    [Attr] public JToken Value { get; set; } = new JObject();
    [HasOne] public File File { get; set; }
}