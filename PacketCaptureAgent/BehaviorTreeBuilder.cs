using System.Text.Json;

namespace PacketCaptureAgent;

/// <summary>녹화 데이터로부터 Behavior Tree를 자동 구축.
/// 알고리즘: 녹화 시퀀스 → Trie → BT 변환 + 분기 조건 자동 감지.</summary>
public class BehaviorTreeBuilder
{
    private HashSet<string> _dynamicFields = new();
    private Dictionary<string, HashSet<string>> _fieldValuesPerRecording = new();

    /// <summary>녹화 저장소에서 BT 자동 생성.</summary>
    public BehaviorTreeDefinition Build(RecordingStore store, string name = "auto_generated", ActionCatalog? catalog = null, ProtocolDefinition? protocol = null)
    {
        _dynamicFields = AnalyzeFieldDynamics(store.Recordings);
        _fieldValuesPerRecording = CollectFieldValuesPerRecording(store.Recordings);
        var trie = BuildTrie(store.Recordings);
        var root = ConvertToNode(trie);
        if (catalog != null) AnnotateStateBindings(root, catalog);
        var tree = new BehaviorTreeDefinition { Name = name, Root = root };
        if (catalog != null) AnnotateWeightsAndConditions(tree, store.Recordings, catalog, protocol?.Semantics);
        return tree;
    }

    /// <summary>녹화 내에서 값이 변하는 필드(동적) vs 불변(정적) 분류.
    /// 정적 필드(accountUid 등)는 세션 고유값이므로 조건 후보에서 제외.</summary>
    internal static HashSet<string> AnalyzeFieldDynamics(List<Recording> recordings)
    {
        var dynamic = new HashSet<string>();
        foreach (var rec in recordings)
        {
            var firstValues = new Dictionary<string, string>();
            foreach (var step in rec.Sequence)
                foreach (var (key, value) in step.RecvState)
                {
                    var sv = value is JsonElement je ? ConvertJson(je).ToString()! : value?.ToString() ?? "";
                    if (!firstValues.TryGetValue(key, out var first))
                        firstValues[key] = sv;
                    else if (first != sv)
                        dynamic.Add(key);
                }
        }
        return dynamic;
    }

    /// <summary>녹화별 대표값 수집. 각 녹화에서 필드의 최빈값(= 세션 대표값)을 추출.
    /// 고유값 비율이 높으면 세션 식별자로 판단.</summary>
    private static Dictionary<string, HashSet<string>> CollectFieldValuesPerRecording(List<Recording> recordings)
    {
        var result = new Dictionary<string, HashSet<string>>();
        foreach (var rec in recordings)
        {
            // 각 필드의 최빈값 = 세션 대표값 (브로드캐스트 노이즈 제거)
            var counts = new Dictionary<string, Dictionary<string, int>>();
            foreach (var step in rec.Sequence)
                foreach (var (key, value) in step.RecvState)
                {
                    var sv = value is JsonElement je ? ConvertJson(je).ToString()! : value?.ToString() ?? "";
                    if (!counts.ContainsKey(key)) counts[key] = new();
                    counts[key].TryGetValue(sv, out var c);
                    counts[key][sv] = c + 1;
                }
            foreach (var (key, valCounts) in counts)
            {
                var modeValue = valCounts.OrderByDescending(kv => kv.Value).First().Key;
                if (!result.ContainsKey(key)) result[key] = new();
                result[key].Add(modeValue);
            }
        }
        return result;
    }

    #region Trie

    private class TrieNode
    {
        public string? Action;
        public Dictionary<string, TrieNode> Children = new();
        public List<int> RecordingIndices = new(); // 이 노드를 지나는 녹화 인덱스
        public List<Dictionary<string, object>> RecvStates = new(); // 분기 직전 recv_state
    }

    private TrieNode BuildTrie(List<Recording> recordings)
    {
        var root = new TrieNode();
        for (int ri = 0; ri < recordings.Count; ri++)
        {
            var node = root;
            foreach (var step in recordings[ri].Sequence)
            {
                if (!node.Children.TryGetValue(step.Action, out var child))
                {
                    child = new TrieNode { Action = step.Action };
                    node.Children[step.Action] = child;
                }
                child.RecordingIndices.Add(ri);
                child.RecvStates.Add(step.RecvState);
                node = child;
            }
        }
        return root;
    }

    #endregion

    #region Trie → BT 변환

    private BtNode ConvertToNode(TrieNode trie)
    {
        var children = trie.Children.Values.ToList();
        if (children.Count == 0) return new BtSequence();
        if (children.Count == 1) return BuildChain(children[0]);

        // 분기점 → Selector
        var selector = new BtSelector();
        var parentRecvStates = CollectParentStates(trie);
        foreach (var child in children)
        {
            var branch = BuildChain(child);
            branch.Condition = DetectCondition(child, parentRecvStates, trie.Children);
            selector.Children.Add(branch);
        }
        return selector;
    }

