namespace PacketCaptureAgent;

/// <summary>semantics.proximity_actions 기반 범용 인터셉터.
/// 해당 액션 실행 전 매칭 npcType의 가장 가까운 NPC로 이동.</summary>
public class ProximityInterceptor : IReplayInterceptor
{
    private const int MoveIntervalMs = 500;
    private const int MaxMoveAttempts = 100;

    private readonly List<ProximityAction> _rules;

    public ProximityInterceptor(List<ProximityAction> rules) => _rules = rules;

    public int Priority => 100;

    public bool ShouldIntercept(ReplayPacket packet, GameWorldState world)
        => FindRule(packet.Name) != null && world.Npcs.Count > 0;

    public ReplayPacket Prepare(ReplaySession session, ReplayPacket original)
    {
        var rule = FindRule(original.Name)!;
        var output = session.Output;

        for (int attempt = 0; attempt < MaxMoveAttempts; attempt++)
        {
            var nearest = session.World.FindNearestNpc(rule.NpcType);
            if (nearest == null)
            {
                output.WriteLine($"[Interceptor] No NPC (type={rule.NpcType}) found");
                return original;
            }

            var (npcUid, npcX, npcY) = nearest.Value;
            var (px, py) = session.World.PlayerPos;

            if (Math.Abs(px - npcX) + Math.Abs(py - npcY) <= rule.Range)
            {
                output.WriteLine($"[Interceptor] In range of NPC {npcUid} at ({npcX},{npcY})");
                // attack 패턴이면 targetUid 교체
                if (original.Fields.ContainsKey("targetUid"))
                    return original with { Fields = new Dictionary<string, object>(original.Fields) { ["targetUid"] = npcUid } };
                return original;
            }

            var (tx, ty) = FindBestPos(px, py, npcX, npcY);
            output.WriteLine($"[Interceptor] Moving toward NPC {npcUid}: ({px},{py}) -> ({tx},{ty})");

            int dx = Math.Sign(tx - px);
            int dy = (dx == 0) ? Math.Sign(ty - py) : 0;
            session.SendPacket("CS_MOVE", new Dictionary<string, object> { ["dirX"] = dx, ["dirY"] = dy });
            session.ReceiveAndProcess();
            Thread.Sleep(MoveIntervalMs);
        }

        output.WriteLine("[Interceptor] Max move attempts reached");
        return original;
    }

    private ProximityAction? FindRule(string packetName)
    {
        // CS_ATTACK → "attack", CS_QUEST_LIST → "quest"
        var actionId = packetName.Replace("CS_", "").ToLower();
        return _rules.FirstOrDefault(r => actionId.Contains(r.ActionPattern));
    }

    internal static (int x, int y) FindBestPos(int px, int py, int npcX, int npcY)
    {
        ReadOnlySpan<(int dx, int dy)> dirs = [(0, -1), (0, 1), (-1, 0), (1, 0)];
        int bestDist = int.MaxValue;
        (int x, int y) best = (npcX, npcY - 1);
        foreach (var (dx, dy) in dirs)
        {
            int ax = npcX + dx, ay = npcY + dy;
            int dist = Math.Abs(ax - px) + Math.Abs(ay - py);
            if (dist < bestDist) { bestDist = dist; best = (ax, ay); }
        }
        return best;
    }
}
