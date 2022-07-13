namespace tonehub.Controllers;

public class JsonTag : ModelBase
{
    [Attr] public JToken Value { get; set; } = new JObject();

    [HasMany] public virtual ICollection<JsonFileTag> FileTags { get; set; } = new List<JsonFileTag>();
}    