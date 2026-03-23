using System.Net.Sockets;
using System.Text.RegularExpressions;

namespace PacketCaptureAgent;

public record ReplayPacket(string Name, string Direction, Dictionary<string, object> Fields, TimeSpan Timestamp);

public enum ReplayMode { Timing, Response, Hybrid }

public class ReplayOptions
{
    public ReplayMode Mode { get; set; } = ReplayMode.Hybrid;
    public int TimeoutMs { get; set; } = 5000;
    public double Speed { get; set; } = 1.0;
    public Dictionary<string, object>? Overrides { get; set; }
}

/// <summary>응답 처리 전략. 코어 리플레이 루프와 응답 해석을 분리.</summary>
public interface IResponseHandler
{
    /// <summary>수신 데이터 처리. 반환값: 수신 패킷 수.</summary>
    int OnResponse(byte[] data, int length, ReplayContext context);
}

/// <summary>리플레이 중 공유되는 세션 상태.</summary>
public class ReplayContext
{
    public TimeSpan Elapsed { get; set; }
    /// <summary>응답값 저장 → 다음 패킷 Overrides에 동적 주입 가능.</summary>
    public Dictionary<string, object> SessionState { get; } = new();
    public GameWorldState World { get; } = new();
}

/// <summary>바이트 크기만 출력하는 기본 핸들러.</summary>
public class RawResponseHandler : IResponseHandler
{
    public int OnResponse(byte[] data, int length, ReplayContext context)
    {
        Console.WriteLine($"[{context.Elapsed:mm\\:ss\\.fff}] RECV ({length} bytes)");
        return 1;
    }
}

/// <summary>프로토콜 파싱 후 필드까지 출력하는 핸들러.</summary>
public class ParsingResponseHandler : IResponseHandler
{
    private readonly PacketParser _parser;
    private readonly TcpStream _tcpStream;

    public ParsingResponseHandler(ProtocolDefinition protocol, string host, int port)
    {
        _parser = new PacketParser(protocol);
        var connKey = new ConnectionKey(System.Net.IPAddress.Parse(host), port, System.Net.IPAddress.Loopback, 0);
        _tcpStream = new TcpStream(connKey);
    }

    public int OnResponse(byte[] data, int length, ReplayContext context)
    {
        _tcpStream.Append(data.AsSpan(0, length));
        int count = 0;
        ParsedPacket? result;
        while ((result = _parser.TryParse(_tcpStream)) != null)
        {
            count++;
            Console.WriteLine($"[{context.Elapsed:mm\\:ss\\.fff}] RECV {result.Name}");
            foreach (var field in result.Fields)
            {
                var val = field.Value;
                string display = val is string s ? $"\"{s}\"" : val?.ToString() ?? "null";
                Console.WriteLine($"    {field.Key}: {display}");
            }
            // 응답 필드를 SessionState에 저장 (동적 주입용)
            foreach (var field in result.Fields)
                context.SessionState[$"{result.Name}.{field.Key}"] = field.Value;
            context.World.Update(result.Name, result.Fields);
        }
        return count;
    }
}

public class PacketReplayer
{
    private readonly ProtocolDefinition _protocol;
    private readonly PacketBuilder _builder;

    public PacketReplayer(ProtocolDefinition protocol)
    {
        _protocol = protocol;
        _builder = new PacketBuilder(protocol);
    }

    public List<ReplayPacket> ParseLog(string logPath)
    {
        var packets = new List<ReplayPacket>();
        var lines = File.ReadAllLines(logPath);

        string? currentPacket = null;
        string? currentDirection = null;
        TimeSpan currentTimestamp = TimeSpan.Zero;
        var currentFields = new Dictionary<string, object>();

        foreach (var line in lines)
        {
            var headerMatch = Regex.Match(line, @"\[(\d+):(\d+):(\d+)\.(\d+)\]\s+(SEND|RECV)\s+(\w+)\s+\(\d+\s+bytes\)");
            if (headerMatch.Success)
            {
                if (currentPacket != null)
                    packets.Add(new ReplayPacket(currentPacket, currentDirection!, new(currentFields), currentTimestamp));

                currentTimestamp = new TimeSpan(0,
                    int.Parse(headerMatch.Groups[1].Value),
                    int.Parse(headerMatch.Groups[2].Value),
                    int.Parse(headerMatch.Groups[3].Value),
                    int.Parse(headerMatch.Groups[4].Value));
                currentDirection = headerMatch.Groups[5].Value;
                currentPacket = headerMatch.Groups[6].Value;
                currentFields.Clear();
                continue;
            }

            var fieldMatch = Regex.Match(line, @"^\s+([\w\[\]\.]+):\s+(.+)$");
            if (fieldMatch.Success && currentPacket != null)
            {
                var name = fieldMatch.Groups[1].Value;
                var valueStr = fieldMatch.Groups[2].Value;
                if (name == "raw" || valueStr.Contains("->")) continue;
                currentFields[name] = ParseValue(valueStr);
            }
        }

        if (currentPacket != null)
            packets.Add(new ReplayPacket(currentPacket, currentDirection!, new(currentFields), currentTimestamp));

        return packets;
    }

