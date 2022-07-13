namespace tonehub.Controllers;

public class FileTag : ModelBase
{
    [StringLength(255)][Attr] public string Namespace { get; set; } = "default";
    [Attr] public uint Type { get; set; }
    [HasOne] public Tag Tag { get; set; }
    [HasOne] public File File { get; set; }
}    