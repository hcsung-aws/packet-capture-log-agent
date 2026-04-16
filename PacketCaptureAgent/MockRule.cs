using System.Text.Json;
using System.Text.Json.Serialization;

namespace PacketCaptureAgent;

/// <summary>C2S 패킷 → S2C 응답 규칙.</summary>
public class MockRule
{
    [JsonPropertyName("trigger")] public string Trigger { get; set; } = "";
    [JsonPropertyName("responses")] public List<MockResponse> Responses { get; set; } = new();
    [JsonPropertyName("stateful")] public bool Stateful { get; set; }
}

public class MockResponse
{
    [JsonPropertyName("packet")] public string Packet { get; set; } = "";
    [JsonPropertyName("fields")] public Dictionary<string, JsonElement>? Fields { get; set; }
}

public class MockRuleSet
{
    [JsonPropertyName("protocol")] public string Protocol { get; set; } = "";
    [JsonPropertyName("rules")] public List<MockRule> Rules { get; set; } = new();
    /// <summary>S2C 패킷 필드별 관측 범위. key="SC_NPC_SPAWN.posX" → {min, max}.</summary>
    [JsonPropertyName("field_ranges")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, FieldRange>? FieldRanges { get; set; }
}

public class FieldRange
{
    [JsonPropertyName("min")] public long Min { get; set; }
    [JsonPropertyName("max")] public long Max { get; set; }
}

/// <summary>세션별 게임 상태.</summary>
public class MockSession
{
    private static ulong _nextAccountUid = 1000;
    private static ulong _nextCharUid = 100;
    private static ulong _nextNpcUid = 1;

    public ulong AccountUid { get; set; }
    public ulong CharUid { get; set; }
    public string CharName { get; set; } = "";
    public int Level { get; set; } = 1;
    public uint Exp { get; set; }
    public ushort Hp { get; set; } = 100;
    public ushort MaxHp { get; set; } = 100;
    public short PosX { get; set; }
    public short PosY { get; set; }
    public ulong Gold { get; set; } = 1000;
    public bool LoggedIn { get; set; }
    public bool InGame { get; set; }

    public Dictionary<ulong, NpcState> Npcs { get; } = new();
    public List<InventorySlot> Inventory { get; } = new();
    public List<CharListEntry> Characters { get; } = new();

    public ulong AllocAccountUid() => Interlocked.Increment(ref _nextAccountUid);
    public ulong AllocCharUid() => Interlocked.Increment(ref _nextCharUid);
    public ulong AllocNpcUid() => Interlocked.Increment(ref _nextNpcUid);

    public void SpawnInitialNpcs(Dictionary<string, FieldRange>? ranges, int count = 5)
    {
        var rng = new Random();
        var posRange = GetRange(ranges, "SC_NPC_SPAWN.posX", 0, 19);
        var hpRange = GetRange(ranges, "SC_NPC_SPAWN.hp", 30, 30);

        for (int i = 0; i < count; i++)
        {
            var uid = AllocNpcUid();
            Npcs[uid] = new NpcState
            {
                Uid = uid,
                PosX = (short)rng.Next((int)posRange.Min, (int)posRange.Max + 1),
                PosY = (short)rng.Next((int)posRange.Min, (int)posRange.Max + 1),
                Hp = (ushort)rng.Next((int)hpRange.Min, (int)hpRange.Max + 1),
                MaxHp = (ushort)hpRange.Max,
                NpcType = (byte)(i % 2)
            };
        }
    }

    /// <summary>초기 플레이어 좌표를 field_ranges에서 결정.</summary>
    public void InitPosition(Dictionary<string, FieldRange>? ranges)
    {
        var rng = new Random();
        var xRange = GetRange(ranges, "SC_CHAR_INFO.posX", 0, 19);
        var yRange = GetRange(ranges, "SC_CHAR_INFO.posY", 0, 19);
        PosX = (short)rng.Next((int)xRange.Min, (int)xRange.Max + 1);
        PosY = (short)rng.Next((int)yRange.Min, (int)yRange.Max + 1);
    }

    private static FieldRange GetRange(Dictionary<string, FieldRange>? ranges, string key,
        long defaultMin, long defaultMax)
    {
        if (ranges != null && ranges.TryGetValue(key, out var r)) return r;
        return new FieldRange { Min = defaultMin, Max = defaultMax };
    }
}

public class NpcState
{
    public ulong Uid { get; set; }
    public short PosX { get; set; }
    public short PosY { get; set; }
    public ushort Hp { get; set; }
    public ushort MaxHp { get; set; }
    public byte NpcType { get; set; }
}

public class InventorySlot
{
    public byte Slot { get; set; }
    public ushort ItemId { get; set; }
    public string ItemName { get; set; } = "";
}

public class CharListEntry
{
    public ulong CharUid { get; set; }
    public string Name { get; set; } = "";
    public byte CharType { get; set; }
    public ushort Level { get; set; } = 1;
}
