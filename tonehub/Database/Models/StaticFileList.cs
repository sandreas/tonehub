using JsonApiDotNetCore.Resources.Annotations;

namespace tonehub.Database.Models;

public class StaticFileList: FileListBase
{
    [HasMany] public virtual ICollection<File> Files { get; set; } = new List<File>();
}