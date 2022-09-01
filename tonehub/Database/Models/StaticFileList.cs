using System.Collections.ObjectModel;
using JsonApiDotNetCore.Resources.Annotations;

namespace tonehub.Database.Models;

public class StaticFileList: FileListBase
{
    [HasMany] public virtual ICollection<File> Files { get; set; } = new Collection<File>();
}