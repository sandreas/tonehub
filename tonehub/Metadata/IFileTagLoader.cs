using System.IO.Abstractions;
using Newtonsoft.Json.Linq;
using tonehub.Database.Models;

namespace tonehub.Metadata;

public interface IFileTagLoader
{
    public string Namespace { get;}
    public bool Supports(IFileInfo path);
    public GlobalFilterType LoadGlobalFilterType(IFileInfo path);

    public IEnumerable<(uint type, string value)> LoadTags(IFileInfo path);
    public IEnumerable<(uint type, JToken value)> LoadJsonValues(IFileInfo path);


}