using System;
using System.Net;
using System.Net.Sockets;

namespace PacketCaptureAgent;

class Program
{
    // ── 공통 헬퍼 ──

    internal static ProtocolDefinition? LoadProtocol(string? path)
    {
        if (path == null || !File.Exists(path))
        { Console.WriteLine("프로토콜 파일 필요: -p protocol.json"); return null; }
        return ProtocolLoader.Load(path);
    }

    internal static (string host, int port)? ParseTarget(string? target)
    {
        if (string.IsNullOrEmpty(target))
        {
            Console.Write("\n대상 서버 (host:port): ");
            target = Console.ReadLine();
        }
        if (string.IsNullOrEmpty(target)) { Console.WriteLine("대상 서버가 필요합니다."); return null; }
        var parts = target.Split(':');
        if (parts.Length != 2 || !int.TryParse(parts[1], out var port))
        { Console.WriteLine("잘못된 형식. 예: 172.29.160.1:9000"); return null; }
        return (parts[0], port);
    }

    internal static string CatalogPath(string protocolPath) =>
        Path.Combine(Path.GetDirectoryName(protocolPath) ?? ".", "..", "actions",
            $"{Path.GetFileNameWithoutExtension(protocolPath)}_actions.json");

    internal static string RecordingsPath(string protocolPath) =>
        Path.Combine(Path.GetDirectoryName(protocolPath) ?? ".", "..", "recordings",
            $"{Path.GetFileNameWithoutExtension(protocolPath)}_recordings.json");

    internal static string LogDir(string protocolPath) =>
        Path.Combine(Path.GetDirectoryName(protocolPath) ?? ".", "..", "logs");

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
        string? BehaviorPath = null,
        bool BuildBehavior = false,
        string? EditBehaviorPath = null,
        int? Duration = null,
        string? WebEditorPath = null,
        int WebPort = 8080,
        string? FsmPath = null,
        bool BuildFsm = false,
        bool AgentMode = false,
        int AgentPort = 8090,
        string? ManagerPath = null);

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
        bool buildBehavior = false;
        string? editBehaviorPath = null;
        int? duration = null;
        string? webEditorPath = null;
        int webPort = 8080;
        string? fsmPath = null;
        bool buildFsm = false;
        bool agentMode = false;
        int agentPort = 8090;
        string? managerPath = null;

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
                case "--build-behavior":
                    buildBehavior = true;
                    break;
                case "--edit-behavior" when i + 1 < args.Length:
                    editBehaviorPath = args[++i];
                    break;
                case "--duration" when i + 1 < args.Length:
                    if (int.TryParse(args[++i], out var dur)) duration = dur;
                    break;
                case "--web-editor" when i + 1 < args.Length:
                    webEditorPath = args[++i];
                    break;
                case "--web-port" when i + 1 < args.Length:
                    int.TryParse(args[++i], out webPort);
                    break;
                case "--fsm" when i + 1 < args.Length:
                    fsmPath = args[++i];
                    break;
                case "--build-fsm":
                    buildFsm = true;
                    break;
                case "--agent-mode":
                    agentMode = true;
                    break;
                case "--agent-port" when i + 1 < args.Length:
                    int.TryParse(args[++i], out agentPort);
                    break;
                case "--manager" when i + 1 < args.Length:
                    managerPath = args[++i];
                    break;
            }
        }

        return new CliOptions(protocolPath, replayLog, target, mode, timeout, speed, port, showHelp, analyzeLog, scenarioPath, buildScenario, clients, behaviorPath, buildBehavior, editBehaviorPath, duration, webEditorPath, webPort, fsmPath, buildFsm, agentMode, agentPort, managerPath);
    }

    static void Main(string[] args)
    {
        var cli = ParseArgs(args);

        if (cli.ShowHelp) { ShowUsage(); return; }
        if (cli.AgentMode) { AgentMode.Run(cli); return; }
        if (cli.ManagerPath != null) { ManagerMode.Run(cli); return; }
        if (cli.AnalyzeLog != null) { AnalyzeMode.Run(cli); return; }
        if (cli.BuildScenario) { ScenarioMode.RunBuild(cli); return; }
        if (cli.FsmPath != null) { FsmMode.Run(cli); return; }
        if (cli.BuildFsm) { FsmMode.RunBuild(cli); return; }
        if (cli.BehaviorPath != null) { BehaviorTreeMode.Run(cli); return; }
        if (cli.BuildBehavior) { BehaviorTreeMode.RunBuild(cli); return; }
        if (cli.EditBehaviorPath != null) { BehaviorTreeMode.RunEdit(cli); return; }
        if (cli.WebEditorPath != null) { BehaviorTreeMode.RunWebEditor(cli); return; }
        if (cli.ScenarioPath != null) { ScenarioMode.RunReplay(cli); return; }
        if (cli.ReplayLog != null) { ReplayModeRunner.Run(cli); return; }
        CaptureMode.Run(cli);
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
        Console.WriteLine("  --build-behavior               녹화에서 Behavior Tree 자동 생성");
        Console.WriteLine("  --edit-behavior bt.json        Behavior Tree 인터랙티브 편집");
        Console.WriteLine();
        Console.WriteLine("재현 옵션:");
        Console.WriteLine("  --mode timing|response|hybrid  재현 모드 (기본: hybrid)");
        Console.WriteLine("  --timeout 5000                 응답 대기 타임아웃 ms (기본: 5000)");
        Console.WriteLine("  --speed 1.0                    재생 속도 배율 (기본: 1.0)");
    }
}
