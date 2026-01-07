using System;
using System.Net;
using System.Net.Sockets;

namespace PacketCaptureAgent;

class Program
{
    static int? filterPort = null;
    static StreamWriter? logWriter = null;
    static PacketParser? parser = null;
    static PacketFormatter? formatter = null;
    static TcpStreamManager streamManager = new();

    static void Main(string[] args)
    {
        // 인자 파싱
        string? protocolPath = null;
        string? replayLog = null;
        string? target = null;
        string mode = "hybrid";
        int timeout = 5000;
        double speed = 1.0;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "-p" or "--protocol" when i + 1 < args.Length:
                    protocolPath = args[++i];
                    break;
                case "-r" or "--replay" when i + 1 < args.Length:
                    replayLog = args[++i];
                    break;
                case "-t" or "--target" when i + 1 < args.Length:
                    target = args[++i];
                    break;
                case "--mode" when i + 1 < args.Length:
                    mode = args[++i];
                    break;
                case "--timeout" when i + 1 < args.Length:
                    int.TryParse(args[++i], out timeout);
                    break;
                case "--speed" when i + 1 < args.Length:
                    double.TryParse(args[++i], out speed);
                    break;
            }
        }

        // Replay 모드
        if (replayLog != null)
        {
            var options = new ReplayOptions
            {
                Mode = mode switch
                {
                    "timing" => ReplayMode.Timing,
                    "response" => ReplayMode.Response,
                    _ => ReplayMode.Hybrid
                },
                TimeoutMs = timeout,
                Speed = speed
            };
            RunReplayMode(protocolPath, replayLog, target, options);
            return;
        }

        // Capture 모드
        RunCaptureMode(protocolPath);
    }

    static void RunReplayMode(string? protocolPath, string replayLog, string? target, ReplayOptions options)
    {
        Console.WriteLine("=== Packet Replay Mode ===\n");

        if (protocolPath == null || !File.Exists(protocolPath))
        {
            Console.WriteLine("프로토콜 파일 필요: -p protocol.json");
            return;
        }

        if (!File.Exists(replayLog))
        {
            Console.WriteLine($"로그 파일을 찾을 수 없음: {replayLog}");
            return;
        }

        var protocol = ProtocolLoader.Load(protocolPath);
        var replayer = new PacketReplayer(protocol);

        Console.WriteLine($"프로토콜: {protocol.Protocol.Name}");
        Console.WriteLine($"로그 파일: {replayLog}");

        var packets = replayer.ParseLog(replayLog);
        Console.WriteLine($"파싱된 패킷: {packets.Count}개");
        Console.WriteLine($"  SEND: {packets.Count(p => p.Direction == "SEND")}개");
        Console.WriteLine($"  RECV: {packets.Count(p => p.Direction == "RECV")}개");

        if (target == null)
        {
            Console.Write("\n대상 서버 (host:port): ");
            target = Console.ReadLine();
        }

        if (string.IsNullOrEmpty(target))
        {
            Console.WriteLine("대상 서버가 필요합니다.");
            return;
        }

        var parts = target.Split(':');
        if (parts.Length != 2 || !int.TryParse(parts[1], out var port))
        {
            Console.WriteLine("잘못된 형식. 예: 172.29.160.1:1239");
            return;
        }

        Console.WriteLine($"\n{parts[0]}:{port}로 재현 시작...\n");
        replayer.Replay(parts[0], port, packets, options);
    }

    static void RunCaptureMode(string? protocolPath)
    {
        Console.WriteLine("=== Packet Capture Agent (Raw Socket) ===\n");

        if (protocolPath != null)
        {
            if (File.Exists(protocolPath))
            {
                var protocol = ProtocolLoader.Load(protocolPath);
                parser = new PacketParser(protocol);
                formatter = new PacketFormatter(protocol);
                Console.WriteLine($"프로토콜 로드: {protocol.Protocol.Name} ({protocol.Packets.Count} packets)");
            }
            else
            {
                Console.WriteLine($"프로토콜 파일을 찾을 수 없음: {protocolPath}");
                Console.WriteLine("Raw 모드로 실행합니다.");
            }
        }
        else
        {
            Console.WriteLine("프로토콜 파일 없음 (Raw 모드)");
            Console.WriteLine("사용법: PacketCaptureAgent -p protocol.json");
        }

        var hostName = Dns.GetHostName();
        var addresses = Dns.GetHostAddresses(hostName)
            .Where(a => a.AddressFamily == AddressFamily.InterNetwork)
            .ToArray();

        if (addresses.Length == 0)
        {
            Console.WriteLine("IPv4 주소를 찾을 수 없습니다.");
            return;
        }

        Console.WriteLine("\n사용 가능한 IPv4 주소:");
        for (int i = 0; i < addresses.Length; i++)
            Console.WriteLine($"  [{i}] {addresses[i]}");

        Console.Write($"\n캡처할 인터페이스 번호 (0-{addresses.Length - 1}): ");
        if (!int.TryParse(Console.ReadLine(), out int idx) || idx < 0 || idx >= addresses.Length)
        {
            Console.WriteLine("잘못된 번호입니다.");
            return;
        }

        Console.Write("필터링할 포트 (전체 캡처는 Enter): ");
        var portInput = Console.ReadLine();
        if (!string.IsNullOrEmpty(portInput) && int.TryParse(portInput, out int port))
            filterPort = port;

        var localIP = addresses[idx];
        var logFileName = $"capture_{DateTime.Now:yyyyMMdd_HHmmss}.log";
        logWriter = new StreamWriter(logFileName, false) { AutoFlush = true };

        Console.WriteLine($"\n선택된 IP: {localIP}");
        Console.WriteLine(filterPort.HasValue ? $"필터: 포트 {filterPort}" : "필터: 없음 (전체 TCP)");
        Console.WriteLine($"로그 파일: {logFileName}");
        Console.WriteLine("패킷 캡처 시작... (Ctrl+C로 중지)\n");

        Log($"=== Capture Started: {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===");
        Log($"IP: {localIP}, Filter: {(filterPort.HasValue ? $"Port {filterPort}" : "All TCP")}");
        Log("");

        try
        {
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Raw, ProtocolType.IP);
            socket.Bind(new IPEndPoint(localIP, 0));
            socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.HeaderIncluded, true);
            socket.IOControl(IOControlCode.ReceiveAll, BitConverter.GetBytes(1), null);

            byte[] buffer = new byte[65535];
            
            Console.CancelKeyPress += (s, e) => { 
                e.Cancel = true; 
                socket.Close();
                Log($"\n=== Capture Stopped: {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===");
                logWriter?.Close();
            };

            while (true)
            {
                int len = socket.Receive(buffer);
                if (len > 0) ProcessPacket(buffer, len);
            }
        }
        catch (SocketException ex)
        {
            Console.WriteLine($"소켓 오류: {ex.Message}");
            Console.WriteLine("관리자 권한으로 실행했는지 확인하세요.");
        }
    }

    static void Log(string message, bool consoleOnly = false)
    {
        Console.WriteLine(message);
        if (!consoleOnly)
            logWriter?.WriteLine(message);
    }

    static void LogFile(string message)
    {
        logWriter?.WriteLine(message);
    }

    static void ProcessPacket(byte[] buffer, int length)
    {
        int ipHeaderLen = (buffer[0] & 0x0F) * 4;
        int protocol = buffer[9];
        
        if (protocol != 6) return;

        var srcIP = new IPAddress(new ReadOnlySpan<byte>(buffer, 12, 4));
        var dstIP = new IPAddress(new ReadOnlySpan<byte>(buffer, 16, 4));

        int tcpOffset = ipHeaderLen;
        int srcPort = (buffer[tcpOffset] << 8) | buffer[tcpOffset + 1];
        int dstPort = (buffer[tcpOffset + 2] << 8) | buffer[tcpOffset + 3];

        if (filterPort.HasValue && srcPort != filterPort && dstPort != filterPort)
            return;

        int tcpHeaderLen = ((buffer[tcpOffset + 12] >> 4) & 0x0F) * 4;
        int payloadOffset = ipHeaderLen + tcpHeaderLen;
        int payloadLen = length - payloadOffset;

        if (payloadLen <= 0) return;

        var connKey = new ConnectionKey(srcIP, srcPort, dstIP, dstPort);
        var payload = new ReadOnlySpan<byte>(buffer, payloadOffset, payloadLen);

        if (parser != null && formatter != null)
        {
            var stream = streamManager.GetOrCreate(connKey);
            stream.Append(payload);

            while (true)
            {
                var parsed = parser.TryParse(stream);
                if (parsed == null) break;

                string direction = filterPort.HasValue && dstPort == filterPort ? "SEND" : "RECV";
                var (consoleOut, fileOut) = formatter.Format(parsed, connKey, direction);
                Console.WriteLine(consoleOut);
                logWriter?.WriteLine(fileOut);
            }
        }
        else
        {
            var rawHex = Convert.ToHexString(payload);
            var shortHex = rawHex.Length > 64 ? rawHex[..64] + "..." : rawHex;
            
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] {srcIP}:{srcPort} -> {dstIP}:{dstPort} | Len: {payloadLen}");
            Console.WriteLine($"  raw: {shortHex}");
            
            logWriter?.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] {srcIP}:{srcPort} -> {dstIP}:{dstPort} | Len: {payloadLen}");
            logWriter?.WriteLine($"  raw: {rawHex}");
        }
    }

    static void ShowUsage()
    {
        Console.WriteLine("사용법:");
        Console.WriteLine("  캡처: PacketCaptureAgent -p protocol.json");
        Console.WriteLine("  재현: PacketCaptureAgent -p protocol.json -r capture.log -t host:port [options]");
        Console.WriteLine();
        Console.WriteLine("재현 옵션:");
        Console.WriteLine("  --mode timing|response|hybrid  재현 모드 (기본: hybrid)");
        Console.WriteLine("  --timeout 5000                 응답 대기 타임아웃 ms (기본: 5000)");
        Console.WriteLine("  --speed 1.0                    재생 속도 배율 (기본: 1.0)");
    }
}
