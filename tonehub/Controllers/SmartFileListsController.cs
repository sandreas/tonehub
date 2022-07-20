using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Controllers;
using JsonApiDotNetCore.Services;
using tonehub.Database.Models;

namespace tonehub.Controllers;

public class SmartFileListsController: JsonApiQueryController<Setting, Guid>
{
    public SmartFileListsController(IJsonApiOptions options, IResourceGraph resourceGraph, IResourceService<Setting, Guid> resourceService,
        ILoggerFactory loggerFactory)
        : base(options, resourceGraph, loggerFactory, resourceService)
    {
    }
}