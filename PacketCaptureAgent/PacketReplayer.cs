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

            var fieldMatch = Regex.Match(line, @"^\s+(\w+):\s+(.+)$");
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

    public void Replay(string host, int port, List<ReplayPacket> packets, ReplayOptions? options = null)
    {
        options ??= new ReplayOptions();
        using var client = new TcpClient();
        client.Connect(host, port);
        using var stream = client.GetStream();
        
        Console.WriteLine($"Connected to {host}:{port}");
        Console.WriteLine($"Mode: {options.Mode}, Timeout: {options.TimeoutMs}ms, Speed: {options.Speed}x\n");

        var recvBuffer = new byte[65536];
        var startTime = DateTime.Now;
        int sent = 0, received = 0;

        for (int i = 0; i < packets.Count; i++)
        {
            var pkt = packets[i];
            
            if (pkt.Direction == "SEND")
            {
                // 시간 간격 대기 (timing/hybrid 모드)
                if (options.Mode != ReplayMode.Response && i > 0)
                {
                    var delay = (pkt.Timestamp - packets[i - 1].Timestamp) / options.Speed;
                    if (delay > TimeSpan.Zero)
                        Thread.Sleep(delay);
                }

                var data = _builder.Build(pkt.Name, pkt.Fields, options.Overrides);
                stream.Write(data);
                sent++;
                var elapsed = DateTime.Now - startTime;
                Console.WriteLine($"[{elapsed:mm\\:ss\\.fff}] SEND {pkt.Name} ({data.Length} bytes)");

                // 응답 대기 (response/hybrid 모드)
                if (options.Mode != ReplayMode.Timing)
                {
                    var nextRecv = packets.Skip(i + 1).FirstOrDefault(p => p.Direction == "RECV");
                    if (nextRecv != null)
                    {
                        if (WaitForResponse(stream, recvBuffer, options.TimeoutMs, out var recvLen))
                        {
                            received++;
                            elapsed = DateTime.Now - startTime;
                            Console.WriteLine($"[{elapsed:mm\\:ss\\.fff}] RECV ({recvLen} bytes) OK");
                            
                            // RECV 패킷 인덱스까지 스킵
                            var recvIdx = packets.FindIndex(i + 1, p => p.Direction == "RECV");
                            if (recvIdx > i) i = recvIdx;
                        }
                        else
                        {
                            Console.WriteLine($"  ⚠ Response timeout ({options.TimeoutMs}ms)");
                        }
                    }
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
}
