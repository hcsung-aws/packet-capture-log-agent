namespace PacketCaptureAgent;

/// <summary>녹화 데이터에서 액션 간 전이 확률을 계산하여 FsmDefinition 생성.</summary>
public class FsmBuilder
{
    /// <summary>녹화 저장소에서 FSM 전이 확률 테이블 생성.</summary>
    public FsmDefinition Build(RecordingStore store, string name = "auto_fsm")
    {
        var transitionCounts = new Dictionary<string, Dictionary<string, int>>();
        string? firstAction = null;
        var lastActions = new Dictionary<string, int>(); // 녹화 마지막 액션 빈도

        foreach (var rec in store.Recordings)
        {
            if (rec.Sequence.Count == 0) continue;
            firstAction ??= rec.Sequence[0].Action;

            for (int i = 0; i < rec.Sequence.Count - 1; i++)
            {
                var from = rec.Sequence[i].Action;
                var to = rec.Sequence[i + 1].Action;

                if (!transitionCounts.TryGetValue(from, out var targets))
                    transitionCounts[from] = targets = new();
                targets.TryGetValue(to, out var c);
                targets[to] = c + 1;
            }

            // 마지막 액션 → disconnect 전이 빈도
            var last = rec.Sequence[^1].Action;
            lastActions.TryGetValue(last, out var lc);
            lastActions[last] = lc + 1;
        }

        // 빈도 → 확률 정규화
        var transitions = new Dictionary<string, Dictionary<string, float>>();
        foreach (var (from, targets) in transitionCounts)
        {
            float total = targets.Values.Sum();
            // 마지막 액션이면 disconnect 전이 추가
            if (lastActions.TryGetValue(from, out var dc))
                total += dc;
            var probs = targets.ToDictionary(kv => kv.Key, kv => MathF.Round(kv.Value / total, 3));
            if (lastActions.ContainsKey(from))
                probs["disconnect"] = MathF.Round(dc / total, 3);
            transitions[from] = probs;
        }

        // connect → 첫 액션 전이
        transitions["connect"] = new() { [firstAction ?? "login"] = 1.0f };
        // disconnect → connect 전이 (재접속)
        transitions["disconnect"] = new() { ["connect"] = 1.0f };

        return new FsmDefinition
        {
            Name = name,
            InitialState = "connect",
            Transitions = transitions
        };
    }
}