    private object ParseValue(string valueStr)
    {
        if (valueStr.StartsWith('"') && valueStr.EndsWith('"'))
            return valueStr[1..^1];
        var enumMatch = Regex.Match(valueStr, @"^(\d+)\s+\(");
        if (enumMatch.Success)
            return int.Parse(enumMatch.Groups[1].Value);
        if (int.TryParse(valueStr, out var intVal))
            return intVal;
        if (double.TryParse(valueStr, out var doubleVal))
            return doubleVal;
        return valueStr;
    }

    /// <summary>코어 리플레이 루프. 타이밍·송수신 시퀀싱을 담당하고, 응답 처리는 handler에 위임.</summary>
    public void Replay(string host, int port, List<ReplayPacket> packets, IResponseHandler handler, ReplayOptions? options = null, List<IReplayInterceptor>? interceptors = null)
    {
        options ??= new ReplayOptions();
        using var client = new TcpClient();
        client.Connect(host, port);
        using var stream = client.GetStream();

        Console.WriteLine($"Connected to {host}:{port}");
        Console.WriteLine($"Mode: {options.Mode}, Timeout: {options.TimeoutMs}ms, Speed: {options.Speed}x\n");

        var recvBuffer = new byte[65536];
        var startTime = DateTime.Now;
        var context = new ReplayContext();
        var session = new ReplaySession(stream, _builder, handler, context, startTime);
        int sent = 0, received = 0;

        for (int i = 0; i < packets.Count; i++)
        {
            var pkt = packets[i];
            if (pkt.Direction != "SEND") continue;

            // 타이밍 대기 (timing/hybrid 모드)
            if (options.Mode != ReplayMode.Response && i > 0)
            {
                var prev = packets.Take(i).LastOrDefault(p => p.Direction == "SEND");
                if (prev != null)
                {
                    var delay = (pkt.Timestamp - prev.Timestamp) / options.Speed;
                    if (delay > TimeSpan.Zero)
                        Thread.Sleep(delay);
                }
            }

            // 인터셉터: 사전 작업 수행 후 수정된 패킷 반환
            var interceptor = interceptors?.FirstOrDefault(ic => ic.ShouldIntercept(pkt, context.World));
            if (interceptor != null)
                pkt = interceptor.Prepare(session, pkt);

            var data = _builder.Build(pkt.Name, pkt.Fields, options.Overrides);
            stream.Write(data);
            sent++;
            context.Elapsed = DateTime.Now - startTime;
            Console.WriteLine($"[{context.Elapsed:mm\\:ss\\.fff}] SEND {pkt.Name} ({data.Length} bytes)");

            // 응답 대기 (response/hybrid 모드)
            if (options.Mode != ReplayMode.Timing)
            {
                bool expectResponse = packets.Skip(i + 1).Any(p => p.Direction == "RECV");
                if (expectResponse && WaitForResponse(stream, recvBuffer, options.TimeoutMs, out var recvLen))
                {
                    context.Elapsed = DateTime.Now - startTime;
                    received += handler.OnResponse(recvBuffer, recvLen, context);
                    DrainPendingData(stream, recvBuffer, handler, context, ref received);
                }
                else if (expectResponse)
                {
                    Console.WriteLine($"  ⚠ Response timeout ({options.TimeoutMs}ms)");
                }
            }
        }

        Console.WriteLine($"\nReplay completed: {sent} sent, {received} received");
    }

    private bool WaitForResponse(NetworkStream stream, byte[] buffer, int timeoutMs, out int length)
    {
        length = 0;
        stream.ReadTimeout = timeoutMs;
        try
        {
            length = stream.Read(buffer);
            return length > 0;
        }
        catch (IOException)
        {
            return false;
        }
    }

    /// <summary>DataAvailable인 동안 추가 데이터를 읽어 handler에 전달.</summary>
    private void DrainPendingData(NetworkStream stream, byte[] buffer, IResponseHandler handler, ReplayContext context, ref int received)
    {
        Thread.Sleep(50); // 서버 응답 도착 대기
        while (stream.DataAvailable)
        {
            try
            {
                stream.ReadTimeout = 100;
                int len = stream.Read(buffer);
                if (len > 0)
                {
                    context.Elapsed = DateTime.Now - DateTime.Now; // updated by caller
                    received += handler.OnResponse(buffer, len, context);
                }
            }
            catch (IOException) { break; }
        }
    }
}
