using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Controllers;
using JsonApiDotNetCore.Services;
using tonehub.Database.Models;
using File = tonehub.Database.Models.File;

namespace tonehub.Controllers;

public class FilesController: JsonApiQueryController<Database.Models.File, Guid>
{
    public FilesController(IJsonApiOptions options, IResourceGraph resourceGraph, IResourceQueryService<File, Guid> resourceService,
        ILoggerFactory loggerFactory)
        : base(options, resourceGraph, loggerFactory, resourceService)
    {
    }
}