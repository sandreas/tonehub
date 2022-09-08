using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using JsonApiDotNetCore.Resources.Annotations;
using tonehub.Metadata;

namespace tonehub.Database.Models;

public class FileSource : ModelBaseDatedDisabled
{
    [Attr] public GlobalFilterType GlobalFilterType { get; set; } = GlobalFilterType.Unspecified;
    [StringLength(4096)][Attr] public string Location { get; set; } = "";
    [HasMany] public virtual ICollection<File> Files { get; set; } = new Collection<File>();

    [NotMapped] public CancellationTokenSource Cts { get; set; } = new();
}