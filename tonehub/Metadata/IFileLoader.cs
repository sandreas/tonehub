using System.IO.Abstractions;
using Newtonsoft.Json.Linq;

namespace tonehub.Metadata;

public interface IFileLoader
{
    // TagTypes above this value are custom tags and not automatically managed and seen as user defined tags
    const int CustomTagTypeStart = 1000000;
    
    public string Namespace { get;}
    public bool Supports(IFileInfo file);

    public void Initialize(IFileInfo file);

    public byte[] BuildHash();
    public GlobalFilterType LoadGlobalFilterType();

    public IEnumerable<(string Namespace, uint Type, string Value)> LoadTags();
    public IEnumerable<(string Namespace, uint Type, JToken Value)> LoadJsonValues();


}