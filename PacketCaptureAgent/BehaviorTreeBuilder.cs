using System.Text.Json;

namespace PacketCaptureAgent;

/// <summary>녹화 데이터로부터 Behavior Tree를 자동 구축.
/// 알고리즘: 녹화 시퀀스 → Trie → BT 변환 + 분기 조건 자동 감지.</summary>
public class BehaviorTreeBuilder
{
    /// <summary>녹화 저장소에서 BT 자동 생성.</summary>
    public BehaviorTreeDefinition Build(RecordingStore store, string name = "auto_generated")
    {
        var trie = BuildTrie(store.Recordings);
        var root = ConvertToNode(trie);
        return new BehaviorTreeDefinition { Name = name, Root = root };
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

        // 이 분기의 recv_state vs 다른 분기의 recv_state 비교
        var branchState = branch.RecvStates[0];
        var otherState = allStates[otherIndices[0]];

        // 값이 다른 키 찾기 (숫자 비교 가능한 것 우선)
        foreach (var key in branchState.Keys.Intersect(otherState.Keys))
        {
            var bv = branchState[key];
            var ov = otherState[key];
            if (bv is JsonElement bje) bv = ConvertJson(bje);
            if (ov is JsonElement oje) ov = ConvertJson(oje);

            if (bv is int bi && ov is int oi && bi != oi)
                return $"{key} == {bi}";
            if (bv is long bl && ov is long ol && bl != ol)
                return $"{key} == {bl}";
        }

        return null; // 자동 감지 실패 → 사용자가 수동 편집
    }

    private static object ConvertJson(JsonElement je) => je.ValueKind switch
    {
        JsonValueKind.Number => je.TryGetInt32(out var i) ? i : je.GetInt64(),
        JsonValueKind.String => je.GetString()!,
        _ => je.ToString()
    };

    #endregion
}
