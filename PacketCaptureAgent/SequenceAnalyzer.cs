namespace PacketCaptureAgent;

public enum PacketRole { Core, DataSource, Conditional, Noise }

public record ClassifiedPacket(ReplayPacket Packet, PacketRole Role);

public record DynamicField(string SendPacket, string SendField, string SourcePacket, string SourceField);

public record PacketGroup(string Name, string Direction, PacketRole Role, int Count, Dictionary<string, object>? SampleFields, string? Phase = null);

/// <summary>Grouped request-response pair for diagram output.</summary>
public record PairGroup(PacketGroup Send, PacketGroup? Recv, int RepeatCount);

public class SequenceAnalyzer
{
    static readonly HashSet<string> NoisePackets = new(StringComparer.OrdinalIgnoreCase)
    {
        "CS_HEARTBEAT", "SC_HEARTBEAT"
    };

    public List<ClassifiedPacket> Classify(List<ReplayPacket> packets)
    {
        var sendFieldValues = CollectSendValues(packets);
        var requestGroups = BuildRequestGroups(packets);
        var result = new List<ClassifiedPacket>(packets.Count);

        foreach (var group in requestGroups)
        {
            if (group.Send != null)
            {
                var sendRole = NoisePackets.Contains(group.Send.Name) ? PacketRole.Noise : PacketRole.Core;
                result.Add(new(group.Send, sendRole));
            }

            var recvClassified = group.Responses
                .Select(r => new ClassifiedPacket(r, ClassifyRecv(r, group.Send, sendFieldValues)))
                .ToList();

            if (group.Send != null && recvClassified.Count > 0
                && !recvClassified.Any(c => c.Role == PacketRole.Core))
            {
                var first = recvClassified.FindIndex(c => c.Role == PacketRole.Conditional);
                if (first >= 0)
                    recvClassified[first] = recvClassified[first] with { Role = PacketRole.Core };
            }

            result.AddRange(recvClassified);
        }

        return result;
    }

    internal List<RequestGroup> BuildRequestGroups(List<ReplayPacket> packets)
    {
        var groups = new List<RequestGroup>();
        RequestGroup? current = null;

        foreach (var pkt in packets)
        {
            if (pkt.Direction == "SEND")
            {
                current = new RequestGroup { Send = pkt };
                groups.Add(current);
            }
            else
            {
                if (current == null)
                {
                    current = new RequestGroup();
                    groups.Add(current);
                }
                current.Responses.Add(pkt);
            }
        }

        return groups;
    }

    HashSet<string> CollectSendValues(List<ReplayPacket> packets)
    {
        var values = new HashSet<string>();
        foreach (var pkt in packets)
        {
            if (pkt.Direction != "SEND") continue;
            foreach (var field in pkt.Fields)
            {
                var v = NormalizeValue(field.Value);
                if (v != null) values.Add(v);
            }
        }
        return values;
    }

    PacketRole ClassifyRecv(ReplayPacket recv, ReplayPacket? send, HashSet<string> sendFieldValues)
    {
        if (NoisePackets.Contains(recv.Name)) return PacketRole.Noise;
        if (IsNotification(recv.Name)) return PacketRole.Noise;

        if (send != null && IsDirectResponse(send.Name, recv.Name))
            return PacketRole.Core;

        foreach (var field in recv.Fields)
        {
            var v = NormalizeValue(field.Value);
            if (v != null && sendFieldValues.Contains(v))
                return PacketRole.DataSource;
        }

        return PacketRole.Conditional;
    }

    internal static bool IsDirectResponse(string sendName, string recvName)
    {
        if (!sendName.StartsWith("CS_")) return false;
        var baseName = sendName[3..];
        return recvName == $"SC_{baseName}_RESULT"
            || recvName == $"SC_{baseName}_LIST"
            || recvName == $"SC_{baseName}";
    }

    static bool IsNotification(string name) => name is "SC_EXP_UPDATE" or "SC_LEVEL_UP";

    /// <summary>Normalize value for comparison. Skip booleans (0/1) only.</summary>
    static string? NormalizeValue(object value)
    {
        if (value is int i && i >= 0 && i <= 1) return null;
        if (value is string s && string.IsNullOrEmpty(s)) return null;
        return value.ToString();
    }

