using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Standart.Hash.xxHash;
using tonehub.StreamUtils;

namespace tonehub.Metadata;

public abstract class HashBuilderBase : IHashBuilder
{
    protected const long PartialHashBytesCount = 1048576;

    protected readonly Func<Stream, byte[]> HashFunction;

    protected HashBuilderBase(Func<Stream, byte[]>? hashFunction = null)
    {
        HashFunction = hashFunction ?? (s => BitConverter.GetBytes(xxHash64.ComputeHash(s)));
    }

    public virtual bool Supports(System.IO.Abstractions.IFileInfo _) => true;

    
    public virtual byte[] BuildPartialHash(System.IO.Abstractions.IFileInfo file)
    {
        return BuildPartialHash(file, PartialHashBytesCount);
    }
    
    protected byte[] BuildPartialHash(System.IO.Abstractions.IFileInfo file, long centerBytesToRead)
    {
        using var input = file.OpenRead();
        var offset = input.Length / 2 - centerBytesToRead / 2;
        return BuildPartialHash(input, offset, centerBytesToRead);
    }
    
    protected  byte[] BuildPartialHash(Stream input, long centerWindowSize)   {
        var offset = input.Length / 2 - centerWindowSize / 2;
        return BuildPartialHash(input, offset, centerWindowSize);
    }

    
    protected  byte[] BuildPartialHash(Stream input, long offset, long length)
    {
        var pos = input.Position;
        try
        {
            var limited = new StreamLimiter(input, offset, length);
            return HashFunction(limited);
        }
        finally
        {
            input.Position = pos;
        }
    }

    public virtual byte[] BuildFullHash(System.IO.Abstractions.IFileInfo file)
    {
        using var stream = file.OpenRead();
        return HashFunction(stream);
    }
}