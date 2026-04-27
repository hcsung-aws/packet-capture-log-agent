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

    public static async Task RunAsync(Program.CliOptions cli)
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

        CoverageTracker? tracker = cli.Coverage ? new CoverageTracker() : null;

        if (cli.Clients > 1)
        {
            Console.WriteLine($"=== FSM Load Test: {cli.Clients} clients ===\n");
            int completed = 0;
            var tasks = new Task[cli.Clients];
            for (int i = 0; i < cli.Clients; i++)
            {
                int idx = i;
                tasks[i] = Task.Run(async () =>
                {
                    try
                    {
                        var ctx = new ReplayContext();
                        var state = new Dictionary<string, object>();
                        var inner = new ParsingResponseHandler(protocol, host, port, TextWriter.Null, tracker);
                        var hdl = new TrackingResponseHandler(inner, state);
                        var sync = new BtSyncHandler(hdl, ctx, state);
                        var ics = new List<IReplayInterceptor>
                        {
                            new DynamicFieldInterceptor(new ScenarioBuilder().CollectAllDynamicFields(catalog), state),
                            new ProximityInterceptor(protocol.Semantics?.ProximityActions ?? new())
                        };
                        var ae = new ActionExecutor(protocol, catalog, tracker: tracker);
                        var fe = new FsmExecutor(ae, TextWriter.Null, tracker: tracker);
                        await fe.ExecuteAsync(fsm, host, port, sync, ctx, ics, durationSec: cli.Duration);
                        var c = Interlocked.Increment(ref completed);
                        Console.WriteLine($"  [{c}/{cli.Clients}] Client {idx + 1} completed");
                    }
                    catch (Exception ex)
                    {
                        var c = Interlocked.Increment(ref completed);
                        Console.WriteLine($"  [{c}/{cli.Clients}] Client {idx + 1} failed: {ex.Message}");
                    }
                });
            }
            await Task.WhenAll(tasks);
            Console.WriteLine($"\nAll {cli.Clients} clients finished.");
        }
        else
        {
            var logDir = Program.LogDir(cli.ProtocolPath!);
            using var logger = new ReplayLogger(logDir, console: Console.Out);

            var context = new ReplayContext();
            var sharedState = new Dictionary<string, object>();
            var innerHandler = new ParsingResponseHandler(protocol, host, port, logger, tracker);
            var handler = new TrackingResponseHandler(innerHandler, sharedState);
            var syncHandler = new BtSyncHandler(handler, context, sharedState);

            var interceptors = new List<IReplayInterceptor>
            {
                new DynamicFieldInterceptor(
                    new ScenarioBuilder().CollectAllDynamicFields(catalog), sharedState),
                new ProximityInterceptor(protocol.Semantics?.ProximityActions ?? new())
            };

            var actionExecutor = new ActionExecutor(protocol, catalog, tracker: tracker);
            var executor = new FsmExecutor(actionExecutor, logger, tracker: tracker);
            await executor.ExecuteAsync(fsm, host, port, syncHandler, context, interceptors, durationSec: cli.Duration);
        }

        if (tracker != null)
        {
            var report = CoverageReport.Generate(tracker, protocol, fsm: fsm);
            report.PrintToConsole(Console.Out);
            if (cli.CoverageOutput != null)
                report.SaveJson(cli.CoverageOutput);
        }
    }
}
