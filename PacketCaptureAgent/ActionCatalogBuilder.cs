using System.Text.Json;
using System.Text.Json.Serialization;

namespace PacketCaptureAgent;

public class ActionCatalog
{
    [JsonPropertyName("protocol")] public string Protocol { get; set; } = "";
    [JsonPropertyName("last_updated")] public string LastUpdated { get; set; } = "";
    [JsonPropertyName("actions")] public List<CatalogAction> Actions { get; set; } = new();
}

public class CatalogAction
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("phase")] public string? Phase { get; set; }
    [JsonPropertyName("packets")] public List<ActionPacket> Packets { get; set; } = new();
    [JsonPropertyName("dynamic_fields")] public List<ActionDynamicField> DynamicFields { get; set; } = new();
    [JsonPropertyName("outputs")] public List<string> Outputs { get; set; } = new();
    [JsonPropertyName("dependencies")] public List<string> Dependencies { get; set; } = new();
    [JsonPropertyName("repeat_count")] public int RepeatCount { get; set; } = 1;
    [JsonPropertyName("user_input_fields")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? UserInputFields { get; set; }
    [JsonPropertyName("source_log")] public string SourceLog { get; set; } = "";
    [JsonPropertyName("last_observed")] public string LastObserved { get; set; } = "";
    /// <summary>같은 액션 반복 시 필드값이 다른 경우, 필드별 관측된 값 목록. 실행 시 랜덤 선택.</summary>
    [JsonPropertyName("field_variants")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, List<JsonElement>>? FieldVariants { get; set; }
}

public class ActionPacket
{
    [JsonPropertyName("direction")] public string Direction { get; set; } = "";
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("role")] public string Role { get; set; } = "";
    [JsonPropertyName("fields")] public Dictionary<string, object>? Fields { get; set; }
}

public class ActionDynamicField
{
    [JsonPropertyName("packet")] public string Packet { get; set; } = "";
    [JsonPropertyName("field")] public string Field { get; set; } = "";
    [JsonPropertyName("source")] public string Source { get; set; } = "";
}

