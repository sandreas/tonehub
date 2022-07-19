using System.IO.Abstractions;
using Sandreas.AudioMetadata;
using tonehub.StreamUtils;

namespace tonehub.Metadata;

public class AudioHashBuilder: HashBuilderBase
{
    public override bool Supports(IFileInfo file)
    {
        return file.Extension == ".m4b";
    }
    
    public override byte[] BuildPartialHash(IFileInfo file)
    {
        using var fileStream = file.OpenRead();
        using var input = WrapStream(file, fileStream);
        return BuildPartialHash(input, PartialHashBytesCount);
    }
    
    public override byte[] BuildFullHash(IFileInfo file)
    {
        using var fileStream = file.OpenRead();
        using var input = WrapStream(file, fileStream);
        return HashFunction(input);
    }
    private static Stream WrapStream(IFileInfo file, Stream input)    {
        var track = new MetadataTrack(file.FullName);
        var offset = track.TechnicalInformation.AudioDataOffset;
        var length = track.TechnicalInformation.AudioDataSize;
        return new StreamLimiter(input, offset, length);
    }
    
    /*
    private Stream BuildPartialStream(IFileInfo file)    {
        var track = new MetadataTrack(file.FullName);
        var offset = track.TechnicalInformation.AudioDataOffset;
        var size = track.TechnicalInformation.AudioDataSize;
        var fileStream = file.OpenRead();
        
        
        fileStream.Position = offset;
        fileStream.SetLength(offset+size);
        return fileStream;
    }
    */

}