namespace PacketCaptureAgent;

/// <summary>CS_ATTACK 감지 시 가장 가까운 NPC로 이동 후 targetUid를 교체하여 반환.</summary>
public class NpcAttackInterceptor : IReplayInterceptor
{
    private const int MoveIntervalMs = 500;
    private const int MaxMoveAttempts = 100;

    public int Priority => 100;

    public bool ShouldIntercept(ReplayPacket packet, GameWorldState world)
        => packet.Name == "CS_ATTACK" && world.Npcs.Count > 0;

    public ReplayPacket Prepare(ReplaySession session, ReplayPacket original)
    {
        var output = session.Output;
        for (int attempt = 0; attempt < MaxMoveAttempts; attempt++)
        {
            var nearest = session.World.FindNearestNpc();
            if (nearest == null)
            {
                output.WriteLine("[Interceptor] No NPC found, sending original packet");
                return original;
            }

            var (npcUid, npcX, npcY) = nearest.Value;
            var (px, py) = session.World.PlayerPos;

            // 공격 가능: 맨해튼 거리 1 이하
            if (Math.Abs(px - npcX) + Math.Abs(py - npcY) <= 1)
            {
                output.WriteLine($"[Interceptor] In range of NPC {npcUid} at ({npcX},{npcY}), replacing targetUid");
                var fields = new Dictionary<string, object>(original.Fields) { ["targetUid"] = npcUid };
                return original with { Fields = fields };
            }

            // 공격 가능 위치 계산 (NPC 상하좌우 중 가장 가까운 칸)
            var (tx, ty) = FindBestAttackPos(px, py, npcX, npcY);
            output.WriteLine($"[Interceptor] Moving toward NPC {npcUid}: ({px},{py}) -> ({tx},{ty})");

            // 한 칸 이동
            int dx = Math.Sign(tx - px);
            int dy = (dx == 0) ? Math.Sign(ty - py) : 0;

            session.SendPacket("CS_MOVE", new Dictionary<string, object> { ["dirX"] = dx, ["dirY"] = dy });
            session.ReceiveAndProcess();
            Thread.Sleep(MoveIntervalMs);
        }

        output.WriteLine("[Interceptor] Max move attempts reached");
        return original;
    }

    /// <summary>NPC 인접 4칸 중 플레이어에 가장 가까운 위치.</summary>
    private static (int x, int y) FindBestAttackPos(int px, int py, int npcX, int npcY)
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
