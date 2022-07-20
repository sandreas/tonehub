using System.IO.Abstractions;
using Newtonsoft.Json.Linq;
using tonehub.Database.Models;

namespace tonehub.Metadata;

public interface IFileTagLoader
{
    // TagTypes above this value are custom tags and not automatically managed and seen as user defined tags
    const int CustomTagTypeStart = 1000000;
    
    public string Namespace { get;}
    public bool Supports(IFileInfo path);
    public GlobalFilterType LoadGlobalFilterType(IFileInfo path);

    public IEnumerable<(string Namespace, uint Type, string Value)> LoadTags(IFileInfo path);
    public IEnumerable<(string Namespace, uint Type, JToken Value)> LoadJsonValues(IFileInfo path);


}