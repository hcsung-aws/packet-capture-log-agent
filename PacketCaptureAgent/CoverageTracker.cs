namespace PacketCaptureAgent;

/// <summary>실행 중 커버리지 데이터 수집. 각 Executor에서 optional로 호출.</summary>
public class CoverageTracker
{
    public HashSet<string> SentPackets { get; } = new();
    public HashSet<string> ReceivedPackets { get; } = new();
    public HashSet<string> FsmStatesVisited { get; } = new();
    public HashSet<(string From, string To)> FsmTransitions { get; } = new();
    public HashSet<string> BtNodesExecuted { get; } = new();

    public void OnSend(string packetName) => SentPackets.Add(packetName);
    public void OnReceive(string packetName) => ReceivedPackets.Add(packetName);
    public void OnFsmTransition(string from, string to)
    {
        FsmStatesVisited.Add(from);
        FsmStatesVisited.Add(to);
        FsmTransitions.Add((from, to));
    }
    public void OnBtNode(string nodeId) => BtNodesExecuted.Add(nodeId);
}
