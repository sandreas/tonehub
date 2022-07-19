using Microsoft.Extensions.FileProviders;

namespace tonehub.Metadata;

public interface IHashBuilder
{
    public bool Supports(System.IO.Abstractions.IFileInfo file);
    public byte[] BuildPartialHash(System.IO.Abstractions.IFileInfo file);
    public byte[] BuildFullHash(System.IO.Abstractions.IFileInfo file);
}