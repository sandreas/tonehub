using System.ComponentModel.DataAnnotations;
using JsonApiDotNetCore.Resources.Annotations;

namespace tonehub.Database.Models;

public abstract class FileListBase: ModelBase
{
    [StringLength(50)][Attr] public string Name { get; set; } = "";

}