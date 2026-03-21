namespace PacketCaptureAgent;

/// <summary>리플레이 중 서버 응답에서 추출한 게임 월드 상태.</summary>
public class GameWorldState
{
    public ulong PlayerCharUid { get; private set; }
    public (int x, int y) PlayerPos { get; set; }
    public Dictionary<ulong, (int x, int y)> Npcs { get; } = new();

    /// <summary>서버 응답 패킷으로 상태 갱신.</summary>
    public void Update(string packetName, Dictionary<string, object> fields)
    {
        switch (packetName)
        {
            case "SC_CHAR_INFO":
                if (!fields.TryGetValue("charUid", out var uid)) break;
                var charUid = ToUlong(uid);
                if (PlayerCharUid == 0) PlayerCharUid = charUid;
                if (charUid == PlayerCharUid &&
                    fields.TryGetValue("posX", out var cx) && fields.TryGetValue("posY", out var cy))
                    PlayerPos = (ToInt(cx), ToInt(cy));
                break;
            case "SC_MOVE_RESULT":
                if (fields.TryGetValue("success", out var s) && ToInt(s) != 0 &&
                    fields.TryGetValue("posX", out var mx) && fields.TryGetValue("posY", out var my))
                    PlayerPos = (ToInt(mx), ToInt(my));
                break;
            case "SC_NPC_SPAWN":
                if (fields.TryGetValue("npcUid", out var su) &&
                    fields.TryGetValue("posX", out var sx) && fields.TryGetValue("posY", out var sy))
                    Npcs[ToUlong(su)] = (ToInt(sx), ToInt(sy));
                break;
            case "SC_NPC_DEATH":
                if (fields.TryGetValue("npcUid", out var du))
                    Npcs.Remove(ToUlong(du));
                break;
        }
    }

    /// <summary>플레이어에서 가장 가까운 NPC의 uid와 위치. 없으면 null.</summary>
    public (ulong uid, int x, int y)? FindNearestNpc()
    {
        if (Npcs.Count == 0) return null;
        ulong bestUid = 0;
        int bestDist = int.MaxValue;
        foreach (var (uid, pos) in Npcs)
        {
            int dist = Math.Abs(pos.x - PlayerPos.x) + Math.Abs(pos.y - PlayerPos.y);
            if (dist < bestDist) { bestDist = dist; bestUid = uid; }
        }
        var best = Npcs[bestUid];
        return (bestUid, best.x, best.y);
    }

    private static int ToInt(object v) => Convert.ToInt32(v);
    private static ulong ToUlong(object v) => Convert.ToUInt64(v);
}
