namespace PacketCaptureAgent;

/// <summary>실행 중 커버리지 데이터 수집. 각 Executor에서 optional로 호출. Thread-safe.</summary>
public class CoverageTracker
{
    private readonly object _lock = new();
    public HashSet<string> SentPackets { get; } = new();
    public HashSet<string> ReceivedPackets { get; } = new();
    public HashSet<string> FsmStatesVisited { get; } = new();
    public HashSet<(string From, string To)> FsmTransitions { get; } = new();
    public HashSet<string> BtNodesExecuted { get; } = new();

    public void OnSend(string packetName) { lock (_lock) SentPackets.Add(packetName); }
    public void OnReceive(string packetName) { lock (_lock) ReceivedPackets.Add(packetName); }
    public void OnFsmTransition(string from, string to)
    {
        lock (_lock)
        {
            FsmStatesVisited.Add(from);
            FsmStatesVisited.Add(to);
            FsmTransitions.Add((from, to));
        }
    }
    public void OnBtNode(string nodeId) { lock (_lock) BtNodesExecuted.Add(nodeId); }
}
