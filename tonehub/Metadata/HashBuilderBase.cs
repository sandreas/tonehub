using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Standart.Hash.xxHash;

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
        using var stream = file.OpenRead();
        return BuildPartialHash(stream, centerBytesToRead);
    }
    protected  byte[] BuildPartialHash(Stream input, long centerBytesToRead)
    {
        var offset = input.Length / 2 - centerBytesToRead / 2;
        using var memoryStream = new MemoryStream();
        CopyStream(input, memoryStream, offset, centerBytesToRead);
        return HashFunction(memoryStream);
    }
    
    protected byte[] BuildPartialHash(Stream input, long offset , long length)
    {
        using var memoryStream = new MemoryStream();
        CopyStream(input, memoryStream, offset, length);
        return HashFunction(memoryStream);
    }
    
    protected static void CopyStream(Stream input, Stream output, long offset=0, long limit=long.MaxValue)
    {
        byte[] buffer = new byte[32768];
        input.Position = Math.Min(Math.Max(offset, 0), input.Length);
        limit = Math.Min(limit, input.Length - input.Position);
        var bufferLen = buffer.Length;
        int read;
        while (limit > 0 &&
               (read = input.Read(buffer, 0, bufferLen)) > 0)
        {
            output.Write(buffer, 0, read);
            limit -= read;
            if(limit < bufferLen)
            {
                bufferLen = (int)limit;
            }
        }
    }

    public virtual byte[] BuildFullHash(System.IO.Abstractions.IFileInfo file)
    {
        using var stream = file.OpenRead();
        return HashFunction(stream);
    }
}