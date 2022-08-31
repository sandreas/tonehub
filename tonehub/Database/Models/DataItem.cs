using System.ComponentModel.DataAnnotations;
using JsonApiDotNetCore.Resources.Annotations;
using Newtonsoft.Json.Linq;

namespace tonehub.Database.Models;

// maybe more generic "EntityAction" with
// - EntityName
// - EntityId
// - Action
// - Context
public class DataItem : ModelBaseDated
{
    [StringLength(250)][Attr] public string Entity { get; set; } = "";
    [StringLength(100)][Attr] public string Identifier { get; set; } = "";
    [StringLength(50)][Attr] public string Key { get; set; } = "";
    [Attr] public JToken Value { get; set; } = new JObject();
}