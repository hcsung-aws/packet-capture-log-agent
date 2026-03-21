using System.Net;

namespace PacketCaptureAgent;

public record ConnectionKey(IPAddress SrcIP, int SrcPort, IPAddress DstIP, int DstPort)
{
    public ConnectionKey Reverse() => new(DstIP, DstPort, SrcIP, SrcPort);
    public override string ToString() => $"{SrcIP}:{SrcPort}->{DstIP}:{DstPort}";
}

public class TcpStream
{
    private byte[] _buffer = new byte[65536];
    private int _writePos = 0;
    private int _readPos = 0;
    public ConnectionKey Key { get; }
    public DateTime LastActivity { get; private set; } = DateTime.Now;

    public TcpStream(ConnectionKey key) => Key = key;

    public void Append(ReadOnlySpan<byte> data)
    {
        if (_writePos + data.Length > _buffer.Length)
        {
            Compact();
            if (_writePos + data.Length > _buffer.Length)
                Grow(_writePos + data.Length);
        }
        data.CopyTo(_buffer.AsSpan(_writePos));
        _writePos += data.Length;
        LastActivity = DateTime.Now;
    }

    public int Available => _writePos - _readPos;

    public bool TryPeek(Span<byte> dest)
    {
        if (Available < dest.Length) return false;
        _buffer.AsSpan(_readPos, dest.Length).CopyTo(dest);
        return true;
    }

    public bool TryRead(Span<byte> dest)
    {
        if (Available < dest.Length) return false;
        _buffer.AsSpan(_readPos, dest.Length).CopyTo(dest);
        _readPos += dest.Length;
        if (_readPos == _writePos)
        {
            _readPos = 0;
            _writePos = 0;
        }
        return true;
    }

    private void Compact()
    {
        if (_readPos > 0)
        {
            var remaining = Available;
            Buffer.BlockCopy(_buffer, _readPos, _buffer, 0, remaining);
            _readPos = 0;
            _writePos = remaining;
        }
    }

    private void Grow(int required)
    {
        int newSize = _buffer.Length;
        while (newSize < required) newSize *= 2;
        var newBuffer = new byte[newSize];
        Buffer.BlockCopy(_buffer, 0, newBuffer, 0, _writePos);
        _buffer = newBuffer;
    }
}

public class TcpStreamManager
{
    private readonly Dictionary<ConnectionKey, TcpStream> _streams = new();

    public TcpStream GetOrCreate(ConnectionKey key)
    {
        if (!_streams.TryGetValue(key, out var stream))
        {
            stream = new TcpStream(key);
            _streams[key] = stream;
        }
        return stream;
    }
}
