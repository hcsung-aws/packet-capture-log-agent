namespace PacketCaptureAgent;

static class FsmMode
{
    public static void RunBuild(Program.CliOptions cli)
    {
        if (Program.LoadProtocol(cli.ProtocolPath) == null) return;

        var recordingsPath = Program.RecordingsPath(cli.ProtocolPath!);
        var store = RecordingStore.Load(recordingsPath);

        if (store.Recordings.Count == 0)
        { Console.WriteLine($"녹화 없음: {recordingsPath}\n먼저 --analyze로 캡처 로그를 분석하세요."); return; }

        Console.WriteLine($"녹화 {store.Recordings.Count}건에서 FSM 전이 확률 생성...\n");

        var protocolName = Path.GetFileNameWithoutExtension(cli.ProtocolPath);
        var builder = new FsmBuilder();
        var fsm = builder.Build(store, $"{protocolName}_fsm");

        var fsmDir = Path.Combine(Path.GetDirectoryName(cli.ProtocolPath) ?? ".", "..", "behaviors");
        var fsmPath = Path.Combine(fsmDir, $"{protocolName}_fsm.json");
        fsm.Save(fsmPath);

        Console.WriteLine($"FSM 저장: {fsmPath}");
        Console.WriteLine($"  초기 상태: {fsm.InitialState}");
        Console.WriteLine($"  상태 수: {fsm.Transitions.Count}");
        foreach (var (from, targets) in fsm.Transitions)
        {
            var top = string.Join(", ", targets.OrderByDescending(kv => kv.Value).Take(3).Select(kv => $"{kv.Key}({kv.Value:P0})"));
            Console.WriteLine($"  {from} → {top}");
        }
    }

    public static void Run(Program.CliOptions cli)
    {
        var protocol = Program.LoadProtocol(cli.ProtocolPath);
        if (protocol == null) return;
        if (!File.Exists(cli.FsmPath))
        { Console.WriteLine($"FSM 파일을 찾을 수 없음: {cli.FsmPath}"); return; }

        var catalog = ActionCatalogBuilder.LoadCatalog(Program.CatalogPath(cli.ProtocolPath!));
        if (catalog == null) { Console.WriteLine($"Action Catalog 없음"); return; }

        var fsm = FsmDefinition.Load(cli.FsmPath!);

        var ep = Program.ParseTarget(cli.Target);
        if (ep == null) return;
        var (host, port) = ep.Value;

        var logDir = Program.LogDir(cli.ProtocolPath!);
        using var logger = new ReplayLogger(logDir, console: Console.Out);

        var context = new ReplayContext();
        var sharedState = new Dictionary<string, object>();
        var innerHandler = new ParsingResponseHandler(protocol, host, port, logger);
        var handler = new TrackingResponseHandler(innerHandler, sharedState);
        var syncHandler = new BtSyncHandler(handler, context, sharedState);

        var interceptors = new List<IReplayInterceptor>
        {
            new DynamicFieldInterceptor(
                new ScenarioBuilder().CollectAllDynamicFields(catalog), sharedState),
            new ProximityInterceptor(protocol.Semantics?.ProximityActions ?? new())
        };

        var executor = new FsmExecutor(new ActionExecutor(protocol, catalog), logger);
        executor.ExecuteAsync(fsm, host, port, syncHandler, context, interceptors, durationSec: cli.Duration).GetAwaiter().GetResult();
    }
}
