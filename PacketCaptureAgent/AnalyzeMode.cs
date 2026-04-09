namespace PacketCaptureAgent;

static class AnalyzeMode
{
    public static void Run(Program.CliOptions cli) => RunAnalyze(cli.ProtocolPath, cli.AnalyzeLog!);

    internal static void RunAnalyze(string? protocolPath, string logPath)
    {
        var protocol = Program.LoadProtocol(protocolPath);
        if (protocol == null) return;
        if (!File.Exists(logPath))
        {
            Console.WriteLine($"로그 파일을 찾을 수 없음: {logPath}");
            return;
        }
        var replayer = new PacketReplayer(protocol);
        var clientPackets = replayer.ParseLogByClient(logPath);

        Console.WriteLine($"클라이언트 {clientPackets.Count}개 감지\n");

        var protocolName = Path.GetFileNameWithoutExtension(protocolPath);
        var catalogPath = Program.CatalogPath(protocolPath!);
        var recordingsPath = Program.RecordingsPath(protocolPath!);

        ActionCatalog? catalog = null;

        foreach (var (clientPort, packets) in clientPackets)
        {
            Console.WriteLine($"--- Client :{clientPort} ({packets.Count} packets) ---\n");

            var analyzer = new SequenceAnalyzer();
            var classified = analyzer.Classify(packets);
            var groups = analyzer.GroupPackets(classified);
            Console.Write(analyzer.FormatDiagram(groups));

            if (clientPort == clientPackets.Keys.First())
            {
                var phased = analyzer.AssignPhases(groups, protocol);
                var mdPath = Path.ChangeExtension(logPath, null) + "_sequence.md";
                File.WriteAllText(mdPath, $"# Sequence Diagram\n\n{analyzer.FormatMermaid(phased)}");
                Console.WriteLine($"\nMermaid 다이어그램 저장: {mdPath}");
            }

            var dynamicFields = analyzer.DetectDynamicFields(packets, protocol.FieldMappings);
            var catalogBuilder = new ActionCatalogBuilder();
            var newActions = catalogBuilder.BuildActions(packets, classified, dynamicFields, protocol, logPath);

            var existing = catalog ?? ActionCatalogBuilder.LoadCatalog(catalogPath);
            catalog = catalogBuilder.Merge(existing, newActions, protocol);

            if (dynamicFields.Count > 0)
            {
                Console.WriteLine($"Dynamic Fields: {dynamicFields.Count}건");
                foreach (var df in dynamicFields)
                    Console.WriteLine($"  {df.SendPacket}.{df.SendField} ← {df.SourcePacket}.{df.SourceField}");
            }

            var recording = RecordingStore.ExtractFromCapture(packets, classified, catalog, logPath);
            var store = RecordingStore.Load(recordingsPath);
            store.Recordings.Add(recording);
            store.Save(recordingsPath);
            Console.WriteLine($"Recording 저장: {store.Recordings.Count}건 (이번 {recording.Sequence.Count} steps)\n");
        }

        if (catalog != null)
        {
            ActionCatalogBuilder.SaveCatalog(catalogPath, catalog);
            Console.WriteLine($"Action Catalog 저장: {catalogPath} ({catalog.Actions.Count} actions)");
        }
    }
}