    /// <summary>Group consecutive same-type packets, then merge repeated SEND-RECV pairs.</summary>
    public List<PacketGroup> GroupPackets(List<ClassifiedPacket> classified)
    {
        // Step 1: group consecutive same-type
        var raw = new List<PacketGroup>();
        for (int i = 0; i < classified.Count; i++)
        {
            var c = classified[i];
            int count = 1;
            while (i + 1 < classified.Count
                && classified[i + 1].Packet.Name == c.Packet.Name
                && classified[i + 1].Packet.Direction == c.Packet.Direction
                && classified[i + 1].Role == c.Role)
            {
                count++;
                i++;
            }
            raw.Add(new(c.Packet.Name, c.Packet.Direction, c.Role, count, c.Packet.Fields));
        }

        // Step 2: merge repeated SEND-RECV pairs (e.g., CS_MOVE, SC_MOVE_RESULT × N)
        var merged = new List<PacketGroup>();
        for (int i = 0; i < raw.Count; i++)
        {
            if (i + 1 < raw.Count && raw[i].Direction == "SEND" && raw[i + 1].Direction == "RECV"
                && raw[i].Count == 1 && raw[i + 1].Count == 1)
            {
                // Count how many times this SEND-RECV pair repeats
                int pairCount = 1;
                int j = i + 2;
                while (j + 1 < raw.Count
                    && raw[j].Name == raw[i].Name && raw[j].Direction == "SEND" && raw[j].Count == 1
                    && raw[j + 1].Name == raw[i + 1].Name && raw[j + 1].Direction == "RECV" && raw[j + 1].Count == 1)
                {
                    pairCount++;
                    j += 2;
                }

                if (pairCount > 1)
                {
                    merged.Add(raw[i] with { Count = pairCount });
                    merged.Add(raw[i + 1] with { Count = pairCount });
                    i = j - 1; // skip merged pairs
                    continue;
                }
            }
            merged.Add(raw[i]);
        }

        return merged;
    }

    public string FormatDiagram(List<PacketGroup> groups)
    {
        var sb = new System.Text.StringBuilder();
        int total = groups.Sum(g => g.Count);
        var stats = new Dictionary<PacketRole, int>();
        foreach (var g in groups)
            stats[g.Role] = stats.GetValueOrDefault(g.Role) + g.Count;

        sb.AppendLine($"=== Sequence Diagram ({total} packets) ===");
        sb.AppendLine($"=== Core: {stats.GetValueOrDefault(PacketRole.Core)}, DataSource: {stats.GetValueOrDefault(PacketRole.DataSource)}, Conditional: {stats.GetValueOrDefault(PacketRole.Conditional)}, Noise: {stats.GetValueOrDefault(PacketRole.Noise)} ===");
        sb.AppendLine();
        sb.AppendLine("Client                                    Server");
        sb.AppendLine("  │                                         │");

        for (int i = 0; i < groups.Count; i++)
        {
            var g = groups[i];
            string tag = g.Role switch
            {
                PacketRole.DataSource => " [DataSrc]",
                PacketRole.Conditional => " [Cond]",
                PacketRole.Noise => " [Noise]",
                _ => ""
            };
            string countStr = g.Count > 1 ? $" ×{g.Count}" : "";
            string label = $"{g.Name}{countStr}";
            string fields = FormatKeyFields(g);

            // Check if this SEND has a paired RECV with same count (merged pair)
            bool isPairedSend = g.Direction == "SEND" && i + 1 < groups.Count
                && groups[i + 1].Direction == "RECV" && groups[i + 1].Count == g.Count && g.Count > 1;

            if (g.Direction == "SEND")
            {
                if (isPairedSend)
                {
                    var recv = groups[i + 1];
                    string recvTag = recv.Role switch
                    {
                        PacketRole.DataSource => " [DataSrc]",
                        PacketRole.Conditional => " [Cond]",
                        PacketRole.Noise => " [Noise]",
                        _ => ""
                    };
                    sb.AppendLine($"  │── {g.Name} ──>  ×{g.Count}            │");
                    sb.AppendLine($"  │<── {recv.Name} ──  ×{recv.Count}       │{recvTag}");
                    i++; // skip the RECV
                }
                else
                {
                    sb.AppendLine($"  │── {label} ──>                         │{tag}");
                    if (fields.Length > 0)
                        sb.AppendLine($"  │     {fields}                          │");
                }
            }
            else
            {
                sb.AppendLine($"  │<── {label} ──                         │{tag}");
                if (fields.Length > 0)
                    sb.AppendLine($"  │     {fields}                          │");
            }
        }

        sb.AppendLine("  │                                         │");
        return sb.ToString();
    }

