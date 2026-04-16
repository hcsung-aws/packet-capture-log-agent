namespace PacketCaptureAgent;

/// <summary>--build-mock / --mock CLI 진입점.</summary>
static class MockServerMode
{
    public static void RunBuild(Program.CliOptions cli)
    {
        var protocol = Program.LoadProtocol(cli.ProtocolPath);
        if (protocol == null) return;

        var catalogPath = Program.CatalogPath(cli.ProtocolPath!);
        var catalog = ActionCatalogBuilder.LoadCatalog(catalogPath);
        if (catalog == null)
        {
            Console.WriteLine($"ActionCatalog 없음: {catalogPath}");
            Console.WriteLine("먼저 --analyze로 캡처 로그를 분석하세요.");
            return;
        }

        var recordingsPath = Program.RecordingsPath(cli.ProtocolPath!);
        var recordings = RecordingStore.Load(recordingsPath);
        if (recordings.Recordings.Count == 0)
            Console.WriteLine("[MockRuleBuilder] recordings 없음 — 필드 범위는 프로토콜 기본값 사용");

        var builder = new MockRuleBuilder();
        var ruleSet = builder.Build(catalog, protocol, recordings.Recordings.Count > 0 ? recordings : null);

        var outDir = Path.Combine(Path.GetDirectoryName(cli.ProtocolPath!) ?? ".", "..", "behaviors");
        var outPath = Path.Combine(outDir, $"{Path.GetFileNameWithoutExtension(cli.ProtocolPath)}_mock_rules.json");
        MockRuleBuilder.Save(outPath, ruleSet);

        Console.WriteLine($"MockRuleSet 생성 완료: {outPath}");
        Console.WriteLine($"  규칙 수: {ruleSet.Rules.Count} (상태 추적: {ruleSet.Rules.Count(r => r.Stateful)})");
    }

    public static async Task RunAsync(Program.CliOptions cli)
    {
        var protocol = Program.LoadProtocol(cli.ProtocolPath);
        if (protocol == null) return;

        if (cli.MockPath == null)
        {
            Console.WriteLine("목업 규칙 파일 필요: --mock rules.json");
            return;
        }

        var ruleSet = MockRuleBuilder.Load(cli.MockPath);
        if (ruleSet == null)
        {
            Console.WriteLine($"규칙 파일 로드 실패: {cli.MockPath}");
            return;
        }

        var port = cli.Port ?? 9000;
        var server = new MockServer(protocol, ruleSet);

        using var cts = new CancellationTokenSource();
        var serverTask = server.RunAsync(port, cts.Token);

        Console.WriteLine("q=종료");
        while (true)
        {
            if (Console.KeyAvailable)
            {
                var key = Console.ReadKey(true);
                if (key.KeyChar == 'q') break;
            }
            await Task.Delay(100);
        }

        cts.Cancel();
        try { await serverTask; } catch (OperationCanceledException) { }
        Console.WriteLine("[MockServer] 종료");
    }
}
