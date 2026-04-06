using System.Text.Json;
using System.Text.Json.Serialization;

namespace PacketCaptureAgent;

// ── Scenario JSON 모델 ──

public class ScenarioDefinition
{
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("protocol")] public string Protocol { get; set; } = "";
    [JsonPropertyName("steps")] public List<ScenarioStep> Steps { get; set; } = new();
}

public class ScenarioStep
{
    [JsonPropertyName("action")] public string Action { get; set; } = "";
    [JsonPropertyName("repeat")] public int Repeat { get; set; } = 1;
    [JsonPropertyName("overrides")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, object>? Overrides { get; set; }
}

// ── ScenarioBuilder: 시나리오 + 카탈로그 → List<ReplayPacket> ──

public class ScenarioBuilder
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>시나리오의 모든 action이 카탈로그에 존재하고 의존성이 충족되는지 검증.</summary>
    public List<string> Validate(ScenarioDefinition scenario, ActionCatalog catalog)
    {
        var errors = new List<string>();
        var catalogIds = new HashSet<string>(catalog.Actions.Select(a => a.Id));
        var includedIds = new HashSet<string>(scenario.Steps.Select(s => s.Action));

        foreach (var step in scenario.Steps)
        {
            if (!catalogIds.Contains(step.Action))
            {
                errors.Add($"Action '{step.Action}'이(가) 카탈로그에 없습니다.");
                continue;
            }

            var action = catalog.Actions.First(a => a.Id == step.Action);
            foreach (var dep in action.Dependencies)
                if (!includedIds.Contains(dep))
                    errors.Add($"Action '{step.Action}'은(는) '{dep}'에 의존하지만 시나리오에 포함되지 않았습니다.");
        }

        return errors;
    }

    /// <summary>시나리오 + 카탈로그 → List&lt;ReplayPacket&gt; 조립. SEND에 템플릿 필드, RECV는 placeholder.</summary>
    public List<ReplayPacket> Build(ScenarioDefinition scenario, ActionCatalog catalog)
    {
        var packets = new List<ReplayPacket>();
        var ts = TimeSpan.Zero;
        var step100ms = TimeSpan.FromMilliseconds(100);
        // {random:N} 패턴은 시나리오 실행 단위로 1회 생성하여 재사용
        var randomCache = new Dictionary<string, string>();

        foreach (var step in scenario.Steps)
        {
            var action = catalog.Actions.FirstOrDefault(a => a.Id == step.Action);
            if (action == null) continue;

            for (int r = 0; r < step.Repeat; r++)
            {
                foreach (var ap in action.Packets)
                {
                    // ×N 표기 제거 (예: "SC_NPC_SPAWN ×5" → "SC_NPC_SPAWN", count=5)
                    var (name, count) = ParsePacketName(ap.Name);

                    var fields = new Dictionary<string, object>();
                    if (ap.Direction == "SEND" && ap.Fields != null)
                    {
                        foreach (var kv in ap.Fields)
                            fields[kv.Key] = kv.Value is JsonElement je ? ConvertJsonElement(je) : kv.Value;
                        // step-level overrides 적용
                        if (step.Overrides != null)
                            foreach (var kv in step.Overrides)
                                fields[kv.Key] = ResolveValue(kv.Value is JsonElement je2 ? ConvertJsonElement(je2) : kv.Value, randomCache);
                    }

                    for (int c = 0; c < count; c++)
                    {
                        packets.Add(new ReplayPacket(name, ap.Direction, new(fields), ts));
                        ts += step100ms;
                    }
                }
            }
        }

        return packets;
    }

    /// <summary>{random:N} 패턴 해석. 동일 패턴은 같은 실행 내에서 동일 값 반환.</summary>
    internal static object ResolveValue(object value, Dictionary<string, string> cache)
    {
        if (value is not string s) return value;
        var m = System.Text.RegularExpressions.Regex.Match(s, @"^\{random:(\d+)\}$");
        if (!m.Success) return value;
        if (cache.TryGetValue(s, out var cached)) return cached;
        int len = int.Parse(m.Groups[1].Value);
        const string chars = "abcdefghijklmnopqrstuvwxyz0123456789";
        var result = new string(Enumerable.Range(0, len).Select(_ => chars[Random.Shared.Next(chars.Length)]).ToArray());
        cache[s] = result;
        return result;
    }

    /// <summary>시나리오에 포함된 모든 dynamic field 매핑 수집.</summary>
    public List<ActionDynamicField> CollectDynamicFields(ScenarioDefinition scenario, ActionCatalog catalog)
    {
        var result = new List<ActionDynamicField>();
        var seen = new HashSet<string>();

        foreach (var step in scenario.Steps)
        {
            var action = catalog.Actions.FirstOrDefault(a => a.Id == step.Action);
            if (action == null) continue;
            foreach (var df in action.DynamicFields)
            {
                var key = $"{df.Packet}.{df.Field}";
                if (seen.Add(key)) result.Add(df);
            }
        }

        return result;
    }

    /// <summary>카탈로그 전체에서 dynamic fields 수집 (BT용).</summary>
    public List<ActionDynamicField> CollectAllDynamicFields(ActionCatalog catalog)
    {
        var result = new List<ActionDynamicField>();
        var seen = new HashSet<string>();
        foreach (var action in catalog.Actions)
            foreach (var df in action.DynamicFields)
                if (seen.Add($"{df.Packet}.{df.Field}")) result.Add(df);
        return result;
    }

    // ── I/O ──

    public static ScenarioDefinition Load(string path)
        => JsonSerializer.Deserialize<ScenarioDefinition>(File.ReadAllText(path), JsonOpts)!;

