using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Controllers;
using JsonApiDotNetCore.Services;
using tonehub.Database.Models;

namespace tonehub.Controllers;

public class StaticFileListsController: JsonApiQueryController<Setting, Guid>
{
    public StaticFileListsController(IJsonApiOptions options, IResourceGraph resourceGraph, IResourceService<Setting, Guid> resourceService,
        ILoggerFactory loggerFactory)
        : base(options, resourceGraph, loggerFactory, resourceService)
    {
    }
}