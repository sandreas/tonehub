using System.ComponentModel.DataAnnotations;
using JsonApiDotNetCore.Resources.Annotations;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;

namespace tonehub.Database.Models;

[Index(nameof(Value), IsUnique = true)]
public class Setting : ModelBase
{
    [Attr] public string Key { get; set; } = "";
    [Attr] public JToken Value { get; set; } = new JObject();
}