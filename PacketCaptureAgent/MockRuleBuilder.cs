using System.Text.Json;

namespace PacketCaptureAgent;

/// <summary>ActionCatalog → MockRuleSet 자동 생성.</summary>
public class MockRuleBuilder
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    // 상태 추적 대상 C2S 패킷 (이들은 MockServer가 동적 응답 생성)
    private static readonly HashSet<string> StatefulTriggers = new()
    {
        "CS_LOGIN", "CS_CHAR_CREATE", "CS_CHAR_SELECT",
        "CS_MOVE", "CS_ATTACK",
        "CS_SHOP_OPEN", "CS_SHOP_BUY", "CS_SHOP_SELL",
        "CS_ITEM_USE", "CS_ITEM_EQUIP",
        "CS_HEARTBEAT"
    };

    public MockRuleSet Build(ActionCatalog catalog, ProtocolDefinition protocol,
        RecordingStore? recordings = null)
    {
        var rules = new List<MockRule>();

        foreach (var action in catalog.Actions)
        {
            var sendPkt = action.Packets.FirstOrDefault(p => p.Direction == "SEND");
            if (sendPkt == null) continue;

            var trigger = sendPkt.Name;
            var stateful = StatefulTriggers.Contains(trigger);

            var responses = new List<MockResponse>();
            foreach (var pkt in action.Packets.Where(p => p.Direction == "RECV"))
            {
                // "SC_NPC_SPAWN ×5" → "SC_NPC_SPAWN"
                var name = pkt.Name.Split(' ')[0];
                var def = protocol.Packets.FirstOrDefault(p => p.Name == name);
                if (def == null) continue;

                var fields = BuildDefaultFields(def);
                responses.Add(new MockResponse { Packet = name, Fields = fields });
            }

            if (responses.Count == 0) continue;

            rules.Add(new MockRule
            {
                Trigger = trigger,
                Responses = responses,
                Stateful = stateful
            });
        }

        // heartbeat 규칙 추가 (카탈로그에 없을 수 있음)
        if (!rules.Any(r => r.Trigger == "CS_HEARTBEAT"))
            rules.Add(new MockRule
            {
                Trigger = "CS_HEARTBEAT",
                Responses = new() { new MockResponse { Packet = "SC_HEARTBEAT" } },
                Stateful = true
            });

        var fieldRanges = ExtractFieldRanges(recordings);

        return new MockRuleSet
        {
            Protocol = protocol.Protocol.Name,
            Rules = rules,
            FieldRanges = fieldRanges.Count > 0 ? fieldRanges : null
        };
    }

    /// <summary>recordings의 recv_state에서 S2C 필드별 min/max 추출.</summary>
    internal static Dictionary<string, FieldRange> ExtractFieldRanges(RecordingStore? store)
    {
        var ranges = new Dictionary<string, FieldRange>();
        if (store == null) return ranges;

        foreach (var rec in store.Recordings)
            foreach (var step in rec.Sequence)
                foreach (var (key, val) in step.RecvState)
                {
                    if (!TryGetLong(val, out var num)) continue;
                    if (ranges.TryGetValue(key, out var r))
                    {
                        r.Min = Math.Min(r.Min, num);
                        r.Max = Math.Max(r.Max, num);
                    }
                    else
                        ranges[key] = new FieldRange { Min = num, Max = num };
                }

        return ranges;
    }

    private static bool TryGetLong(object val, out long result)
    {
        result = 0;
        try
        {
            if (val is JsonElement je)
            {
                if (je.ValueKind == JsonValueKind.Number && je.TryGetInt64(out result)) return true;
                return false;
            }
            result = Convert.ToInt64(val);
            return true;
        }
        catch { return false; }
    }

    /// <summary>패킷 정의에서 기본값 필드 맵 생성.</summary>
    private Dictionary<string, JsonElement>? BuildDefaultFields(PacketDefinition def)
    {
        if (def.Fields == null || def.Fields.Count == 0) return null;
        var dict = new Dictionary<string, JsonElement>();
        foreach (var f in def.Fields)
        {
            if (f.Type == "array") continue; // 배열은 동적 생성
            var val = DefaultValue(f.Type);
            dict[f.Name] = JsonSerializer.SerializeToElement(val);
        }
        return dict.Count > 0 ? dict : null;
    }

    private static object DefaultValue(string type) => type switch
    {
        "uint8" or "int8" => 0,
        "uint16" or "int16" => 0,
        "uint32" or "int32" => 0,
        "uint64" or "int64" => 0L,
        "string" => "",
        _ => 0
    };

    public static MockRuleSet? Load(string path)
    {
        if (!File.Exists(path)) return null;
        return JsonSerializer.Deserialize<MockRuleSet>(File.ReadAllText(path), JsonOpts);
    }

    public static void Save(string path, MockRuleSet ruleSet)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(ruleSet, JsonOpts));
    }
}
