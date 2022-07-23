using System.ComponentModel.DataAnnotations;
using JsonApiDotNetCore.Resources.Annotations;
using Microsoft.EntityFrameworkCore;

namespace tonehub.Database.Models;

[Index(nameof(Name), IsUnique = true)]
public abstract class FileListBase: ModelBase
{
    [StringLength(50)][Attr] public string Name { get; set; } = "";

}