using System.ComponentModel.DataAnnotations;
using JsonApiDotNetCore.Resources.Annotations;

namespace tonehub.Database.Models;

public class FileTag : ModelBase
{
    [StringLength(255)][Attr] public string Namespace { get; set; } = "default";
    [Attr] public uint Type { get; set; } = 0;
    [HasOne] public Tag Tag { get; set; }
    [HasOne] public File File { get; set; }
}    