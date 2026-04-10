namespace PacketCaptureAgent;

static class ReplayModeRunner
{
    public static async Task RunAsync(Program.CliOptions cli)
    {
        Console.WriteLine("=== Packet Replay Mode ===\n");

        var protocol = Program.LoadProtocol(cli.ProtocolPath);
        if (protocol == null) return;

        if (!File.Exists(cli.ReplayLog))
        {
            Console.WriteLine($"로그 파일을 찾을 수 없음: {cli.ReplayLog}");
            return;
        }

        var replayer = new PacketReplayer(protocol);

        Console.WriteLine($"프로토콜: {protocol.Protocol.Name}");
        Console.WriteLine($"로그 파일: {cli.ReplayLog}");

        var packets = replayer.ParseLog(cli.ReplayLog!);
        Console.WriteLine($"파싱된 패킷: {packets.Count}개");
        Console.WriteLine($"  SEND: {packets.Count(p => p.Direction == "SEND")}개");
        Console.WriteLine($"  RECV: {packets.Count(p => p.Direction == "RECV")}개");

        var ep = Program.ParseTarget(cli.Target);
        if (ep == null) return;
        var (host, port) = ep.Value;

        Console.WriteLine($"\n{host}:{port}로 재현 시작...\n");
        var logDir = Program.LogDir(cli.ProtocolPath!);
        using var logger = new ReplayLogger(logDir, console: Console.Out);
        var options = ParseReplayOptions(cli);
        var handler = new ParsingResponseHandler(protocol, host, port, logger);
        var interceptors = new List<IReplayInterceptor> { new ProximityInterceptor(protocol.Semantics?.ProximityActions ?? new()) };
        await replayer.ReplayAsync(host, port, packets, handler, options, interceptors, logger);
    }

    internal static ReplayOptions ParseReplayOptions(Program.CliOptions cli) => new()
    {
        Mode = cli.Mode switch
        {
            "timing" => ReplayMode.Timing,
            "response" => ReplayMode.Response,
            _ => ReplayMode.Hybrid
        },
        TimeoutMs = cli.Timeout,
        Speed = cli.Speed
    };
}
