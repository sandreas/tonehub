using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Controllers;
using JsonApiDotNetCore.Services;
using tonehub.Database.Models;

namespace tonehub.Controllers;

public class StaticFileListsController: JsonApiQueryController<StaticFileList, Guid>
{
    public StaticFileListsController(IJsonApiOptions options, IResourceGraph resourceGraph, IResourceQueryService<StaticFileList, Guid> resourceService,
        ILoggerFactory loggerFactory)
        : base(options, resourceGraph, loggerFactory, resourceService)
    {
    }
}