namespace PacketCaptureAgent.Tests;

/// <summary>FsmBuilder + FsmExecutor.SelectNextState 단위 테스트.</summary>
public class FsmTests
{
    // ── FsmBuilder ──

    static RecordingStore MakeStore(params string[][] sequences) =>
        new()
        {
            Recordings = sequences.Select(seq => new Recording
            {
                Id = "test",
                Sequence = seq.Select(a => new RecordingStep { Action = a }).ToList()
            }).ToList()
        };

    [Fact]
    public void Build_SingleRecording_CorrectTransitions()
    {
        var store = MakeStore(["login", "move", "attack", "move"]);
        var fsm = new FsmBuilder().Build(store, "test");

        Assert.Equal("connect", fsm.InitialState);
        // connect → login
        Assert.Equal(1.0f, fsm.Transitions["connect"]["login"]);
        // login → move (1/1)
        Assert.Equal(1.0f, fsm.Transitions["login"]["move"]);
        // move → attack (1/2), move has disconnect too since it's last action
        Assert.True(fsm.Transitions["move"].ContainsKey("attack"));
        // attack → move (1/1)
        Assert.Equal(1.0f, fsm.Transitions["attack"]["move"]);
        // disconnect → connect
        Assert.Equal(1.0f, fsm.Transitions["disconnect"]["connect"]);
    }

    [Fact]
    public void Build_MultipleRecordings_MergedProbabilities()
    {
        var store = MakeStore(
            ["login", "move", "attack"],
            ["login", "move", "move"]
        );
        var fsm = new FsmBuilder().Build(store, "test");

        // move → attack (1회) + move → move (1회) + move → disconnect (2회, 양쪽 마지막)
        var moveTransitions = fsm.Transitions["move"];
        Assert.True(moveTransitions.ContainsKey("attack"));
        Assert.True(moveTransitions.ContainsKey("move"));
        Assert.True(moveTransitions.ContainsKey("disconnect"));
        // 합이 ~1.0
        var sum = moveTransitions.Values.Sum();
        Assert.InRange(sum, 0.99f, 1.01f);
    }

    [Fact]
    public void Build_EmptyStore_DefaultConnectDisconnect()
    {
        var store = new RecordingStore();
        var fsm = new FsmBuilder().Build(store, "empty");

        Assert.Equal("connect", fsm.InitialState);
        // connect → login (default)
        Assert.Equal(1.0f, fsm.Transitions["connect"]["login"]);
        Assert.Equal(1.0f, fsm.Transitions["disconnect"]["connect"]);
    }

    [Fact]
    public void Build_DisconnectTransition_AddedForLastAction()
    {
        // "move" appears mid-sequence AND as last action → gets disconnect transition
        var store = MakeStore(["login", "move", "attack"], ["login", "move"]);
        var fsm = new FsmBuilder().Build(store, "test");

        Assert.True(fsm.Transitions["move"].ContainsKey("disconnect"));
        Assert.True(fsm.Transitions["move"].ContainsKey("attack"));
    }

    [Fact]
    public void Build_FirstAction_BecomesConnectTarget()
    {
        var store = MakeStore(["char_select", "enter_game"]);
        var fsm = new FsmBuilder().Build(store, "test");

        Assert.Equal(1.0f, fsm.Transitions["connect"]["char_select"]);
    }

    // ── FsmExecutor.SelectNextState ──

    [Fact]
    public void SelectNextState_SingleTarget_AlwaysReturns()
    {
        var fsm = new FsmDefinition
        {
            Transitions = new() { ["A"] = new() { ["B"] = 1.0f } }
        };

        for (int i = 0; i < 100; i++)
            Assert.Equal("B", FsmExecutor.SelectNextState(fsm, "A"));
    }

    [Fact]
    public void SelectNextState_NoTransition_ReturnsNull()
    {
        var fsm = new FsmDefinition { Transitions = new() };
        Assert.Null(FsmExecutor.SelectNextState(fsm, "unknown"));
    }

    [Fact]
    public void SelectNextState_MultipleTargets_AllReachable()
    {
        var fsm = new FsmDefinition
        {
            Transitions = new()
            {
                ["state"] = new() { ["A"] = 0.5f, ["B"] = 0.5f }
            }
        };

        var seen = new HashSet<string>();
        for (int i = 0; i < 1000; i++)
            seen.Add(FsmExecutor.SelectNextState(fsm, "state")!);

        Assert.Contains("A", seen);
        Assert.Contains("B", seen);
    }

    [Fact]
    public void SelectNextState_SkewedProbability_HighProbMoreFrequent()
    {
        var fsm = new FsmDefinition
        {
            Transitions = new()
            {
                ["state"] = new() { ["common"] = 0.9f, ["rare"] = 0.1f }
            }
        };

        int commonCount = 0;
        const int trials = 10000;
        for (int i = 0; i < trials; i++)
            if (FsmExecutor.SelectNextState(fsm, "state") == "common") commonCount++;

        // common should be ~90% ± margin
        Assert.InRange(commonCount, trials * 0.8, trials * 0.98);
    }
}
