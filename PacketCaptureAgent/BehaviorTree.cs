using System.Text.Json;
using System.Text.Json.Serialization;

namespace PacketCaptureAgent;

/// <summary>Behavior Tree 정의. JSON 직렬화 가능.</summary>
public class BehaviorTreeDefinition
{
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("root")] public BtNode Root { get; set; } = new BtAction();

    public static BehaviorTreeDefinition Load(string path)
        => JsonSerializer.Deserialize<BehaviorTreeDefinition>(File.ReadAllText(path), JsonOpts)!;

    public void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(this, JsonOpts));
    }

    internal static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new BtNodeConverter() }
    };
}

/// <summary>BT node base class. Derived type determined by 'type' field.</summary>
[JsonConverter(typeof(BtNodeConverter))]
public abstract class BtNode
{
    [JsonPropertyName("type")] public abstract string Type { get; }
    [JsonPropertyName("condition")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Condition { get; set; }
    [JsonPropertyName("weight")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public float Weight { get; set; } = 1.0f;
}

public class BtAction : BtNode
{
    public override string Type => "action";
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("overrides")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, object>? Overrides { get; set; }
}

public class BtSequence : BtNode
{
    public override string Type => "sequence";
    [JsonPropertyName("children")] public List<BtNode> Children { get; set; } = new();
}

public class BtSelector : BtNode
{
    public override string Type => "selector";
    [JsonPropertyName("children")] public List<BtNode> Children { get; set; } = new();
}

public class BtRepeat : BtNode
{
    public override string Type => "repeat";
    [JsonPropertyName("count")] public int Count { get; set; } = 1;
    [JsonPropertyName("child")] public BtNode Child { get; set; } = new BtAction();
}

/// <summary>JSON converter that determines BtNode derived type by 'type' field.</summary>
public class BtNodeConverter : JsonConverter<BtNode>
{
    public override BtNode Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        var root = doc.RootElement;
        var type = root.GetProperty("type").GetString();

        // Remove this converter to avoid recursion, use plain deserialization
        var opts = new JsonSerializerOptions(options);
        opts.Converters.Clear();

        return type switch
        {
            "action" => JsonSerializer.Deserialize<BtAction>(root.GetRawText(), opts)!,
            "sequence" => DeserializeComposite<BtSequence>(root, opts),
            "selector" => DeserializeComposite<BtSelector>(root, opts),
            "repeat" => DeserializeRepeat(root, opts),
            _ => throw new JsonException($"Unknown BT node type: {type}")
        };
    }

    public override void Write(Utf8JsonWriter writer, BtNode value, JsonSerializerOptions options)
    {
        var opts = new JsonSerializerOptions(options);
        opts.Converters.Clear();
        JsonSerializer.Serialize(writer, value, value.GetType(), opts);
    }

    private T DeserializeComposite<T>(JsonElement el, JsonSerializerOptions opts) where T : BtNode, new()
    {
        var node = JsonSerializer.Deserialize<T>(el.GetRawText(), opts)!;
        if (el.TryGetProperty("children", out var children))
        {
            var list = new List<BtNode>();
            foreach (var child in children.EnumerateArray())
                list.Add(Read(child, opts));
            if (node is BtSequence seq) seq.Children = list;
            else if (node is BtSelector sel) sel.Children = list;
        }
        return node;
    }

    private BtRepeat DeserializeRepeat(JsonElement el, JsonSerializerOptions opts)
    {
        var node = JsonSerializer.Deserialize<BtRepeat>(el.GetRawText(), opts)!;
        if (el.TryGetProperty("child", out var child))
            node.Child = Read(child, opts);
        return node;
    }

    private BtNode Read(JsonElement el, JsonSerializerOptions opts)
    {
        var type = el.GetProperty("type").GetString();
        return type switch
        {
            "action" => JsonSerializer.Deserialize<BtAction>(el.GetRawText(), opts)!,
            "sequence" => DeserializeComposite<BtSequence>(el, opts),
            "selector" => DeserializeComposite<BtSelector>(el, opts),
            "repeat" => DeserializeRepeat(el, opts),
            _ => throw new JsonException($"Unknown BT node type: {type}")
        };
    }
}
