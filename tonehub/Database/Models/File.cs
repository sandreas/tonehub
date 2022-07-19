using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using JsonApiDotNetCore.Resources.Annotations;
using Microsoft.EntityFrameworkCore;
using tonehub.Metadata;

namespace tonehub.Database.Models;

[Index(nameof(Location), IsUnique = true)]
public class File : ModelBase
{
    [Attr] public GlobalFilterType ContentType { get; set; } = GlobalFilterType.Unspecified;

    [StringLength(50)][Attr] public string MimeMediaType { get; set; } = "";
    [StringLength(50)][Attr] public string MimeSubType { get; set; } = "";
    
    
    [Attr] public ulong BytesCount { get; set; } = 0; // todo: rename to length / size?
    [StringLength(250)][Attr] public string PartialHash { get; set; } = ""; // todo: remove, always hash fully
    [StringLength(250)][Attr] public string FullHash { get; set; } = "";
    

    [StringLength(4096)][Attr] public string Location { get; set; } = "";
    [Attr] public long Size { get; set; }
    
    [Attr] public DateTimeOffset LastCheckDate { get; set; }

    
    [HasMany] public virtual ICollection<FileTag> FileTags { get; set; } = new List<FileTag>();
    [HasMany] public virtual ICollection<FileJsonValue> FileJsonValues { get; set; } = new List<FileJsonValue>();
        
    [NotMapped] public bool IsDirty { get; set; }
}