    private BtNode BuildChain(TrieNode node)
    {
        var sequence = new List<BtNode>();
        var current = node;

        while (current != null)
        {
            // 연속 동일 액션 → Repeat
            int repeatCount = CountConsecutive(current);
            if (repeatCount > 1)
            {
                sequence.Add(new BtRepeat { Count = repeatCount, Child = new BtAction { Id = current.Action! } });
                current = SkipConsecutive(current, repeatCount);
            }
            else
            {
                sequence.Add(new BtAction { Id = current.Action! });
                var children = current.Children.Values.ToList();
                if (children.Count == 0) break;
                if (children.Count == 1) { current = children[0]; continue; }

                // 분기 → 재귀
                sequence.Add(ConvertToNode(current));
                break;
            }
        }

        if (sequence.Count == 1) return sequence[0];
        return new BtSequence { Children = sequence };
    }

    private int CountConsecutive(TrieNode node)
    {
        int count = 1;
        var cur = node;
        while (cur.Children.Count == 1)
        {
            var child = cur.Children.Values.First();
            if (child.Action != node.Action) break;
            count++;
            cur = child;
        }
        return count;
    }

    private TrieNode? SkipConsecutive(TrieNode node, int count)
    {
        var cur = node;
        for (int i = 1; i < count; i++)
        {
            if (cur.Children.Count != 1) return cur;
            cur = cur.Children.Values.First();
        }
        return cur.Children.Count == 1 ? cur.Children.Values.First() :
               cur.Children.Count > 1 ? cur : null;
    }

    #endregion

    #region 조건 자동 감지

    private Dictionary<int, Dictionary<string, object>> CollectParentStates(TrieNode parent)
    {
        // 각 녹화의 분기 직전 recv_state 수집 (자식 노드들의 state에서)
        var result = new Dictionary<int, Dictionary<string, object>>();
        foreach (var child in parent.Children.Values)
            for (int i = 0; i < child.RecordingIndices.Count; i++)
                result[child.RecordingIndices[i]] = child.RecvStates[i];
        return result;
    }

    private string? DetectCondition(TrieNode branch, Dictionary<int, Dictionary<string, object>> allStates, Dictionary<string, TrieNode> siblings)
    {
        if (branch.RecvStates.Count == 0) return null;

        var branchIndices = new HashSet<int>(branch.RecordingIndices);
        var otherIndices = allStates.Keys.Where(k => !branchIndices.Contains(k)).ToList();
        if (otherIndices.Count == 0) return null;

        var branchState = branch.RecvStates[0];
        var otherState = allStates[otherIndices[0]];

        // 값이 다른 키 찾기 — 동적 필드만 후보로 사용 (정적 필드=세션 고유값 제외)
        foreach (var key in branchState.Keys.Intersect(otherState.Keys))
        {
            if (!_dynamicFields.Contains(key)) continue; // 정적 필드 스킵

            // 녹화 간 고유값 비율 체크: 녹화마다 다른 값이면 세션 식별자 → 스킵
            if (_fieldValuesPerRecording.TryGetValue(key, out var perRec) && perRec.Count >= allStates.Count)
                continue;

            var bv = branchState[key];
            var ov = otherState[key];
            if (bv is JsonElement bje) bv = ConvertJson(bje);
            if (ov is JsonElement oje) ov = ConvertJson(oje);

            if (bv is int bi && ov is int oi && bi != oi)
                return $"{key} == {bi}";
            if (bv is long bl && ov is long ol && bl != ol)
                return $"{key} == {bl}";
        }

        return null;
    }

    private static object ConvertJson(JsonElement je) => je.ValueKind switch
    {
        JsonValueKind.Number => je.TryGetInt32(out var i) ? i : je.GetInt64(),
        JsonValueKind.String => je.GetString()!,
        _ => je.ToString()
    };

    #endregion

    #region 상태 바인딩

