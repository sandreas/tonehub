namespace tonehub.Controllers;

public class ValueStoreItem : ModelBase
{
    [StringLength(255)] [Attr] public string Namespace { get; set; } = "";
    [StringLength(50)][Attr] public string Type { get; set; } = "";
    [StringLength(50)][Attr] public string Action { get; set; } = "";
    [Attr] public Guid? Identifier { get; set; }
    [Attr] public JToken Context { get; set; } = new JObject();
}