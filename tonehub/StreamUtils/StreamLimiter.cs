namespace tonehub.StreamUtils;

// inspired by https://stackoverflow.com/questions/33354822/how-to-set-length-in-stream-without-truncated
public class StreamLimiter: Stream
{
    private readonly Stream _innerStream;
    private readonly long _limit;
    public StreamLimiter(Stream input, long offset, long length)
    {
        _innerStream = input;
        _innerStream.Position = ClampPosition(_innerStream, offset);
        _limit = ClampPosition(_innerStream, _innerStream.Position + length);
    }

    private static long ClampPosition(Stream input, long offset) => Math.Min(Math.Max(offset, 0), input.Length);

    public override bool CanRead => _innerStream.CanRead;
    public override bool CanSeek => _innerStream.CanSeek;
    public override bool CanWrite => false;
    public override void Flush() => _innerStream.Flush();
    public override long Length => _limit;

    public override long Position
    {
        get => _innerStream.Position;
        set => _innerStream.Position = value; // todo: clamp position to max length?
    }

    public override int Read(byte[] buffer, int offset, int count) => _innerStream.Read(buffer, offset, ClampCount(count));

    public override long Seek(long offset, SeekOrigin origin) => _innerStream.Seek(offset, origin);

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    public override bool CanTimeout => _innerStream.CanTimeout;

    public override int ReadTimeout
    {
        get => _innerStream.ReadTimeout;
        set => _innerStream.ReadTimeout = value;
    }

    public override int WriteTimeout
    {
        get => _innerStream.ReadTimeout;
        set => _innerStream.ReadTimeout = value;
    }

    public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state) => _innerStream.BeginRead(buffer, offset, ClampCount(count), callback, state);

    public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state) => throw new NotSupportedException();

    // do not close the inner stream
    public override void Close() { }

    public override Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken) => _innerStream.CopyToAsync(destination, bufferSize, cancellationToken);

    public override int EndRead(IAsyncResult asyncResult) => _innerStream.EndRead(asyncResult);

    public override Task FlushAsync(CancellationToken cancellationToken) => _innerStream.FlushAsync(cancellationToken);

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => _innerStream.ReadAsync(buffer, offset, ClampCount(count), cancellationToken);

    public override int ReadByte() => ClampCount(1) == 0 ? -1 : _innerStream.ReadByte();

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => throw new NotSupportedException();

    public override void WriteByte(byte value) => throw new NotSupportedException();

    private int ClampCount(int count) => (int)Math.Min(count, ClampPosition(_innerStream, _limit - _innerStream.Position));
}