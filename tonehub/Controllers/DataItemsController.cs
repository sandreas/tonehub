using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Controllers;
using JsonApiDotNetCore.Services;
using tonehub.Database.Models;

namespace tonehub.Controllers;

public class DataItemsController: JsonApiQueryController<DataItem, Guid>
{
    public DataItemsController(IJsonApiOptions options, IResourceGraph resourceGraph, IResourceQueryService<DataItem, Guid> resourceService,
        ILoggerFactory loggerFactory)
        : base(options, resourceGraph, loggerFactory, resourceService)
    {
    }
}