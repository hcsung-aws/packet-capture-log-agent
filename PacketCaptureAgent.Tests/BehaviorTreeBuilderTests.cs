using System.Text.Json;

namespace PacketCaptureAgent.Tests;

/// <summary>BehaviorTreeBuilder 단위 테스트 — 녹화→BT 변환 로직 검증.</summary>
public class BehaviorTreeBuilderTests
{
    // ── 헬퍼 ──

    static Recording MakeRecording(params string[] actions) => new()
    {
        Id = "test", Sequence = actions.Select(a => new RecordingStep
        {
            Action = a, RecvState = new Dictionary<string, object>()
        }).ToList()
    };

    static Recording MakeRecordingWithState(params (string action, Dictionary<string, object> state)[] steps) => new()
    {
        Id = "test", Sequence = steps.Select(s => new RecordingStep
        {
            Action = s.action, RecvState = s.state
        }).ToList()
    };

    static RecordingStore Store(params Recording[] recs) => new() { Recordings = recs.ToList() };

    // ── AnalyzeFieldDynamics ──

    [Fact]
    public void AnalyzeFieldDynamics_StaticField_NotDynamic()
    {
        var rec = MakeRecordingWithState(
            ("login", new() { ["SC_LOGIN.uid"] = 100 }),
            ("move", new() { ["SC_LOGIN.uid"] = 100 }));

        var dynamic = BehaviorTreeBuilder.AnalyzeFieldDynamics([rec]);
        Assert.DoesNotContain("SC_LOGIN.uid", dynamic);
    }

    [Fact]
    public void AnalyzeFieldDynamics_ChangingField_IsDynamic()
    {
        var rec = MakeRecordingWithState(
            ("move", new() { ["SC_CHAR.posX"] = 5 }),
            ("move", new() { ["SC_CHAR.posX"] = 8 }));

        var dynamic = BehaviorTreeBuilder.AnalyzeFieldDynamics([rec]);
        Assert.Contains("SC_CHAR.posX", dynamic);
    }

    // ── Build: 단일 녹화 → Sequence ──

    [Fact]
    public void Build_SingleRecording_ProducesSequence()
    {
        var store = Store(MakeRecording("login", "move", "attack"));
        var tree = new BehaviorTreeBuilder().Build(store);

        // 단일 경로 → sequence of actions
        var actions = CollectActions(tree.Root);
        Assert.Equal(["login", "move", "attack"], actions);
    }

    // ── Build: 반복 액션 → Repeat ──

    [Fact]
    public void Build_RepeatedAction_ProducesRepeat()
    {
        var store = Store(MakeRecording("login", "move", "move", "move"));
        var tree = new BehaviorTreeBuilder().Build(store);

        // login → repeat(3, move)
        var seq = Assert.IsType<BtSequence>(tree.Root);
        Assert.Equal(2, seq.Children.Count);
        Assert.Equal("login", ((BtAction)seq.Children[0]).Id);
        var repeat = Assert.IsType<BtRepeat>(seq.Children[1]);
        Assert.Equal(3, repeat.Count);
        Assert.Equal("move", ((BtAction)repeat.Child).Id);
    }

    // ── Build: 분기 → Selector ──

    [Fact]
    public void Build_DivergingRecordings_ProducesSelector()
    {
        var store = Store(
            MakeRecording("login", "attack"),
            MakeRecording("login", "move"));
        var tree = new BehaviorTreeBuilder().Build(store);

        // login → selector(attack, move)
        var seq = Assert.IsType<BtSequence>(tree.Root);
        Assert.Equal("login", ((BtAction)seq.Children[0]).Id);
        var selector = Assert.IsType<BtSelector>(seq.Children[1]);
        Assert.Equal(2, selector.Children.Count);
    }

    // ── InjectExplorePhases ──

    [Fact]
    public void Build_WithFieldVariants_InjectsExploreRepeat()
    {
        var store = Store(MakeRecording("login", "move"));
        var catalog = new ActionCatalog
        {
            Actions =
            [
                new CatalogAction { Id = "login", Packets = [], DynamicFields = [] },
                new CatalogAction
                {
                    Id = "move", Packets = [], DynamicFields = [],
                    FieldVariants = new()
                    {
                        ["dirX"] = [JsonSerializer.Deserialize<JsonElement>("1"), JsonSerializer.Deserialize<JsonElement>("-1")],
                        ["dirY"] = [JsonSerializer.Deserialize<JsonElement>("0"), JsonSerializer.Deserialize<JsonElement>("1")]
                    }
                }
            ]
        };

        var tree = new BehaviorTreeBuilder().Build(store, "test", catalog);

        // explore repeat이 삽입되어야 함
        var allActions = CollectActions(tree.Root);
        Assert.Contains("move", allActions);
        // repeat 노드가 존재해야 함
        Assert.True(HasRepeatFor(tree.Root, "move"));
    }

    // ── JSON 직렬화 왕복 ──

    [Fact]
    public void Build_SerializeDeserialize_Roundtrip()
    {
        var store = Store(
            MakeRecording("login", "move", "attack"),
            MakeRecording("login", "move", "move"));
        var tree = new BehaviorTreeBuilder().Build(store, "roundtrip_test");

        var json = JsonSerializer.Serialize(tree, BehaviorTreeDefinition.JsonOpts);
        var loaded = JsonSerializer.Deserialize<BehaviorTreeDefinition>(json, BehaviorTreeDefinition.JsonOpts)!;

        Assert.Equal("roundtrip_test", loaded.Name);
        var origActions = CollectActions(tree.Root);
        var loadedActions = CollectActions(loaded.Root);
        Assert.Equal(origActions, loadedActions);
    }

    // ── 유틸 ──

    static List<string> CollectActions(BtNode node) => node switch
    {
        BtAction a => [a.Id],
        BtSequence s => s.Children.SelectMany(CollectActions).ToList(),
        BtSelector s => s.Children.SelectMany(CollectActions).ToList(),
        BtRepeat r => CollectActions(r.Child),
        _ => []
    };

    static bool HasRepeatFor(BtNode node, string actionId) => node switch
    {
        BtRepeat r when r.Child is BtAction a && a.Id == actionId => true,
        BtSequence s => s.Children.Any(c => HasRepeatFor(c, actionId)),
        BtSelector s => s.Children.Any(c => HasRepeatFor(c, actionId)),
        BtRepeat r => HasRepeatFor(r.Child, actionId),
        _ => false
    };
}
