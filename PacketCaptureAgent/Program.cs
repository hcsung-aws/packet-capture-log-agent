using System;
using System.Net;
using System.Net.Sockets;

namespace PacketCaptureAgent;

class Program
{
    public record CliOptions(
        string? ProtocolPath = null,
        string? ReplayLog = null,
        string? Target = null,
        string Mode = "hybrid",
        int Timeout = 5000,
        double Speed = 1.0,
        int? Port = null,
        bool ShowHelp = false,
        string? AnalyzeLog = null,
        string? ScenarioPath = null,
        bool BuildScenario = false,
        int Clients = 1,
        string? BehaviorPath = null);

    public static CliOptions ParseArgs(string[] args)
    {
        string? protocolPath = null;
        string? replayLog = null;
        string? target = null;
        string mode = "hybrid";
        int timeout = 5000;
        double speed = 1.0;
        int? port = null;
        bool showHelp = false;
        string? analyzeLog = null;
        string? scenarioPath = null;
        bool buildScenario = false;
        int clients = 1;
        string? behaviorPath = null;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "-h" or "--help":
                    showHelp = true;
                    break;
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
                case "--port" when i + 1 < args.Length:
                    if (int.TryParse(args[++i], out var p)) port = p;
                    break;
                case "--analyze" when i + 1 < args.Length:
                    analyzeLog = args[++i];
                    break;
                case "-s" or "--scenario" when i + 1 < args.Length:
                    scenarioPath = args[++i];
                    break;
                case "--build-scenario":
                    buildScenario = true;
                    break;
                case "--clients" when i + 1 < args.Length:
                    int.TryParse(args[++i], out clients);
                    break;
                case "--behavior" when i + 1 < args.Length:
                    behaviorPath = args[++i];
                    break;
            }
        }

        return new CliOptions(protocolPath, replayLog, target, mode, timeout, speed, port, showHelp, analyzeLog, scenarioPath, buildScenario, clients, behaviorPath);
    }

    static void Main(string[] args)
    {
        var cli = ParseArgs(args);

        if (cli.ShowHelp)
        {
            ShowUsage();
            return;
        }

        if (cli.AnalyzeLog != null)
        {
            RunAnalyzeMode(cli.ProtocolPath, cli.AnalyzeLog);
            return;
        }

        if (cli.BuildScenario)
        {
            RunBuildScenarioMode(cli.ProtocolPath);
            return;
        }

        if (cli.BehaviorPath != null)
        {
            RunBehaviorTreeMode(cli.ProtocolPath, cli.BehaviorPath, cli.Target);
            return;
        }

        if (cli.ScenarioPath != null)
        {
            var options = new ReplayOptions
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
            RunScenarioReplayMode(cli.ProtocolPath, cli.ScenarioPath, cli.Target, options, cli.Clients);
            return;
        }

        if (cli.ReplayLog != null)
        {
            var options = new ReplayOptions
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
            RunReplayMode(cli.ProtocolPath, cli.ReplayLog, cli.Target, options);
            return;
        }

        RunCaptureMode(cli.ProtocolPath, cli.Port);
    }

    static void RunAnalyzeMode(string? protocolPath, string logPath)
    {
        if (protocolPath == null || !File.Exists(protocolPath))
        {
            Console.WriteLine("프로토콜 파일 필요: -p protocol.json");
            return;
        }
        if (!File.Exists(logPath))
        {
            Console.WriteLine($"로그 파일을 찾을 수 없음: {logPath}");
            return;
        }

        var protocol = ProtocolLoader.Load(protocolPath);
        var replayer = new PacketReplayer(protocol);
        var packets = replayer.ParseLog(logPath);

        var analyzer = new SequenceAnalyzer();
        var classified = analyzer.Classify(packets);
        var groups = analyzer.GroupPackets(classified);
        Console.Write(analyzer.FormatDiagram(groups));

        var phased = analyzer.AssignPhases(groups, protocol);
        var mdPath = Path.ChangeExtension(logPath, null) + "_sequence.md";
        File.WriteAllText(mdPath, $"# Sequence Diagram\n\n{analyzer.FormatMermaid(phased)}");
        Console.WriteLine($"\nMermaid 다이어그램 저장: {mdPath}");

        // Action Catalog 생성
        var dynamicFields = analyzer.DetectDynamicFields(packets, protocol.FieldMappings);
        var catalogBuilder = new ActionCatalogBuilder();
        var newActions = catalogBuilder.BuildActions(packets, classified, dynamicFields, protocol, logPath);

        var protocolName = Path.GetFileNameWithoutExtension(protocolPath);
        var catalogDir = Path.Combine(Path.GetDirectoryName(protocolPath) ?? ".", "..", "actions");
        var catalogPath = Path.Combine(catalogDir, $"{protocolName}_actions.json");

        var existing = ActionCatalogBuilder.LoadCatalog(catalogPath);
        var catalog = catalogBuilder.Merge(existing, newActions, protocol);
        ActionCatalogBuilder.SaveCatalog(catalogPath, catalog);

        Console.WriteLine($"Action Catalog 저장: {catalogPath} ({catalog.Actions.Count} actions)");
        if (dynamicFields.Count > 0)
        {
            Console.WriteLine($"Dynamic Fields 감지: {dynamicFields.Count}건");
            foreach (var df in dynamicFields)
                Console.WriteLine($"  {df.SendPacket}.{df.SendField} ← {df.SourcePacket}.{df.SourceField}");
        }

        // Recording 추출 + 저장
        var recording = RecordingStore.ExtractFromCapture(packets, classified, catalog, logPath);
        var recordingsDir = Path.Combine(Path.GetDirectoryName(protocolPath) ?? ".", "..", "recordings");
        var recordingsPath = Path.Combine(recordingsDir, $"{protocolName}_recordings.json");
        var store = RecordingStore.Load(recordingsPath);
        store.Recordings.Add(recording);
        store.Save(recordingsPath);
        Console.WriteLine($"Recording 저장: {recordingsPath} ({store.Recordings.Count}건, 이번 {recording.Sequence.Count} steps)");
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
        var logDir = Path.Combine(Path.GetDirectoryName(protocolPath) ?? ".", "..", "logs");
        using var logger = new ReplayLogger(logDir, console: Console.Out);
        var handler = new ParsingResponseHandler(protocol, parts[0], port, logger);
        var interceptors = new List<IReplayInterceptor> { new NpcAttackInterceptor() };
        replayer.Replay(parts[0], port, packets, handler, options, interceptors, logger);
    }

    static void RunCaptureMode(string? protocolPath, int? filterPort)
    {
        Console.WriteLine("=== Packet Capture Agent (Raw Socket) ===\n");

        PacketParser? parser = null;
        PacketFormatter? formatter = null;

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

            // Q 키 감시 스레드
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
        catch (SocketException) when (analyzeOnExit)
        {
            // Q 키로 정상 종료
        }
        catch (SocketException ex) when (ex.SocketErrorCode == SocketError.Interrupted || ex.SocketErrorCode == SocketError.OperationAborted)
        {
            // Ctrl+C로 정상 종료
        }
        catch (SocketException ex)
        {
            Console.WriteLine($"소켓 오류: {ex.Message}");
            Console.WriteLine("관리자 권한으로 실행했는지 확인하세요.");
        }

        LogBoth($"\n=== Capture Stopped: {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===");
        logWriter.Flush();
        logWriter.Dispose();

        if (analyzeOnExit && protocolPath != null)
        {
            Console.WriteLine("\n캡처 로그 분석 중...\n");
            RunAnalyzeMode(protocolPath, logFileName);
        }
    }

    // ── 시나리오 빌드 모드 (기존 코드와 완전 분리) ──

    static void RunBuildScenarioMode(string? protocolPath)
    {
        if (protocolPath == null || !File.Exists(protocolPath))
        {
            Console.WriteLine("프로토콜 파일 필요: -p protocol.json");
            return;
        }

        var protocolName = Path.GetFileNameWithoutExtension(protocolPath);
        var catalogPath = Path.Combine(Path.GetDirectoryName(protocolPath) ?? ".", "..", "actions", $"{protocolName}_actions.json");
        var catalog = ActionCatalogBuilder.LoadCatalog(catalogPath);
        if (catalog == null || catalog.Actions.Count == 0)
        {
            Console.WriteLine($"Action Catalog 없음: {catalogPath}");
            Console.WriteLine("먼저 --analyze로 캡처 로그를 분석하세요.");
            return;
        }

        var scenario = ScenarioBuilder.BuildInteractive(catalog);
        if (scenario.Steps.Count == 0)
        {
            Console.WriteLine("시나리오가 비어 있습니다.");
            return;
        }

        var builder = new ScenarioBuilder();
        var errors = builder.Validate(scenario, catalog);
        if (errors.Count > 0)
        {
            Console.WriteLine("\n⚠ 검증 오류:");
            foreach (var e in errors) Console.WriteLine($"  - {e}");
            Console.Write("계속 저장하시겠습니까? (y/N): ");
            if (Console.ReadLine()?.Trim().ToLower() != "y") return;
        }

        var scenarioDir = Path.Combine(Path.GetDirectoryName(protocolPath) ?? ".", "..", "scenarios");
        var safeName = string.Join("_", scenario.Name.Split(Path.GetInvalidFileNameChars()));
        var scenarioPath = Path.Combine(scenarioDir, $"{safeName}.json");
        ScenarioBuilder.Save(scenarioPath, scenario);

        var packets = builder.Build(scenario, catalog);
        var sendCount = packets.Count(p => p.Direction == "SEND");
        Console.WriteLine($"\n시나리오 저장: {scenarioPath}");
        Console.WriteLine($"  Steps: {scenario.Steps.Count}, 패킷: {packets.Count} (SEND: {sendCount})");

        var dynamicFields = builder.CollectDynamicFields(scenario, catalog);
        if (dynamicFields.Count > 0)
        {
            Console.WriteLine($"  Dynamic Fields: {dynamicFields.Count}건 (재현 시 자동 주입)");
            foreach (var df in dynamicFields)
                Console.WriteLine($"    {df.Packet}.{df.Field} ← {df.Source}");
        }
    }

    // ── 시나리오 재현 모드 (기존 코드와 완전 분리) ──

    static void RunScenarioReplayMode(string? protocolPath, string scenarioPath, string? target, ReplayOptions options, int clients = 1)
    {
        if (clients <= 1)
            Console.WriteLine("=== Scenario Replay Mode ===\n");

        if (protocolPath == null || !File.Exists(protocolPath))
        {
            Console.WriteLine("프로토콜 파일 필요: -p protocol.json");
            return;
        }
        if (!File.Exists(scenarioPath))
        {
            Console.WriteLine($"시나리오 파일을 찾을 수 없음: {scenarioPath}");
            return;
        }

        var protocol = ProtocolLoader.Load(protocolPath);
        var protocolName = Path.GetFileNameWithoutExtension(protocolPath);
        var catalogPath = Path.Combine(Path.GetDirectoryName(protocolPath) ?? ".", "..", "actions", $"{protocolName}_actions.json");
        var catalog = ActionCatalogBuilder.LoadCatalog(catalogPath);
        if (catalog == null)
        {
            Console.WriteLine($"Action Catalog 없음: {catalogPath}");
            return;
        }

        var scenario = ScenarioBuilder.Load(scenarioPath);
        var builder = new ScenarioBuilder();

        var errors = builder.Validate(scenario, catalog);
        if (errors.Count > 0)
        {
            Console.WriteLine("⚠ 검증 오류:");
            foreach (var e in errors) Console.WriteLine($"  - {e}");
            return;
        }

        if (target == null)
        {
            Console.Write("\n대상 서버 (host:port): ");
            target = Console.ReadLine();
        }
        if (string.IsNullOrEmpty(target)) { Console.WriteLine("대상 서버가 필요합니다."); return; }

        var parts = target.Split(':');
        if (parts.Length != 2 || !int.TryParse(parts[1], out var port))
        {
            Console.WriteLine("잘못된 형식. 예: 172.29.160.1:9000");
            return;
        }

        // 다중 클라이언트 → LoadTestRunner
        if (clients > 1)
        {
            var logDir = Path.Combine(Path.GetDirectoryName(protocolPath) ?? ".", "..", "logs");
            LoadTestRunner.Run(protocol, scenario, catalog, parts[0], port, clients, options, logDir);
            return;
        }

        // 단일 클라이언트 (기존 동작)
        var packets = builder.Build(scenario, catalog);
        var dynamicFields = builder.CollectDynamicFields(scenario, catalog);

        Console.WriteLine($"시나리오: {scenario.Name}");
        Console.WriteLine($"프로토콜: {protocol.Protocol.Name}");
        Console.WriteLine($"패킷: {packets.Count}개 (SEND: {packets.Count(p => p.Direction == "SEND")})");
        if (dynamicFields.Count > 0)
            Console.WriteLine($"Dynamic Fields: {dynamicFields.Count}건 (자동 주입)");

        Console.WriteLine($"\n{parts[0]}:{port}로 시나리오 재현 시작...\n");

        var singleLogDir = Path.Combine(Path.GetDirectoryName(protocolPath) ?? ".", "..", "logs");
        using var logger = new ReplayLogger(singleLogDir, console: Console.Out);

        // 동적 필드 주입: 공유 상태 + TrackingResponseHandler + DynamicFieldInterceptor
        var sharedState = new Dictionary<string, object>();
        var innerHandler = new ParsingResponseHandler(protocol, parts[0], port, logger);
        var handler = new TrackingResponseHandler(innerHandler, sharedState);
        var interceptors = new List<IReplayInterceptor>();
        if (dynamicFields.Count > 0)
            interceptors.Add(new DynamicFieldInterceptor(dynamicFields, sharedState));
        interceptors.Add(new NpcAttackInterceptor());

        var replayer = new PacketReplayer(protocol);
        replayer.Replay(parts[0], port, packets, handler, options, interceptors, logger);
    }


    static void RunBehaviorTreeMode(string? protocolPath, string behaviorPath, string? target)
    {
        if (protocolPath == null || !File.Exists(protocolPath))
        { Console.WriteLine("프로토콜 파일 필요: -p protocol.json"); return; }
        if (!File.Exists(behaviorPath))
        { Console.WriteLine($"BT 파일을 찾을 수 없음: {behaviorPath}"); return; }

        var protocol = ProtocolLoader.Load(protocolPath);
        var protocolName = Path.GetFileNameWithoutExtension(protocolPath);
        var catalogPath = Path.Combine(Path.GetDirectoryName(protocolPath) ?? ".", "..", "actions", $"{protocolName}_actions.json");
        var catalog = ActionCatalogBuilder.LoadCatalog(catalogPath);
        if (catalog == null) { Console.WriteLine($"Action Catalog 없음: {catalogPath}"); return; }

        var tree = BehaviorTreeDefinition.Load(behaviorPath);

        if (target == null)
        { Console.Write("대상 서버 (host:port): "); target = Console.ReadLine(); }
        if (string.IsNullOrEmpty(target)) { Console.WriteLine("대상 서버가 필요합니다."); return; }
        var parts = target.Split(':');
        if (parts.Length != 2 || !int.TryParse(parts[1], out var port))
        { Console.WriteLine("잘못된 형식. 예: 172.29.160.1:9000"); return; }

        var logDir = Path.Combine(Path.GetDirectoryName(protocolPath) ?? ".", "..", "logs");
        using var logger = new ReplayLogger(logDir, console: Console.Out);

        var context = new ReplayContext();
        var sharedState = new Dictionary<string, object>();
        var innerHandler = new ParsingResponseHandler(protocol, parts[0], port, logger);
        var handler = new TrackingResponseHandler(innerHandler, sharedState);

        // TrackingResponseHandler → SessionState 동기화
        var syncHandler = new BtSyncHandler(handler, context, sharedState);

        var interceptors = new List<IReplayInterceptor>
        {
            new DynamicFieldInterceptor(
                new ScenarioBuilder().CollectAllDynamicFields(catalog), sharedState),
            new NpcAttackInterceptor()
        };

        var executor = new BehaviorTreeExecutor(new ActionExecutor(protocol, catalog), logger);
        executor.Execute(tree, parts[0], port, syncHandler, context, interceptors);
    }

    /// <summary>응답 처리 시 SessionState를 ReplayContext와 sharedState 양쪽에 동기화.</summary>
    class BtSyncHandler : IResponseHandler
    {
        private readonly TrackingResponseHandler _inner;
        private readonly ReplayContext _context;
        private readonly Dictionary<string, object> _sharedState;

        public BtSyncHandler(TrackingResponseHandler inner, ReplayContext context, Dictionary<string, object> sharedState)
        { _inner = inner; _context = context; _sharedState = sharedState; }

        public int OnResponse(byte[] data, int length, ReplayContext context)
        {
            int count = _inner.OnResponse(data, length, _context);
            // SessionState → sharedState 동기화 (ConditionEvaluator + DynamicFieldInterceptor 양쪽 사용)
            foreach (var kv in _context.SessionState)
                _sharedState[kv.Key] = kv.Value;
            return count;
        }
    }
    static void ShowUsage()
    {
        Console.WriteLine("사용법:");
        Console.WriteLine("  캡처: PacketCaptureAgent -p protocol.json [--port 9000]");
        Console.WriteLine("  재현: PacketCaptureAgent -p protocol.json -r capture.log -t host:port [options]");
        Console.WriteLine("  분석: PacketCaptureAgent -p protocol.json --analyze capture.log");
        Console.WriteLine("  시나리오 생성: PacketCaptureAgent -p protocol.json --build-scenario");
        Console.WriteLine("  시나리오 재현: PacketCaptureAgent -p protocol.json -s scenario.json -t host:port [options]");
        Console.WriteLine();
        Console.WriteLine("캡처 옵션:");
        Console.WriteLine("  --port 9000                    필터링할 포트 (미지정 시 인터랙티브 입력)");
        Console.WriteLine();
        Console.WriteLine("분석 옵션:");
        Console.WriteLine("  --analyze capture.log          캡처 로그 시퀀스 분석 + 다이어그램 출력");
        Console.WriteLine();
        Console.WriteLine("시나리오 옵션:");
        Console.WriteLine("  --build-scenario               인터랙티브 시나리오 생성");
        Console.WriteLine("  -s, --scenario scenario.json   시나리오 파일로 재현");
        Console.WriteLine("  --clients N                    다중 클라이언트 부하 테스트 (기본: 1)");
        Console.WriteLine("  --behavior bt.json             Behavior Tree 실행");
        Console.WriteLine();
        Console.WriteLine("재현 옵션:");
        Console.WriteLine("  --mode timing|response|hybrid  재현 모드 (기본: hybrid)");
        Console.WriteLine("  --timeout 5000                 응답 대기 타임아웃 ms (기본: 5000)");
        Console.WriteLine("  --speed 1.0                    재생 속도 배율 (기본: 1.0)");
    }
}
