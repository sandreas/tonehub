using JsonApiDotNetCore.Resources.Annotations;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;

namespace tonehub.Database.Models;

[Index(nameof(Key), IsUnique = true)]
public class Setting : ModelBaseDatedDisabled
{
    [Attr] public string Key { get; set; } = "";
    [Attr] public JToken Value { get; set; } = new JObject();
}