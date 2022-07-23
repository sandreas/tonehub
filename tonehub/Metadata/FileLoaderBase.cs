using System.IO.Abstractions;
using Newtonsoft.Json.Linq;
using Standart.Hash.xxHash;

namespace tonehub.Metadata;

public abstract class FileLoaderBase: IFileLoader
{
    public abstract string Namespace { get; }
    protected readonly Func<Stream, byte[]> HashFunction;

    protected FileLoaderBase(Func<Stream, byte[]>? hashFunction = null)
    {
        HashFunction = hashFunction ?? (s => BitConverter.GetBytes(xxHash64.ComputeHash(s)));
    }

    public abstract byte[] BuildHash();

    public abstract bool Supports(IFileInfo file);

    public abstract void Initialize(IFileInfo file);

    public abstract GlobalFilterType LoadGlobalFilterType();

    public abstract IEnumerable<(string Namespace, uint Type, string Value)> LoadTags();

    public abstract IEnumerable<(string Namespace, uint Type, JToken Value)> LoadJsonValues();
}