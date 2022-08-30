using System.ComponentModel.DataAnnotations;
using JsonApiDotNetCore.Resources.Annotations;
using Newtonsoft.Json.Linq;

namespace tonehub.Database.Models;

// maybe more generic "EntityAction" with
// - EntityName
// - EntityId
// - Action
// - Context
public class FileAction : ModelBaseDated
{
    [StringLength(50)][Attr] public string Name { get; set; } = "";
    [Attr] public JToken Context { get; set; } = new JObject();
    [HasOne] public File File { get; set; }
}