    public static void Save(string path, ScenarioDefinition scenario)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
        File.WriteAllText(path, JsonSerializer.Serialize(scenario, JsonOpts));
    }

    // ── 인터랙티브 빌더 ──

    public static ScenarioDefinition BuildInteractive(ActionCatalog catalog)
    {
        Console.WriteLine($"\n=== 시나리오 빌더 ({catalog.Protocol}) ===\n");
        Console.WriteLine("사용 가능한 Action:");
        foreach (var a in catalog.Actions)
        {
            var deps = a.Dependencies.Count > 0 ? $" (의존: {string.Join(", ", a.Dependencies)})" : "";
            Console.WriteLine($"  {a.Id,-20} {a.Phase ?? "",-15} {a.Name}{deps}");
        }

        Console.Write("\n사용할 Action (쉼표 구분): ");
        var input = Console.ReadLine()?.Trim() ?? "";
        if (string.IsNullOrEmpty(input)) return new();

        var steps = new List<ScenarioStep>();
        foreach (var token in input.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            var action = catalog.Actions.FirstOrDefault(a =>
                a.Id.Equals(token, StringComparison.OrdinalIgnoreCase));
            if (action == null)
            {
                Console.WriteLine($"  ⚠ '{token}' 없음, 건너뜀");
                continue;
            }

            int repeat = 1;
            if (action.RepeatCount > 1)
            {
                Console.Write($"  {action.Id} 반복 횟수 (기본 {action.RepeatCount}): ");
                var rInput = Console.ReadLine()?.Trim();
                if (!string.IsNullOrEmpty(rInput) && int.TryParse(rInput, out var r) && r > 0)
                    repeat = r;
                else
                    repeat = action.RepeatCount;
            }

            // user_input_fields 프롬프트
            Dictionary<string, object>? overrides = null;
            if (action.UserInputFields is { Count: > 0 })
            {
                var sendPkt = action.Packets.FirstOrDefault(p => p.Direction == "SEND");
                foreach (var field in action.UserInputFields)
                {
                    var def = sendPkt?.Fields != null && sendPkt.Fields.TryGetValue(field, out var v) ? v?.ToString() ?? "" : "";
                    Console.Write($"  {field} [{def}]: ");
                    var val = Console.ReadLine()?.Trim();
                    if (!string.IsNullOrEmpty(val))
                    {
                        overrides ??= new();
                        overrides[field] = val;
                    }
                }
            }

            steps.Add(new ScenarioStep { Action = action.Id, Repeat = repeat, Overrides = overrides });
        }

        Console.Write("\n시나리오 이름: ");
        var name = Console.ReadLine()?.Trim() ?? "Unnamed";

        return new ScenarioDefinition
        {
            Name = name,
            Protocol = catalog.Protocol,
            Steps = steps
        };
    }

    // ── 유틸 ──

    internal static (string Name, int Count) ParsePacketName(string raw)
    {
        var idx = raw.IndexOf(" ×");
        if (idx > 0 && int.TryParse(raw[(idx + 2)..], out var count))
            return (raw[..idx], count);
        return (raw, 1);
    }

    internal static object ConvertJsonElement(JsonElement je) => je.ValueKind switch
    {
        JsonValueKind.Number when je.TryGetInt32(out var i) => i,
        JsonValueKind.Number when je.TryGetInt64(out var l) => l,
        JsonValueKind.Number => je.GetDouble(),
        JsonValueKind.String => je.GetString()!,
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        _ => je.ToString()
    };
}

// ── 동적 필드 자동 주입 인터셉터 (기존 IReplayInterceptor 구현) ──

public class DynamicFieldInterceptor : IReplayInterceptor
{
    private readonly Dictionary<string, List<ActionDynamicField>> _fieldsByPacket;
    private readonly Dictionary<string, object> _sharedState;

    public int Priority => 0;

    public DynamicFieldInterceptor(List<ActionDynamicField> dynamicFields, Dictionary<string, object> sharedState)
    {
        _sharedState = sharedState;
        _fieldsByPacket = dynamicFields
            .GroupBy(df => df.Packet)
            .ToDictionary(g => g.Key, g => g.ToList());
    }

    public bool ShouldIntercept(ReplayPacket packet, GameWorldState world)
        => packet.Direction == "SEND" && _fieldsByPacket.ContainsKey(packet.Name);

    public Task<ReplayPacket> PrepareAsync(ReplaySession session, ReplayPacket original)
    {
        if (!_fieldsByPacket.TryGetValue(original.Name, out var mappings))
            return Task.FromResult(original);

        var fields = new Dictionary<string, object>(original.Fields);
        foreach (var m in mappings)
            if (_sharedState.TryGetValue(m.Source, out var value))
                fields[m.Field] = value;

        return Task.FromResult(original with { Fields = fields });
    }
}

// ── 응답 추적 핸들러 (기존 IResponseHandler 래핑, SessionState → 공유 상태 복사) ──

public class TrackingResponseHandler : IResponseHandler
{
    private readonly IResponseHandler _inner;
    private readonly Dictionary<string, object> _sharedState;

    public TrackingResponseHandler(IResponseHandler inner, Dictionary<string, object> sharedState)
    {
        _inner = inner;
        _sharedState = sharedState;
    }

    public int OnResponse(byte[] data, int length, ReplayContext context)
    {
        int count = _inner.OnResponse(data, length, context);
        foreach (var kv in context.SessionState)
            _sharedState[kv.Key] = kv.Value;
        return count;
    }
}
