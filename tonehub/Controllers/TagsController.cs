using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Controllers;
using JsonApiDotNetCore.Services;
using tonehub.Database.Models;

namespace tonehub.Controllers;

public class TagsController: JsonApiQueryController<Tag, Guid>
{
    public TagsController(IJsonApiOptions options, IResourceGraph resourceGraph, IResourceQueryService<Tag, Guid> resourceService,
        ILoggerFactory loggerFactory)
        : base(options, resourceGraph, loggerFactory, resourceService)
    {
    }
}