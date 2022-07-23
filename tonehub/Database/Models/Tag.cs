using JsonApiDotNetCore.Resources.Annotations;
using Microsoft.EntityFrameworkCore;

namespace tonehub.Database.Models;


[Index(nameof(Value), IsUnique = true)]
public class Tag : ModelBaseDated
{
    [Attr] public string Value { get; set; } = "";

    [HasMany] public virtual ICollection<FileTag> FileTags { get; set; } = new List<FileTag>();
}    