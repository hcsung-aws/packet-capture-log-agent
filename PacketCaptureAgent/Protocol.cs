using System.Text.Json;
using System.Text.Json.Serialization;

namespace PacketCaptureAgent;

public class ProtocolDefinition
{
    [JsonPropertyName("protocol")]
    public ProtocolInfo Protocol { get; set; } = new();

    [JsonPropertyName("transforms")]
    public List<TransformDefinition>? Transforms { get; set; }

    [JsonPropertyName("types")]
    public Dictionary<string, TypeDefinition> Types { get; set; } = new();

    [JsonPropertyName("packets")]
    public List<PacketDefinition> Packets { get; set; } = new();

    [JsonPropertyName("phases")]
    public Dictionary<string, List<int>>? Phases { get; set; }

    [JsonPropertyName("field_mappings")]
    public List<FieldMapping>? FieldMappings { get; set; }

    [JsonPropertyName("semantics")]
    public SemanticsDefinition? Semantics { get; set; }

    public PacketDefinition? GetPacketByType(int type) =>
        Packets.FirstOrDefault(p => p.Type == type);
}

public class TransformDefinition
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    [JsonPropertyName("options")]
    public Dictionary<string, object>? Options { get; set; }
}

public class ProtocolInfo
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("version")]
    public string Version { get; set; } = "1.0";

    [JsonPropertyName("endian")]
    public string Endian { get; set; } = "little";

    [JsonPropertyName("pack")]
    public int Pack { get; set; } = 1;  // struct packing: 1, 2, 4, 8

    [JsonPropertyName("header")]
    public HeaderInfo Header { get; set; } = new();
}

public class HeaderInfo
{
    [JsonPropertyName("size_field")]
    public string SizeField { get; set; } = "size";

    [JsonPropertyName("type_field")]
    public string TypeField { get; set; } = "type";

    [JsonPropertyName("size")]
    public int? Size { get; set; }  // explicit header size (optional, auto-calculated if null)

    [JsonPropertyName("fields")]
    public List<HeaderFieldInfo>? Fields { get; set; }

    public int GetHeaderSize()
    {
        if (Size.HasValue) return Size.Value;
        if (Fields == null || Fields.Count == 0) return 4; // default
        return Fields.Max(f => f.Offset + GetTypeSize(f.Type));
    }

    private static int GetTypeSize(string type) => type switch
    {
        "int8" or "uint8" => 1,
        "int16" or "uint16" => 2,
        "int32" or "uint32" => 4,
        "int64" or "uint64" => 8,
        _ => 4
    };
}

public class HeaderFieldInfo
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("type")]
    public string Type { get; set; } = "int32";

    [JsonPropertyName("offset")]
    public int Offset { get; set; }
}

public class TypeDefinition
{
    [JsonPropertyName("kind")]
    public string Kind { get; set; } = "";

    [JsonPropertyName("base")]
    public string? Base { get; set; }

    [JsonPropertyName("values")]
    public Dictionary<string, int>? Values { get; set; }

    [JsonPropertyName("fields")]
    public List<FieldDefinition>? Fields { get; set; }
}

public class PacketDefinition
{
    [JsonPropertyName("type")]
    public int Type { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("fields")]
    public List<FieldDefinition> Fields { get; set; } = new();
}

public class FieldDefinition
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    [JsonPropertyName("length")]
    public object? Length { get; set; }  // int or "remaining"

    [JsonPropertyName("length_type")]
    public string? LengthType { get; set; }  // for string_prefixed: "uint8", "uint16", "uint32"

    [JsonPropertyName("encoding")]
    public string? Encoding { get; set; }  // "utf8" (default), "utf16", "ascii"

    [JsonPropertyName("count_field")]
    public string? CountField { get; set; }

    [JsonPropertyName("element")]
    public string? Element { get; set; }  // for array type

    [JsonPropertyName("switch_field")]
    public string? SwitchField { get; set; }  // for conditional type

    [JsonPropertyName("cases")]
    public Dictionary<string, List<FieldDefinition>>? Cases { get; set; }  // for conditional type

    public int GetLength(int remaining = 0)
    {
        if (Length is JsonElement je)
        {
            if (je.ValueKind == JsonValueKind.Number)
                return je.GetInt32();
            if (je.ValueKind == JsonValueKind.String && je.GetString() == "remaining")
                return remaining;
        }
        if (Length is int i) return i;
        if (Length is string s && s == "remaining") return remaining;
        return 0;
    }
}

public static class ProtocolLoader
{
    public static ProtocolDefinition Load(string path)
    {
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<ProtocolDefinition>(json) 
            ?? throw new InvalidOperationException("Failed to load protocol");
    }
}

public class FieldMapping
{
    [JsonPropertyName("target")]
    public string Target { get; set; } = "";  // "CS_ATTACK.targetUid"

    [JsonPropertyName("source")]
    public string Source { get; set; } = "";  // "SC_NPC_SPAWN.npcUid" | "external" | "static"
}

public class SemanticsDefinition
{
    [JsonPropertyName("interaction_sources")]
    public List<InteractionSource>? InteractionSources { get; set; }

    [JsonPropertyName("state_conditions")]
    public List<StateCondition>? StateConditions { get; set; }

    [JsonPropertyName("proximity_actions")]
    public List<ProximityAction>? ProximityActions { get; set; }
}

public class InteractionSource
{
    [JsonPropertyName("source_prefix")]
    public string SourcePrefix { get; set; } = "";

    [JsonPropertyName("condition")]
    public string Condition { get; set; } = "";
}

public class StateCondition
{
    [JsonPropertyName("action_pattern")]
    public string ActionPattern { get; set; } = "";

    [JsonPropertyName("state_field")]
    public string StateField { get; set; } = "";

    [JsonPropertyName("min_value")]
    public int MinValue { get; set; }
}

public class ProximityAction
{
    [JsonPropertyName("action_pattern")]
    public string ActionPattern { get; set; } = "";

    [JsonPropertyName("npc_type")]
    public int NpcType { get; set; }

    [JsonPropertyName("range")]
    public int Range { get; set; } = 1;
}
