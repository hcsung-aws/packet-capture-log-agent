using System.Net;
using System.Net.Sockets;

namespace PacketCaptureAgent;

static class CaptureMode
{
    public static void Run(Program.CliOptions cli)
    {
        Console.WriteLine("=== Packet Capture Agent (Raw Socket) ===\n");

        PacketParser? parser = null;
        PacketFormatter? formatter = null;

        if (cli.ProtocolPath != null)
        {
            if (File.Exists(cli.ProtocolPath))
            {
                var protocol = ProtocolLoader.Load(cli.ProtocolPath);
                parser = new PacketParser(protocol);
                formatter = new PacketFormatter(protocol);
                Console.WriteLine($"프로토콜 로드: {protocol.Protocol.Name} ({protocol.Packets.Count} packets)");
            }
            else
            {
                Console.WriteLine($"프로토콜 파일을 찾을 수 없음: {cli.ProtocolPath}");
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

        var filterPort = cli.Port;
        if (!filterPort.HasValue)
        {
            Console.Write("필터링할 포트 (전체 캡처는 Enter): ");
            var portInput = Console.ReadLine();
            if (!string.IsNullOrEmpty(portInput) && int.TryParse(portInput, out int port))
                filterPort = port;
        }

        var localIP = addresses[idx];
        var logFileName = $"capture_{DateTime.Now:yyyyMMdd_HHmmss}.log";
        using var logWriter = new StreamWriter(logFileName, false) { AutoFlush = true };

        Console.WriteLine($"\n선택된 IP: {localIP}");
        Console.WriteLine(filterPort.HasValue ? $"필터: 포트 {filterPort}" : "필터: 없음 (전체 TCP)");
        Console.WriteLine($"로그 파일: {logFileName}");
        Console.WriteLine("패킷 캡처 시작... (Q: 분석 후 종료, Ctrl+C: 즉시 종료)\n");

        void LogBoth(string msg) { Console.WriteLine(msg); logWriter.WriteLine(msg); }
        void LogFile(string msg) { logWriter.WriteLine(msg); }

        LogBoth($"=== Capture Started: {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===");
        LogBoth($"IP: {localIP}, Filter: {(filterPort.HasValue ? $"Port {filterPort}" : "All TCP")}");
        LogBoth("");

        var session = new CaptureSession(parser, formatter, new TcpStreamManager(), filterPort, LogBoth, LogFile);
        bool analyzeOnExit = false;

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
            };

            Task.Run(() =>
            {
                while (true)
                {
                    if (Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Q)
                    {
                        analyzeOnExit = true;
                        socket.Close();
                        break;
                    }
                    Thread.Sleep(100);
                }
            });

            while (true)
            {
                int len = socket.Receive(buffer);
                if (len > 0) session.ProcessPacket(buffer, len);
            }
        }
        catch (SocketException) when (analyzeOnExit) { }
        catch (SocketException ex) when (ex.SocketErrorCode == SocketError.Interrupted || ex.SocketErrorCode == SocketError.OperationAborted) { }
        catch (SocketException ex)
        {
            Console.WriteLine($"소켓 오류: {ex.Message}");
            Console.WriteLine("관리자 권한으로 실행했는지 확인하세요.");
        }

        LogBoth($"\n=== Capture Stopped: {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===");
        logWriter.Flush();
        logWriter.Dispose();

        if (analyzeOnExit && cli.ProtocolPath != null)
        {
            Console.WriteLine("\n캡처 로그 분석 중...\n");
            AnalyzeMode.RunAnalyze(cli.ProtocolPath, logFileName);
        }
    }
}