    /// <summary>BT 트리를 순회하며 BtAction에 state_random 바인딩 추가.
    /// dynamic_fields의 배열 소스 패턴을 감지하여 인덱스/값 필드 구분.</summary>
    private static void AnnotateStateBindings(BtNode node, ActionCatalog catalog)
    {
        switch (node)
        {
            case BtAction action:
                var catAction = catalog.Actions.FirstOrDefault(a => a.Id == action.Id);
                if (catAction == null) break;
                foreach (var df in catAction.DynamicFields)
                {
                    var m = System.Text.RegularExpressions.Regex.Match(df.Source, @"^(.+)\[(\d+)\]\.(.+)$");
                    if (!m.Success) continue;
                    var arrayKey = m.Groups[1].Value;
                    var idx = m.Groups[2].Value;
                    var fieldName = m.Groups[3].Value;

                    // SEND 필드값 가져오기
                    var sendPkt = catAction.Packets.FirstOrDefault(p => p.Direction == "SEND");
                    if (sendPkt?.Fields == null || !sendPkt.Fields.TryGetValue(df.Field, out var sendValObj)) continue;
                    var sendVal = sendValObj is JsonElement je ? ConvertJson(je).ToString() : sendValObj?.ToString();

                    string binding;
                    if (sendVal == idx)
                        binding = $"{{state_random:{arrayKey}}}";         // 인덱스 필드 (slot)
                    else
                        binding = $"{{state_random:{arrayKey}.{fieldName}}}"; // 값 필드 (itemId)

                    action.Overrides ??= new();
                    action.Overrides[df.Field] = binding;
                }
                break;
            case BtSequence s:
                foreach (var c in s.Children) AnnotateStateBindings(c, catalog);
                break;
            case BtSelector s:
                foreach (var c in s.Children) AnnotateStateBindings(c, catalog);
                break;
            case BtRepeat r:
                AnnotateStateBindings(r.Child, catalog);
                break;
        }
    }

    #endregion

    #region Weight + 상호작용 조건

    /// <summary>액션별 녹화 빈도 → weight, 상호작용 액션 → 조건 자동 부여.
    /// semantics가 있으면 설정 기반, 없으면 조건 부여 스킵.</summary>
    private static void AnnotateWeightsAndConditions(BehaviorTreeDefinition tree, List<Recording> recordings, ActionCatalog catalog, SemanticsDefinition? semantics)
    {
        // 액션별 등장 녹화 수
        var actionRecCount = new Dictionary<string, int>();
        foreach (var rec in recordings)
            foreach (var actionId in rec.Sequence.Select(s => s.Action).Distinct())
            {
                actionRecCount.TryGetValue(actionId, out var c);
                actionRecCount[actionId] = c + 1;
            }
        int total = recordings.Count;

        // semantics 기반 상호작용 감지
        var interactionConditions = new Dictionary<string, string>(); // actionId → condition
        if (semantics?.InteractionSources != null)
            foreach (var action in catalog.Actions)
                foreach (var src in semantics.InteractionSources)
                    if (action.DynamicFields.Any(df => df.Source.StartsWith(src.SourcePrefix)))
                    { interactionConditions[action.Id] = src.Condition; break; }

        // semantics 기반 상태 조건 감지
        var stateConditions = new Dictionary<string, string>(); // actionId → condition
        if (semantics?.StateConditions != null)
            foreach (var sc in semantics.StateConditions)
                foreach (var action in catalog.Actions)
                {
                    if (!action.Id.Contains(sc.ActionPattern)) continue;
                    bool always = true, found = false;
                    foreach (var rec in recordings)
                        foreach (var step in rec.Sequence.Where(s => s.Action == action.Id))
                        {
                            found = true;
                            if (!step.RecvState.TryGetValue(sc.StateField, out var mv))
                                always = false;
                            else
                            {
                                var val = mv is JsonElement je ? (je.TryGetInt32(out var jv) ? jv : 0) : Convert.ToInt32(mv);
                                if (val < sc.MinValue) always = false;
                            }
                        }
                    if (found && always) stateConditions[action.Id] = $"{sc.StateField} >= {sc.MinValue}";
                }

        ApplyAnnotations(tree.Root, total, actionRecCount, interactionConditions, stateConditions);
    }

    private static void ApplyAnnotations(BtNode node, int total, Dictionary<string, int> actionRecCount,
        Dictionary<string, string> interactionConditions, Dictionary<string, string> stateConditions)
    {
        if (node is BtAction action)
        {
            // Weight
            if (actionRecCount.TryGetValue(action.Id, out var count))
            {
                float w = (float)count / total;
                if (w < 1.0f) action.Weight = MathF.Round(w, 2);
            }

            // 조건 부여 (interaction 우선, state 차선)
            if (action.Condition == null && interactionConditions.TryGetValue(action.Id, out var ic))
                action.Condition = ic;
            else if (action.Condition == null && stateConditions.TryGetValue(action.Id, out var sc))
                action.Condition = sc;
        }

        switch (node)
        {
            case BtSequence s: foreach (var c in s.Children) ApplyAnnotations(c, total, actionRecCount, interactionConditions, stateConditions); break;
            case BtSelector s: foreach (var c in s.Children) ApplyAnnotations(c, total, actionRecCount, interactionConditions, stateConditions); break;
            case BtRepeat r: ApplyAnnotations(r.Child, total, actionRecCount, interactionConditions, stateConditions); break;
        }
    }

    #endregion
}
