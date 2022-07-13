namespace tonehub.Controllers;

public class JsonFileTag : ModelBase
{
    [StringLength(255)][Attr] public string Namespace { get; set; } = "default";
    [Attr] public uint Type { get; set; }
    [HasOne] public JsonTag Tag { get; set; }
    [HasOne] public File File { get; set; }
}    