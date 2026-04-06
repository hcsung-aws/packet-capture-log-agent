namespace PacketCaptureAgent;

/// <summary>CS_ATTACK 감지 시 가장 가까운 NPC로 이동 후 targetUid를 교체하여 반환.</summary>
public class NpcAttackInterceptor : IReplayInterceptor
{
    private const int MoveIntervalMs = 500;
    private const int MaxMoveAttempts = 100;

    public int Priority => 100;

    public bool ShouldIntercept(ReplayPacket packet, GameWorldState world)
        => packet.Name == "CS_ATTACK" && world.Npcs.Count > 0;

    public async Task<ReplayPacket> PrepareAsync(ReplaySession session, ReplayPacket original)
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

            if (Math.Abs(px - npcX) + Math.Abs(py - npcY) <= 1)
            {
                output.WriteLine($"[Interceptor] In range of NPC {npcUid} at ({npcX},{npcY}), replacing targetUid");
                var fields = new Dictionary<string, object>(original.Fields) { ["targetUid"] = npcUid };
                return original with { Fields = fields };
            }

            var (tx, ty) = ProximityInterceptor.FindBestPos(px, py, npcX, npcY);
            output.WriteLine($"[Interceptor] Moving toward NPC {npcUid}: ({px},{py}) -> ({tx},{ty})");

            int dx = Math.Sign(tx - px);
            int dy = (dx == 0) ? Math.Sign(ty - py) : 0;

            await session.SendPacketAsync("CS_MOVE", new Dictionary<string, object> { ["dirX"] = dx, ["dirY"] = dy });
            await session.ReceiveAndProcessAsync();
            await Task.Delay(MoveIntervalMs);
        }

        output.WriteLine("[Interceptor] Max move attempts reached");
        return original;
    }
}
