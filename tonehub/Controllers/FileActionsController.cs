using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Controllers;
using JsonApiDotNetCore.Services;
using tonehub.Database.Models;

namespace tonehub.Controllers;

public class FileActionsController: JsonApiQueryController<FileAction, Guid>
{
    public FileActionsController(IJsonApiOptions options, IResourceGraph resourceGraph, IResourceService<FileAction, Guid> resourceService,
        ILoggerFactory loggerFactory)
        : base(options, resourceGraph, loggerFactory, resourceService)
    {
    }
}