using JsonApiDotNetCore.Resources.Annotations;

namespace tonehub.Database.Models;

public class ModelBaseDatedDisabled: ModelBaseDated
{
    [Attr] public bool Disabled { get; set; } = false;
}