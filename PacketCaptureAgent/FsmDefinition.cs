using System.Text.Json;
using System.Text.Json.Serialization;

namespace PacketCaptureAgent;

/// <summary>FSM 전이 확률 모델. 녹화에서 추출한 액션 간 전이 확률로 랜덤 행동 생성.</summary>
public class FsmDefinition
{
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("initial_state")] public string InitialState { get; set; } = "";
    [JsonPropertyName("transitions")] public Dictionary<string, Dictionary<string, float>> Transitions { get; set; } = new();

    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    public static FsmDefinition Load(string path)
        => JsonSerializer.Deserialize<FsmDefinition>(File.ReadAllText(path), JsonOpts)!;

    public void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(this, JsonOpts));
    }
}
