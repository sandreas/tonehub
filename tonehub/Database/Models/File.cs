namespace tonehub.Controllers;

[Index(nameof(Location), IsUnique = true)]
public class File : ModelBase
{
    [StringLength(50)][Attr] public string MimeMediaType { get; set; } = "";
    [StringLength(50)][Attr] public string MimeSubType { get; set; } = "";

    [StringLength(4096)][Attr] public string Location { get; set; } = "";
    [Attr] public long Size { get; set; }
    
    [HasMany] public virtual ICollection<FileTag> FileTags { get; set; } = new List<FileTag>();
        
    [NotMapped] public bool IsDirty { get; set; }
}