public class ActionCatalogBuilder
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>분석 결과로부터 Action 목록 생성.</summary>
    public List<CatalogAction> BuildActions(
        List<ReplayPacket> packets,
        List<ClassifiedPacket> classified,
        List<DynamicField> dynamicFields,
        ProtocolDefinition protocol,
        string sourceLog)
    {
        var analyzer = new SequenceAnalyzer();
        var groups = analyzer.BuildRequestGroups(packets);
        var phased = analyzer.AssignPhases(analyzer.GroupPackets(classified), protocol);

        // SEND 패킷명 → Phase 매핑 (AssignPhases 결과에서 추출)
        var sendPhase = new Dictionary<string, string?>();
        foreach (var g in phased)
            if (g.Direction == "SEND" && !sendPhase.ContainsKey(g.Name))
                sendPhase[g.Name] = g.Phase;

        // RECV 패킷명 → 소속 Action ID (첫 등장 기준)
        var recvToAction = new Dictionary<string, string>();

        // DynamicField의 SourcePacket.SourceField → 출력으로 등록할 키
        var outputKeys = new HashSet<string>(
            dynamicFields.Select(df => $"{df.SourcePacket}.{df.SourceField}"));

        // Role 매핑: 패킷명+방향 → Role
        var roleMap = new Dictionary<string, PacketRole>();
        foreach (var c in classified)
        {
            var key = $"{c.Packet.Direction}:{c.Packet.Name}";
            if (!roleMap.ContainsKey(key)) roleMap[key] = c.Role;
        }

        var actions = new List<CatalogAction>();
        var timestamp = packets.FirstOrDefault()?.Timestamp.ToString(@"hh\:mm\:ss\.fff") ?? "";

        foreach (var group in groups)
        {
            if (group.Send == null) continue;

            var actionId = DeriveId(group.Send.Name);
            var existing = actions.FindIndex(a => a.Id == actionId);

            if (existing >= 0)
            {
                // 동일 Action 반복 → repeat_count 증가 + 필드 변화 추적
                actions[existing].RepeatCount++;
                CollectFieldVariants(actions[existing], group.Send.Fields);
                continue;
            }

            // 패킷 목록 구성
            var actionPackets = new List<ActionPacket>();
            actionPackets.Add(new ActionPacket
            {
                Direction = "SEND",
                Name = group.Send.Name,
                Role = roleMap.GetValueOrDefault($"SEND:{group.Send.Name}").ToString()!,
                Fields = group.Send.Fields.Count > 0 ? new Dictionary<string, object>(group.Send.Fields) : null
            });

            // 연속 동일 RECV 압축
            var recvGroups = GroupConsecutiveRecv(group.Responses);
            foreach (var (recvName, count) in recvGroups)
            {
                var role = roleMap.GetValueOrDefault($"RECV:{recvName}");
                actionPackets.Add(new ActionPacket
                {
                    Direction = "RECV",
                    Name = count > 1 ? $"{recvName} ×{count}" : recvName,
                    Role = role.ToString()!
                });
                if (!recvToAction.ContainsKey(recvName))
                    recvToAction[recvName] = actionId;
            }

            // Dynamic fields for this action's SEND
            var dynFields = dynamicFields
                .Where(df => df.SendPacket == group.Send.Name)
                .Select(df => new ActionDynamicField
                {
                    Packet = df.SendPacket,
                    Field = df.SendField,
                    Source = $"{df.SourcePacket}.{df.SourceField}"
                })
                .ToList();

            // Outputs: 이 Action의 RECV 필드 중 다른 Action에서 참조되는 것
            var outputs = outputKeys
                .Where(k => group.Responses.Any(r => k.StartsWith($"{r.Name}.")))
                .ToList();

            actions.Add(new CatalogAction
            {
                Id = actionId,
                Name = DeriveName(group.Send.Name),
                Phase = sendPhase.GetValueOrDefault(group.Send.Name),
                Packets = actionPackets,
                DynamicFields = dynFields,
                Outputs = outputs,
                RepeatCount = 1,
                UserInputFields = DetectUserInputFields(group.Send.Name, dynFields, protocol),
                SourceLog = Path.GetFileName(sourceLog),
                LastObserved = timestamp
            });
        }

        // Dependencies 계산 (모든 Action 확정 후)
        foreach (var action in actions)
        {
            var deps = new HashSet<string>();
            foreach (var df in action.DynamicFields)
            {
                var srcPacket = df.Source.Split('.')[0];
                if (recvToAction.TryGetValue(srcPacket, out var depAction) && depAction != action.Id)
                    deps.Add(depAction);
            }
            action.Dependencies = deps.ToList();
        }

        return actions;
    }

    /// <summary>기존 카탈로그와 merge. 새 Action은 추가/갱신, 기존만 있는 건 유지, 프로토콜에서 삭제된 건 제거.</summary>
    public ActionCatalog Merge(ActionCatalog? existing, List<CatalogAction> newActions, ProtocolDefinition protocol)
    {
        var validSendNames = new HashSet<string>(
            protocol.Packets.Where(p => p.Name.StartsWith("CS_")).Select(p => p.Name));

        var merged = new Dictionary<string, CatalogAction>();

        // 기존 Action 유지 (프로토콜에 아직 있는 것만)
        if (existing != null)
            foreach (var a in existing.Actions)
                if (validSendNames.Contains($"CS_{a.Id.ToUpper()}") || validSendNames.Any(n => DeriveId(n) == a.Id))
                    merged[a.Id] = a;

        // 새 Action으로 추가/갱신
        foreach (var a in newActions)
        {
            if (merged.TryGetValue(a.Id, out var ex))
            {
                // field_variants 합치기 (기존 + 새 값)
                if (a.FieldVariants != null)
                {
                    ex.FieldVariants ??= new();
                    foreach (var (key, newList) in a.FieldVariants)
                    {
                        if (!ex.FieldVariants.ContainsKey(key))
                            ex.FieldVariants[key] = new();
                        var exList = ex.FieldVariants[key];
                        foreach (var elem in newList)
                            if (!exList.Any(e => e.GetRawText() == elem.GetRawText()))
                                exList.Add(elem);
                    }
                }
                ex.RepeatCount = Math.Max(ex.RepeatCount, a.RepeatCount);
                ex.LastObserved = a.LastObserved;
                ex.SourceLog = a.SourceLog;
            }
            else
                merged[a.Id] = a;
        }

        return new ActionCatalog
        {
            Protocol = protocol.Protocol.Name,
            LastUpdated = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss"),
            Actions = merged.Values.ToList()
        };
    }

    public static ActionCatalog? LoadCatalog(string path)
    {
        if (!File.Exists(path)) return null;
        return JsonSerializer.Deserialize<ActionCatalog>(File.ReadAllText(path), JsonOpts);
    }

    public static void SaveCatalog(string path, ActionCatalog catalog)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(catalog, JsonOpts));
    }

    static string DeriveId(string sendName) =>
        sendName.StartsWith("CS_") ? sendName[3..].ToLower() : sendName.ToLower();

    /// <summary>SEND 패킷의 string 필드 중 dynamic_fields가 아닌 것 = 사용자 입력 필드.</summary>
    static List<string>? DetectUserInputFields(string sendName, List<ActionDynamicField> dynFields, ProtocolDefinition protocol)
    {
        var pktDef = protocol.Packets.FirstOrDefault(p => p.Name == sendName);
        if (pktDef == null) return null;

        var dynamicFieldNames = new HashSet<string>(dynFields.Select(df => df.Field));
        var fields = pktDef.Fields
            .Where(f => f.Type == "string" && !dynamicFieldNames.Contains(f.Name))
            .Select(f => f.Name)
            .ToList();

        return fields.Count > 0 ? fields : null;
    }

    static string DeriveName(string sendName)
    {
        var raw = sendName.StartsWith("CS_") ? sendName[3..] : sendName;
        return string.Join("", raw.Split('_').Select(s =>
            s.Length > 0 ? char.ToUpper(s[0]) + s[1..].ToLower() : ""));
    }

    static List<(string Name, int Count)> GroupConsecutiveRecv(List<ReplayPacket> responses)
    {
        var result = new List<(string, int)>();
        for (int i = 0; i < responses.Count; i++)
        {
            int count = 1;
            while (i + 1 < responses.Count && responses[i + 1].Name == responses[i].Name)
            { count++; i++; }
            result.Add((responses[i].Name, count));
        }
        return result;
    }

    /// <summary>같은 액션 반복 시 SEND 필드값이 다르면 variants에 누적.</summary>
    static void CollectFieldVariants(CatalogAction action, Dictionary<string, object> newFields)
    {
        var sendPkt = action.Packets.FirstOrDefault(p => p.Direction == "SEND");
        if (sendPkt?.Fields == null || newFields.Count == 0) return;

        foreach (var (key, newVal) in newFields)
        {
            if (!sendPkt.Fields.TryGetValue(key, out var origVal)) continue;

            var newStr = newVal is JsonElement nje ? nje.GetRawText() : newVal?.ToString() ?? "";
            var origStr = origVal is JsonElement oje ? oje.GetRawText() : origVal?.ToString() ?? "";
            if (newStr == origStr) continue;

            // 값이 다름 → variants 초기화 (첫 변화 감지 시 원본값도 포함)
            action.FieldVariants ??= new();
            if (!action.FieldVariants.ContainsKey(key))
            {
                var origElem = origVal is JsonElement oe ? oe :
                    JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(origVal));
                action.FieldVariants[key] = new List<JsonElement> { origElem };
            }

            var newElem = newVal is JsonElement ne ? ne :
                JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(newVal));

            // 중복 방지
            var list = action.FieldVariants[key];
            if (!list.Any(e => e.GetRawText() == newElem.GetRawText()))
                list.Add(newElem);
        }
    }
}
