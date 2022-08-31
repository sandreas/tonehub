using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Controllers;
using JsonApiDotNetCore.Services;
using tonehub.Database.Models;

namespace tonehub.Controllers;

public class SmartFileListsController: JsonApiQueryController<SmartFileList, Guid>
{
    public SmartFileListsController(IJsonApiOptions options, IResourceGraph resourceGraph, IResourceQueryService<SmartFileList, Guid> resourceService,
        ILoggerFactory loggerFactory)
        : base(options, resourceGraph, loggerFactory, resourceService)
    {
    }
}