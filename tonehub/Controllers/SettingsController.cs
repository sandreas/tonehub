using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Controllers;
using JsonApiDotNetCore.Services;
using tonehub.Database.Models;

namespace tonehub.Controllers;

public class SettingsController: JsonApiQueryController<Setting, Guid>
{
    public SettingsController(IJsonApiOptions options, IResourceGraph resourceGraph, IResourceQueryService<Setting, Guid> resourceService,
        ILoggerFactory loggerFactory)
        : base(options, resourceGraph, loggerFactory, resourceService)
    {
    }
}