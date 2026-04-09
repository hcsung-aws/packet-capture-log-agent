namespace PacketCaptureAgent;

static class ScenarioMode
{
    public static void RunBuild(Program.CliOptions cli)
    {
        if (Program.LoadProtocol(cli.ProtocolPath) == null) return;

        var catalogPath = Program.CatalogPath(cli.ProtocolPath!);
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

        var scenarioDir = Path.Combine(Path.GetDirectoryName(cli.ProtocolPath) ?? ".", "..", "scenarios");
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

    public static void RunReplay(Program.CliOptions cli)
    {
        if (cli.Clients <= 1)
            Console.WriteLine("=== Scenario Replay Mode ===\n");

        var protocol = Program.LoadProtocol(cli.ProtocolPath);
        if (protocol == null) return;
        if (!File.Exists(cli.ScenarioPath))
        {
            Console.WriteLine($"시나리오 파일을 찾을 수 없음: {cli.ScenarioPath}");
            return;
        }

        var catalogPath = Program.CatalogPath(cli.ProtocolPath!);
        var catalog = ActionCatalogBuilder.LoadCatalog(catalogPath);
        if (catalog == null)
        {
            Console.WriteLine($"Action Catalog 없음: {catalogPath}");
            return;
        }

        var scenario = ScenarioBuilder.Load(cli.ScenarioPath!);
        var builder = new ScenarioBuilder();

        var errors = builder.Validate(scenario, catalog);
        if (errors.Count > 0)
        {
            Console.WriteLine("⚠ 검증 오류:");
            foreach (var e in errors) Console.WriteLine($"  - {e}");
            return;
        }

        var target = cli.Target;
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

        if (cli.Clients > 1)
        {
            var logDir = Program.LogDir(cli.ProtocolPath!);
            var options = ReplayModeRunner.ParseReplayOptions(cli);
            LoadTestRunner.Run(protocol, scenario, catalog, parts[0], port, cli.Clients, options, logDir);
            return;
        }

        var singlePackets = builder.Build(scenario, catalog);
        var dynamicFields = builder.CollectDynamicFields(scenario, catalog);

        Console.WriteLine($"시나리오: {scenario.Name}");
        Console.WriteLine($"프로토콜: {protocol.Protocol.Name}");
        Console.WriteLine($"패킷: {singlePackets.Count}개 (SEND: {singlePackets.Count(p => p.Direction == "SEND")})");
        if (dynamicFields.Count > 0)
            Console.WriteLine($"Dynamic Fields: {dynamicFields.Count}건 (자동 주입)");

        Console.WriteLine($"\n{parts[0]}:{port}로 시나리오 재현 시작...\n");

        var singleLogDir = Program.LogDir(cli.ProtocolPath!);
        using var logger = new ReplayLogger(singleLogDir, console: Console.Out);

        var sharedState = new Dictionary<string, object>();
        var innerHandler = new ParsingResponseHandler(protocol, parts[0], port, logger);
        var handler = new TrackingResponseHandler(innerHandler, sharedState);
        var interceptors = new List<IReplayInterceptor>();
        if (dynamicFields.Count > 0)
            interceptors.Add(new DynamicFieldInterceptor(dynamicFields, sharedState));
        interceptors.Add(new ProximityInterceptor(protocol.Semantics?.ProximityActions ?? new()));

        var replayOptions = ReplayModeRunner.ParseReplayOptions(cli);
        var replayer = new PacketReplayer(protocol);
        replayer.ReplayAsync(parts[0], port, singlePackets, handler, replayOptions, interceptors, logger).GetAwaiter().GetResult();
    }
}
