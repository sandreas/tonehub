using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using JsonApiDotNetCore.Resources.Annotations;
using Microsoft.EntityFrameworkCore;
using tonehub.Metadata;

namespace tonehub.Database.Models;

[Index(nameof(Location), IsUnique = true)]

public class File : ModelBaseDatedDisabled
{
    [Attr] public GlobalFilterType GlobalFilterType { get; set; } = GlobalFilterType.Unspecified;

    [StringLength(50)][Attr] public string MimeMediaType { get; set; } = "";
    [StringLength(50)][Attr] public string MimeSubType { get; set; } = "";
    
    [StringLength(250)][Attr] public string Hash { get; set; } = "";

    [StringLength(4096)][Attr] public string Location { get; set; } = "";
    [Attr] public long Size { get; set; }
    [Attr] public DateTimeOffset ModifiedDate { get; set; }
    [Attr] public DateTimeOffset LastCheckDate { get; set; }

    [HasOne] public FileSource Source { get; set; }

    [HasMany] public virtual ICollection<FileTag> FileTags { get; set; } = new List<FileTag>();
    [HasMany] public virtual ICollection<FileJsonValue> FileJsonValues { get; set; } = new List<FileJsonValue>();
        
    [NotMapped] public bool IsNew { get; set; }
    [NotMapped] private bool _hasChanged;
    [NotMapped] public bool HasChanged { get => _hasChanged || IsNew; set => _hasChanged = value; }
}

