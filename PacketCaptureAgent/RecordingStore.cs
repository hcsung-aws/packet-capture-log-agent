using System.Text.Json;
using System.Text.Json.Serialization;

namespace PacketCaptureAgent;

/// <summary>녹화 저장소. 여러 캡처 세션의 액션 시퀀스 + recv_state를 저장.</summary>
public class RecordingStore
{
    [JsonPropertyName("recordings")] public List<Recording> Recordings { get; set; } = new();

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static RecordingStore Load(string path)
    {
        if (!File.Exists(path)) return new();
        return JsonSerializer.Deserialize<RecordingStore>(File.ReadAllText(path), JsonOpts) ?? new();
    }

    public void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(this, JsonOpts));
    }

    /// <summary>캡처 로그에서 녹화 추출. 기존 분석 결과(패킷 + 분류)를 재사용.</summary>
    public static Recording ExtractFromCapture(
        List<ReplayPacket> packets,
        List<ClassifiedPacket> classified,
        ActionCatalog catalog,
        string sourceLog)
    {
        var analyzer = new SequenceAnalyzer();
        var groups = analyzer.BuildRequestGroups(packets);

        var steps = new List<RecordingStep>();
        var state = new Dictionary<string, object>();

        foreach (var group in groups)
        {
            if (group.Send == null) continue;
            var actionId = catalog.Actions
                .FirstOrDefault(a => a.Packets.Any(p => p.Direction == "SEND" && p.Name == group.Send.Name))?.Id;
            if (actionId == null) continue;

            // RECV 패킷 필드를 flat key로 state에 축적
            foreach (var recv in group.Responses)
                FlattenFields(state, recv.Name, recv.Fields);

            // 현재 state 스냅샷 저장
            steps.Add(new RecordingStep
            {
                Action = actionId,
                RecvState = new Dictionary<string, object>(state)
            });
        }

        return new Recording
        {
            Id = $"rec_{DateTime.Now:yyyyMMdd_HHmmss}",
            Timestamp = DateTime.Now.ToString("o"),
            SourceLog = Path.GetFileName(sourceLog),
            Sequence = steps
        };
    }

    private static void FlattenFields(Dictionary<string, object> state, string prefix, Dictionary<string, object> fields)
    {
        foreach (var (key, value) in fields)
            FieldFlattener.Flatten(state, $"{prefix}.{key}", value);
    }
}

public class Recording
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("timestamp")] public string Timestamp { get; set; } = "";
    [JsonPropertyName("source_log")] public string SourceLog { get; set; } = "";
    [JsonPropertyName("sequence")] public List<RecordingStep> Sequence { get; set; } = new();
}

public class RecordingStep
{
    [JsonPropertyName("action")] public string Action { get; set; } = "";
    [JsonPropertyName("recv_state")] public Dictionary<string, object> RecvState { get; set; } = new();
}
