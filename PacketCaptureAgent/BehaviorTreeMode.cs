namespace PacketCaptureAgent;

static class BehaviorTreeMode
{
    public static void RunBuild(Program.CliOptions cli)
    {
        var protocol = Program.LoadProtocol(cli.ProtocolPath);
        if (protocol == null) return;

        var recordingsPath = Program.RecordingsPath(cli.ProtocolPath!);
        var store = RecordingStore.Load(recordingsPath);

        if (store.Recordings.Count == 0)
        { Console.WriteLine($"녹화 없음: {recordingsPath}\n먼저 --analyze로 캡처 로그를 분석하세요."); return; }

        Console.WriteLine($"녹화 {store.Recordings.Count}건에서 Behavior Tree 생성...\n");

        var catalog = ActionCatalogBuilder.LoadCatalog(Program.CatalogPath(cli.ProtocolPath!));

        var protocolName = Path.GetFileNameWithoutExtension(cli.ProtocolPath);
        var builder = new BehaviorTreeBuilder();
        var tree = builder.Build(store, $"{protocolName}_auto", catalog, protocol);

        var btDir = Path.Combine(Path.GetDirectoryName(cli.ProtocolPath) ?? ".", "..", "behaviors");
        var btPath = Path.Combine(btDir, $"{protocolName}_auto.json");
        tree.Save(btPath);

        Console.WriteLine($"Behavior Tree 저장: {btPath}");
        PrintTree(tree.Root, "");
    }

    public static async Task RunAsync(Program.CliOptions cli)
    {
        var protocol = Program.LoadProtocol(cli.ProtocolPath);
        if (protocol == null) return;
        if (!File.Exists(cli.BehaviorPath))
        { Console.WriteLine($"BT 파일을 찾을 수 없음: {cli.BehaviorPath}"); return; }

        var catalog = ActionCatalogBuilder.LoadCatalog(Program.CatalogPath(cli.ProtocolPath!));
        if (catalog == null) { Console.WriteLine("Action Catalog 없음"); return; }

        var tree = BehaviorTreeDefinition.Load(cli.BehaviorPath!);

        var ep = Program.ParseTarget(cli.Target);
        if (ep == null) return;
        var (host, port) = ep.Value;

        CoverageTracker? tracker = cli.Coverage ? new CoverageTracker() : null;

        if (cli.Clients > 1)
        {
            Console.WriteLine($"=== BT Validation: {cli.Clients} clients ===\n");
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
                        var be = new BehaviorTreeExecutor(ae, TextWriter.Null, protocol.Semantics, tracker: tracker);
                        await be.ExecuteAsync(tree, host, port, sync, ctx, ics, durationSec: cli.Duration);
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
            var executor = new BehaviorTreeExecutor(actionExecutor, logger, protocol.Semantics, tracker: tracker);
            await executor.ExecuteAsync(tree, host, port, syncHandler, context, interceptors, durationSec: cli.Duration);
        }

        if (tracker != null)
        {
            var report = CoverageReport.Generate(tracker, protocol, bt: tree);
            report.PrintToConsole(Console.Out);
            if (cli.CoverageOutput != null)
                report.SaveJson(cli.CoverageOutput);
        }
    }

    public static void RunEdit(Program.CliOptions cli)
    {
        var tree = BehaviorTreeDefinition.Load(cli.EditBehaviorPath!);
        tree = BehaviorTreeEditor.Edit(tree);
        tree.Save(cli.EditBehaviorPath!);
        Console.WriteLine($"\n저장 완료: {cli.EditBehaviorPath}");
    }

    public static void RunWebEditor(Program.CliOptions cli)
    {
        new BehaviorTreeWebEditor(cli.WebEditorPath!).Run(cli.WebPort);
    }

    static void PrintTree(BtNode node, string indent)
    {
        var cond = node.Condition != null ? $" [{node.Condition}]" : "";
        switch (node)
        {
            case BtAction a:
                Console.WriteLine($"{indent}Action: {a.Id}{cond}");
                break;
            case BtSequence s:
                Console.WriteLine($"{indent}Sequence{cond}");
                foreach (var c in s.Children) PrintTree(c, indent + "  ");
                break;
            case BtSelector s:
                Console.WriteLine($"{indent}Selector{cond}");
                foreach (var c in s.Children) PrintTree(c, indent + "  ");
                break;
            case BtRepeat r:
                Console.WriteLine($"{indent}Repeat x{r.Count}{cond}");
                PrintTree(r.Child, indent + "  ");
                break;
        }
    }
}
