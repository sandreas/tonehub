using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Controllers;
using JsonApiDotNetCore.Services;
using tonehub.Database.Models;

namespace tonehub.Controllers;

public class FilesController: JsonApiQueryController<Database.Models.File, Guid>
{
    public FilesController(IJsonApiOptions options, IResourceGraph resourceGraph, IResourceService<Database.Models.File, Guid> resourceService,
        ILoggerFactory loggerFactory)
        : base(options, resourceGraph, loggerFactory, resourceService)
    {
    }
}