using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Controllers;
using JsonApiDotNetCore.Services;
using tonehub.Database.Models;

namespace tonehub.Controllers;

public class FileTagsController: JsonApiQueryController<FileTag, Guid>
{
    public FileTagsController(IJsonApiOptions options, IResourceGraph resourceGraph, IResourceQueryService<FileTag, Guid> resourceService,
        ILoggerFactory loggerFactory)
        : base(options, resourceGraph, loggerFactory, resourceService)
    {
    }
}