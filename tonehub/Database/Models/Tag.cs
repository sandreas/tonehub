namespace tonehub.Controllers;

public class Tag : ModelBase
{
    [Attr] public string Value { get; set; } = "";

    [HasMany] public virtual ICollection<FileTag> FileTags { get; set; } = new List<FileTag>();
}    