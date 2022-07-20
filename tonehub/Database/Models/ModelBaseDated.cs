using JsonApiDotNetCore.Resources.Annotations;

namespace tonehub.Database.Models;

public class ModelBaseDated: ModelBase
{
    [Attr] public DateTimeOffset CreatedDate { get; set; }
    [Attr] public DateTimeOffset UpdatedDate { get; set; }

}