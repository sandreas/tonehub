using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Controllers;
using JsonApiDotNetCore.Services;
using tonehub.Database.Models;

namespace tonehub.Controllers;

public class FileJsonValuesController: JsonApiQueryController<FileJsonValue, Guid>
{
    public FileJsonValuesController(IJsonApiOptions options, IResourceGraph resourceGraph, IResourceService<FileJsonValue, Guid> resourceService,
        ILoggerFactory loggerFactory)
        : base(options, resourceGraph, loggerFactory, resourceService)
    {
    }
}