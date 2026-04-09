namespace PacketCaptureAgent;

/// <summary>동적 필드 자동 주입 인터셉터 — sharedState에서 값을 읽어 SEND 패킷 필드에 주입.</summary>
public class DynamicFieldInterceptor : IReplayInterceptor
{
    private readonly Dictionary<string, List<ActionDynamicField>> _fieldsByPacket;
    private readonly Dictionary<string, object> _sharedState;

    public int Priority => 0;

    public DynamicFieldInterceptor(List<ActionDynamicField> dynamicFields, Dictionary<string, object> sharedState)
    {
        _sharedState = sharedState;
        _fieldsByPacket = dynamicFields
            .GroupBy(df => df.Packet)
            .ToDictionary(g => g.Key, g => g.ToList());
    }

    public bool ShouldIntercept(ReplayPacket packet, GameWorldState world)
        => packet.Direction == "SEND" && _fieldsByPacket.ContainsKey(packet.Name);

    public Task<ReplayPacket> PrepareAsync(ReplaySession session, ReplayPacket original)
    {
        if (!_fieldsByPacket.TryGetValue(original.Name, out var mappings))
            return Task.FromResult(original);

        var fields = new Dictionary<string, object>(original.Fields);
        foreach (var m in mappings)
            if (_sharedState.TryGetValue(m.Source, out var value))
                fields[m.Field] = value;

        return Task.FromResult(original with { Fields = fields });
    }
}
