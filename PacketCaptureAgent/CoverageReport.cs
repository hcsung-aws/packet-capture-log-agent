using System.Text.Json;

namespace PacketCaptureAgent;

/// <summary>CoverageTracker 데이터 + 정의를 비교하여 커버리지 리포트 생성.</summary>
public class CoverageReport
{
    public record CoverageSection(string Name, int Total, int Covered, List<string> Missing);

    public List<CoverageSection> Sections { get; } = new();

    public static CoverageReport Generate(CoverageTracker tracker, ProtocolDefinition protocol,
        FsmDefinition? fsm = null, BehaviorTreeDefinition? bt = null)
    {
        var report = new CoverageReport();

        // 패킷 타입 커버리지
        var allPackets = protocol.Packets.Select(p => p.Name).ToHashSet();
        var coveredPackets = tracker.SentPackets.Union(tracker.ReceivedPackets).ToHashSet();
        var missingPackets = allPackets.Except(coveredPackets).OrderBy(n => n).ToList();
        report.Sections.Add(new("Packet Types", allPackets.Count, allPackets.Count - missingPackets.Count, missingPackets));

        // FSM 커버리지
        if (fsm != null)
        {
            var allStates = fsm.Transitions.Keys.ToHashSet();
            var missingStates = allStates.Except(tracker.FsmStatesVisited).OrderBy(n => n).ToList();
            report.Sections.Add(new("FSM States", allStates.Count, allStates.Count - missingStates.Count, missingStates));

            var allTransitions = fsm.Transitions
                .SelectMany(kv => kv.Value.Keys.Select(to => $"{kv.Key} → {to}"))
                .ToHashSet();
            var coveredTransitions = tracker.FsmTransitions
                .Select(t => $"{t.From} → {t.To}")
                .ToHashSet();
            var missingTransitions = allTransitions.Except(coveredTransitions).OrderBy(n => n).ToList();
            report.Sections.Add(new("FSM Transitions", allTransitions.Count, allTransitions.Count - missingTransitions.Count, missingTransitions));
        }

        // BT 노드 커버리지
        if (bt != null)
        {
            var allNodes = new HashSet<string>();
            CollectActionNodes(bt.Root, allNodes);
            var missingNodes = allNodes.Except(tracker.BtNodesExecuted).OrderBy(n => n).ToList();
            report.Sections.Add(new("BT Nodes", allNodes.Count, allNodes.Count - missingNodes.Count, missingNodes));
        }

        return report;
    }

    public void PrintToConsole(TextWriter output)
    {
        output.WriteLine("\n=== Coverage Report ===");
        foreach (var s in Sections)
        {
            var pct = s.Total > 0 ? (double)s.Covered / s.Total * 100 : 100;
            output.WriteLine($"{s.Name}: {s.Covered}/{s.Total} ({pct:F1}%)");
            if (s.Missing.Count > 0)
                output.WriteLine($"  Missing: {string.Join(", ", s.Missing)}");
        }
    }

    public void SaveJson(string path)
    {
        var data = Sections.Select(s => new
        {
            s.Name, s.Total, s.Covered,
            Percentage = s.Total > 0 ? Math.Round((double)s.Covered / s.Total * 100, 1) : 100,
            s.Missing
        });
        File.WriteAllText(path, JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static void CollectActionNodes(BtNode node, HashSet<string> result)
    {
        if (node is BtAction action)
            result.Add(action.Id);
        else if (node is BtSequence seq)
            foreach (var child in seq.Children) CollectActionNodes(child, result);
        else if (node is BtSelector sel)
            foreach (var child in sel.Children) CollectActionNodes(child, result);
        else if (node is BtRepeat rep)
            CollectActionNodes(rep.Child, result);
    }
}