    static readonly string[] PhaseColors = [
        "rgb(200, 220, 255)", "rgb(200, 255, 220)", "rgb(255, 245, 200)", "rgb(255, 220, 220)",
        "rgb(230, 210, 255)", "rgb(210, 245, 245)"
    ];

    /// <summary>Assign phase to each group based on protocol phases mapping.</summary>
    public List<PacketGroup> AssignPhases(List<PacketGroup> groups, ProtocolDefinition protocol)
    {
        if (protocol.Phases == null || protocol.Phases.Count == 0) return groups;

        // Build category → phase name lookup
        var catToPhase = new Dictionary<int, string>();
        foreach (var (name, cats) in protocol.Phases)
            foreach (var cat in cats) catToPhase[cat] = name;

        // Find packet type by name for category lookup
        var nameToType = protocol.Packets.ToDictionary(p => p.Name, p => p.Type);

        string? currentPhase = null;
        var result = new List<PacketGroup>(groups.Count);
        foreach (var g in groups)
        {
            if (g.Direction == "SEND" && nameToType.TryGetValue(g.Name, out int pktType))
            {
                int cat = pktType >> 8;
                if (catToPhase.TryGetValue(cat, out var phase)) currentPhase = phase;
            }
            result.Add(g with { Phase = currentPhase });
        }
        return result;
    }

    public string FormatMermaid(List<PacketGroup> groups)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("```mermaid");
        sb.AppendLine("sequenceDiagram");
        sb.AppendLine("    participant C as Client");
        sb.AppendLine("    participant S as Server");

        // Collect unique phase names in order for color assignment
        var phaseOrder = new List<string>();
        foreach (var g in groups)
            if (g.Phase != null && !phaseOrder.Contains(g.Phase)) phaseOrder.Add(g.Phase);

        string? currentPhase = null;

        for (int i = 0; i < groups.Count; i++)
        {
            var g = groups[i];

            // Phase rect management
            if (g.Phase != currentPhase)
            {
                if (currentPhase != null) sb.AppendLine("    end");
                if (g.Phase != null)
                {
                    int colorIdx = phaseOrder.IndexOf(g.Phase) % PhaseColors.Length;
                    sb.AppendLine($"    rect {PhaseColors[colorIdx]}");
                    sb.AppendLine($"    Note right of C: {g.Phase}");
                }
                currentPhase = g.Phase;
            }

            string tag = g.Role switch
            {
                PacketRole.DataSource => " [DataSrc]",
                PacketRole.Conditional => " [Cond]",
                PacketRole.Noise => " [Noise]",
                _ => ""
            };

            // Check paired SEND-RECV with same count > 1
            bool isPaired = g.Direction == "SEND" && i + 1 < groups.Count
                && groups[i + 1].Direction == "RECV" && groups[i + 1].Count == g.Count && g.Count > 1;

            if (isPaired)
            {
                var recv = groups[i + 1];
                string recvTag = recv.Role switch
                {
                    PacketRole.DataSource => " [DataSrc]",
                    PacketRole.Conditional => " [Cond]",
                    PacketRole.Noise => " [Noise]",
                    _ => ""
                };
                sb.AppendLine($"    loop ×{g.Count}");
                sb.AppendLine($"        C->>S: {g.Name}");
                sb.AppendLine($"        S->>C: {recv.Name}{recvTag}");
                sb.AppendLine("    end");
                i++;
            }
            else if (g.Role == PacketRole.Noise)
            {
                string dir = g.Direction == "SEND" ? "C,S" : "S,C";
                string countStr = g.Count > 1 ? $" ×{g.Count}" : "";
                sb.AppendLine($"    Note over {dir}: {g.Name}{countStr} [Noise]");
            }
            else if (g.Direction == "SEND")
            {
                string countStr = g.Count > 1 ? $" ×{g.Count}" : "";
                sb.AppendLine($"    C->>S: {g.Name}{countStr}{tag}");
            }
            else
            {
                string countStr = g.Count > 1 ? $" ×{g.Count}" : "";
                sb.AppendLine($"    S->>C: {g.Name}{countStr}{tag}");
            }
        }

