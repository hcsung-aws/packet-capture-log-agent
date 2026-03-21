namespace PacketCaptureAgent.Tests;

/// <summary>
/// GameWorldState tests — 상태 갱신 로직 + FindNearestNpc 검증.
/// </summary>
public class GameWorldStateTests
{
    private static Dictionary<string, object> Fields(params (string k, object v)[] pairs)
        => pairs.ToDictionary(p => p.k, p => p.v);

    // --- SC_CHAR_INFO ---

    [Fact]
    public void Update_CharInfo_SetsPlayerCharUid_OnFirst()
    {
        var state = new GameWorldState();
        state.Update("SC_CHAR_INFO", Fields(("charUid", 100UL), ("posX", 5), ("posY", 10)));

        Assert.Equal(100UL, state.PlayerCharUid);
        Assert.Equal((5, 10), state.PlayerPos);
    }

    [Fact]
    public void Update_CharInfo_IgnoresOtherPlayer()
    {
        var state = new GameWorldState();
        // 내 캐릭터 먼저
        state.Update("SC_CHAR_INFO", Fields(("charUid", 100UL), ("posX", 5), ("posY", 10)));
        // 다른 플레이어
        state.Update("SC_CHAR_INFO", Fields(("charUid", 200UL), ("posX", 99), ("posY", 99)));

        Assert.Equal(100UL, state.PlayerCharUid);
        Assert.Equal((5, 10), state.PlayerPos); // 변경 안 됨
    }

    [Fact]
    public void Update_CharInfo_UpdatesMyPosition()
    {
        var state = new GameWorldState();
        state.Update("SC_CHAR_INFO", Fields(("charUid", 100UL), ("posX", 5), ("posY", 10)));
        state.Update("SC_CHAR_INFO", Fields(("charUid", 100UL), ("posX", 7), ("posY", 12)));

        Assert.Equal((7, 12), state.PlayerPos);
    }

    // --- SC_MOVE_RESULT ---

    [Fact]
    public void Update_MoveResult_Success_UpdatesPos()
    {
        var state = new GameWorldState();
        state.Update("SC_MOVE_RESULT", Fields(("success", 1), ("posX", 3), ("posY", 4)));

        Assert.Equal((3, 4), state.PlayerPos);
    }

    [Fact]
    public void Update_MoveResult_Failure_NoChange()
    {
        var state = new GameWorldState();
        state.Update("SC_CHAR_INFO", Fields(("charUid", 1UL), ("posX", 5), ("posY", 5)));
        state.Update("SC_MOVE_RESULT", Fields(("success", 0), ("posX", 99), ("posY", 99)));

        Assert.Equal((5, 5), state.PlayerPos);
    }

    // --- SC_NPC_SPAWN / SC_NPC_DEATH ---

    [Fact]
    public void Update_NpcSpawn_AddsNpc()
    {
        var state = new GameWorldState();
        state.Update("SC_NPC_SPAWN", Fields(("npcUid", 10UL), ("posX", 3), ("posY", 4)));

        Assert.Single(state.Npcs);
        Assert.Equal((3, 4), state.Npcs[10UL]);
    }

    [Fact]
    public void Update_NpcDeath_RemovesNpc()
    {
        var state = new GameWorldState();
        state.Update("SC_NPC_SPAWN", Fields(("npcUid", 10UL), ("posX", 3), ("posY", 4)));
        state.Update("SC_NPC_DEATH", Fields(("npcUid", 10UL)));

        Assert.Empty(state.Npcs);
    }

    [Fact]
    public void Update_NpcDeath_NonExistent_NoError()
    {
        var state = new GameWorldState();
        state.Update("SC_NPC_DEATH", Fields(("npcUid", 999UL)));

        Assert.Empty(state.Npcs);
    }

    // --- FindNearestNpc ---

    [Fact]
    public void FindNearestNpc_Empty_ReturnsNull()
    {
        var state = new GameWorldState();
        Assert.Null(state.FindNearestNpc());
    }

    [Fact]
    public void FindNearestNpc_SingleNpc()
    {
        var state = new GameWorldState();
        state.Update("SC_CHAR_INFO", Fields(("charUid", 1UL), ("posX", 0), ("posY", 0)));
        state.Update("SC_NPC_SPAWN", Fields(("npcUid", 10UL), ("posX", 3), ("posY", 4)));

        var nearest = state.FindNearestNpc();
        Assert.NotNull(nearest);
        Assert.Equal(10UL, nearest.Value.uid);
    }

    [Fact]
    public void FindNearestNpc_PicksClosest()
    {
        var state = new GameWorldState();
        state.Update("SC_CHAR_INFO", Fields(("charUid", 1UL), ("posX", 0), ("posY", 0)));
        state.Update("SC_NPC_SPAWN", Fields(("npcUid", 10UL), ("posX", 10), ("posY", 10))); // dist=20
        state.Update("SC_NPC_SPAWN", Fields(("npcUid", 20UL), ("posX", 1), ("posY", 1)));   // dist=2
        state.Update("SC_NPC_SPAWN", Fields(("npcUid", 30UL), ("posX", 5), ("posY", 0)));   // dist=5

        var nearest = state.FindNearestNpc();
        Assert.Equal(20UL, nearest!.Value.uid);
    }

    // --- Unknown packet ---

    [Fact]
    public void Update_UnknownPacket_NoError()
    {
        var state = new GameWorldState();
        state.Update("SC_WHATEVER", Fields(("foo", 1)));
        // 예외 없이 무시
        Assert.Equal(0UL, state.PlayerCharUid);
    }
}
