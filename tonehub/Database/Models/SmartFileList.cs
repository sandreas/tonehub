using JsonApiDotNetCore.Resources.Annotations;
using Newtonsoft.Json.Linq;

namespace tonehub.Database.Models;

// ideas
// - mediaType (music, audiobook, mixed, etc. - GlobalFilterType) - 
// - 
public class SmartFileList : FileListBase
{
    [Attr] public JToken Query { get; set; } = new JObject(); // filter, sort, limit
}