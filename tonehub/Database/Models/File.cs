using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using JsonApiDotNetCore.Resources.Annotations;
using Microsoft.EntityFrameworkCore;

namespace tonehub.Database.Models;

[Index(nameof(Location), IsUnique = true)]
public class File : ModelBase
{
    [StringLength(50)][Attr] public string MimeMediaType { get; set; } = "";
    [StringLength(50)][Attr] public string MimeSubType { get; set; } = "";

    [StringLength(4096)][Attr] public string Location { get; set; } = "";
    [Attr] public long Size { get; set; }
    
    [HasMany] public virtual ICollection<FileTag> FileTags { get; set; } = new List<FileTag>();
    [HasMany] public virtual ICollection<FileJsonValue> FileJsonValues { get; set; } = new List<FileJsonValue>();
        
    [NotMapped] public bool IsDirty { get; set; }
}