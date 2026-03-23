namespace PacketCaptureAgent;

public class CaptureSession
{
    readonly PacketParser? _parser;
    readonly PacketFormatter? _formatter;
    readonly TcpStreamManager _streamManager;
    readonly int? _filterPort;
    readonly Action<string> _logBoth;     // console + file
    readonly Action<string> _logFileOnly; // file only

    public CaptureSession(
        PacketParser? parser,
        PacketFormatter? formatter,
        TcpStreamManager streamManager,
        int? filterPort,
        Action<string> logBoth,
        Action<string> logFileOnly)
    {
        _parser = parser;
        _formatter = formatter;
        _streamManager = streamManager;
        _filterPort = filterPort;
        _logBoth = logBoth;
        _logFileOnly = logFileOnly;
    }

    public void ProcessPacket(byte[] buffer, int length)
    {
        var info = RawPacketParser.TryExtract(buffer, length, _filterPort);
        if (info == null) return;

        if (_parser != null && _formatter != null)
        {
            var stream = _streamManager.GetOrCreate(info.Connection);
            stream.Append(info.Payload.Span);

            while (true)
            {
                var parsed = _parser.TryParse(stream);
                if (parsed == null) break;

                string direction = _filterPort.HasValue && info.Connection.DstPort == _filterPort ? "SEND" : "RECV";
                var (consoleOut, fileOut) = _formatter.Format(parsed, info.Connection, direction);
                Console.WriteLine(consoleOut);
                _logFileOnly(fileOut);
            }
        }
        else
        {
            var payload = info.Payload.Span;
            var rawHex = Convert.ToHexString(payload);
            var shortHex = rawHex.Length > 64 ? rawHex[..64] + "..." : rawHex;
            var conn = info.Connection;

            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] {conn.SrcIP}:{conn.SrcPort} -> {conn.DstIP}:{conn.DstPort} | Len: {payload.Length}");
            Console.WriteLine($"  raw: {shortHex}");

            _logFileOnly($"[{DateTime.Now:HH:mm:ss.fff}] {conn.SrcIP}:{conn.SrcPort} -> {conn.DstIP}:{conn.DstPort} | Len: {payload.Length}");
            _logFileOnly($"  raw: {rawHex}");
        }
    }
}