        if (currentPhase != null) sb.AppendLine("    end");
        sb.AppendLine("```");
        return sb.ToString();
    }

    static string FormatKeyFields(PacketGroup g)
    {
        if (g.SampleFields == null || g.SampleFields.Count == 0) return "";
        var interesting = g.SampleFields
            .Where(f => f.Key != "raw" && !f.Key.Contains("->"))
            .Where(f => !(f.Value is int i && i >= 0 && i <= 1))
            .Take(3)
            .Select(f => $"{f.Key}={FormatValue(f.Value)}");
        return string.Join(", ", interesting);
    }

    static string FormatValue(object v) => v is string s ? $"\"{s}\"" : v.ToString() ?? "";

    /// <summary>RECV 출력값 → 이후 SEND 입력값 매칭으로 동적 필드 의존성 감지. 수동 매핑 우선, suffix 타입 필터 적용.</summary>
    public List<DynamicField> DetectDynamicFields(List<ReplayPacket> packets, List<FieldMapping>? manualMappings = null)
    {
        // 1. 수동 매핑 → DynamicField 변환 (최우선)
        var manual = new Dictionary<(string, string), DynamicField>();
        if (manualMappings != null)
        {
            foreach (var m in manualMappings)
            {
                var dot = m.Target.IndexOf('.');
                if (dot < 0) continue;
                var sendPkt = m.Target[..dot];
                var sendField = m.Target[(dot + 1)..];

                if (m.Source is "static") continue; // static = 자동 감지 차단, 결과에 포함 안 함

                var srcDot = m.Source.IndexOf('.');
                var srcPkt = srcDot >= 0 ? m.Source[..srcDot] : m.Source;
                var srcField = srcDot >= 0 ? m.Source[(srcDot + 1)..] : "";

                manual[(sendPkt, sendField)] = new DynamicField(sendPkt, sendField, srcPkt, srcField);
            }
        }

        // 2. 수동 매핑에서 static으로 지정된 필드 수집 (자동 감지 차단)
        var suppressed = new HashSet<(string, string)>();
        if (manualMappings != null)
            foreach (var m in manualMappings.Where(m => m.Source == "static"))
            {
                var dot = m.Target.IndexOf('.');
                if (dot >= 0) suppressed.Add((m.Target[..dot], m.Target[(dot + 1)..]));
            }

        // 3. 자동 감지 (suffix 타입 필터, 시간 순서 준수)
        var result = new List<DynamicField>();
        var seen = new HashSet<(string, string)>();

        // 수동 매핑 먼저 추가
        foreach (var df in manual.Values)
        {
            result.Add(df);
            seen.Add((df.SendPacket, df.SendField));
        }

        // 순차 처리: SEND 시점 이전 RECV만 후보로 사용
        var recvCandidates = new Dictionary<string, List<(string packet, string field)>>();
        foreach (var pkt in packets)
        {
            if (pkt.Direction == "RECV")
            {
                foreach (var (name, value) in pkt.Fields)
                {
                    var v = NormalizeValue(value);
                    if (v == null) continue;
                    if (!recvCandidates.ContainsKey(v)) recvCandidates[v] = new();
                    recvCandidates[v].Add((pkt.Name, name));
                }
            }
            else
            {
                foreach (var (name, value) in pkt.Fields)
                {
                    var key = (pkt.Name, name);
                    if (seen.Contains(key) || suppressed.Contains(key)) continue;

                    var v = NormalizeValue(value);
                    if (v == null || !recvCandidates.TryGetValue(v, out var candidates)) continue;

                    var sendType = GetFieldType(name);
                    var match = candidates.LastOrDefault(c => GetFieldType(c.field) == sendType);
                    if (match != default)
                    {
                        result.Add(new DynamicField(pkt.Name, name, match.packet, match.field));
                        seen.Add(key);
                    }
                }
            }
        }

        return result;
    }

    /// <summary>필드명 suffix에서 ID 타입 추론. uid↔uid, id↔id, slot↔slot만 매칭 허용.</summary>
    internal static string GetFieldType(string fieldName)
    {
        // 배열 경로에서 마지막 필드명 추출: "items[16].itemId" → "itemId"
        var baseName = fieldName.Contains('.') ? fieldName.Split('.')[^1] : fieldName;
        baseName = baseName.Contains('[') ? baseName.Split('[')[0] : baseName;

        if (baseName.EndsWith("Uid", StringComparison.OrdinalIgnoreCase)) return "uid";
        if (baseName.EndsWith("Id", StringComparison.OrdinalIgnoreCase)) return "id";
        if (baseName.Equals("slot", StringComparison.OrdinalIgnoreCase)) return "slot";
        return "other";
    }

    internal class RequestGroup
    {
        public ReplayPacket? Send { get; set; }
        public List<ReplayPacket> Responses { get; } = new();
    }
}
