namespace PacketCaptureAgent;

/// <summary>패스스루 중 패킷을 관찰하여 FSM/BT 상태를 동기화.</summary>
public class PacketObserver
{
    private readonly Dictionary<string, string> _sendPacketToAction; // CS_MOVE → move
    public string? CurrentFsmState { get; private set; }
    public HashSet<string> ObservedActions { get; } = new();

    public PacketObserver(ActionCatalog catalog)
    {
        _sendPacketToAction = new(StringComparer.OrdinalIgnoreCase);
        foreach (var action in catalog.Actions)
            foreach (var pkt in action.Packets)
                if (pkt.Direction == "SEND")
                    _sendPacketToAction.TryAdd(pkt.Name, action.Id);
    }

    /// <summary>SEND 패킷 관찰 시 호출. FSM 상태 갱신 + BT 관찰 액션 추가.</summary>
    public void OnSendPacket(string packetName)
    {
        if (_sendPacketToAction.TryGetValue(packetName, out var actionId))
        {
            CurrentFsmState = actionId;
            ObservedActions.Add(actionId);
        }
    }
}
