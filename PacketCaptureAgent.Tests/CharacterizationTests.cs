using System.Text.Json;

namespace PacketCaptureAgent.Tests;

/// <summary>
/// Characterization tests — 리팩토링 전 기존 동작을 캡처.
/// WELC Test Harness: 변경 대상 순수 함수의 현재 동작을 고정.
/// </summary>
public class CharacterizationTests
{
    // ── FindBestPos (NpcAttackInterceptor / ProximityInterceptor 공통) ──

    [Theory]
    [InlineData(0, 0, 3, 3, 3, 2)]   // 위쪽이 가장 가까움
    [InlineData(5, 3, 3, 3, 4, 3)]   // 오른쪽이 가장 가까움
    [InlineData(3, 5, 3, 3, 3, 4)]   // 아래쪽이 가장 가까움
    [InlineData(1, 3, 3, 3, 2, 3)]   // 왼쪽이 가장 가까움
    [InlineData(3, 3, 3, 3, 3, 2)]   // 같은 위치 → 첫 번째 방향(위)
    public void FindBestPos_ReturnsClosestAdjacentCell(int px, int py, int nx, int ny, int ex, int ey)
    {
        // ProximityInterceptor.FindBestPos는 private static → 리플렉션으로 테스트
        var method = typeof(ProximityInterceptor).GetMethod("FindBestPos",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.NotNull(method);
        var result = (ValueTuple<int, int>)method!.Invoke(null, [px, py, nx, ny])!;
        Assert.Equal((ex, ey), result);
    }

    // ── RemoveFromTree (BehaviorTreeEditor) ──

    [Fact]
    public void RemoveFromTree_RemovesLeafAction()
    {
        var child1 = new BtAction { Id = "a" };
        var child2 = new BtAction { Id = "b" };
        var root = new BtSequence { Children = [child1, child2] };
        var tree = new BehaviorTreeDefinition { Root = root };

        // RemoveFromTree via BehaviorTreeEditor (private static)
        var method = typeof(BehaviorTreeEditor).GetMethod("RemoveFromTree",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        var result = (BtNode?)method!.Invoke(null, [root, child1]);

        Assert.NotNull(result);
        var seq = Assert.IsType<BtSequence>(result);
        Assert.Single(seq.Children);
        Assert.Equal("b", ((BtAction)seq.Children[0]).Id);
    }

    [Fact]
    public void RemoveFromTree_RemovesOnlyChild_ReturnsNull()
    {
        var child = new BtAction { Id = "a" };
        var root = new BtSequence { Children = [child] };

        var method = typeof(BehaviorTreeEditor).GetMethod("RemoveFromTree",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        var result = (BtNode?)method!.Invoke(null, [root, child]);

        Assert.Null(result); // sequence with 0 children → null
    }

    [Fact]
    public void RemoveFromTree_RemovesRoot_ReturnsNull()
    {
        var root = new BtAction { Id = "a" };

        var method = typeof(BehaviorTreeEditor).GetMethod("RemoveFromTree",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        var result = (BtNode?)method!.Invoke(null, [root, root]);

        Assert.Null(result);
    }

    // ── FieldFlattener.Flatten (ParsingResponseHandler + RecordingStore 공통) ──

    [Fact]
    public void Flatten_ScalarValues()
    {
        var state = new Dictionary<string, object>();
        FieldFlattener.Flatten(state, "SC_LOGIN.charUid", 42);
        Assert.Equal(42, state["SC_LOGIN.charUid"]);
    }

    [Fact]
    public void Flatten_NestedDict()
    {
        var state = new Dictionary<string, object>();
        var nested = new Dictionary<string, object> { ["hp"] = 100, ["mp"] = 50 };
        FieldFlattener.Flatten(state, "SC_CHAR.stats", nested);
        Assert.Equal(100, state["SC_CHAR.stats.hp"]);
        Assert.Equal(50, state["SC_CHAR.stats.mp"]);
    }

    [Fact]
    public void Flatten_ListOfStructs()
    {
        var state = new Dictionary<string, object>();
        var list = new List<object>
        {
            new Dictionary<string, object> { ["id"] = 1, ["name"] = "sword" },
            new Dictionary<string, object> { ["id"] = 2, ["name"] = "shield" }
        };
        FieldFlattener.Flatten(state, "SC_ITEMS.items", list);
        Assert.Equal(1, state["SC_ITEMS.items[0].id"]);
        Assert.Equal("sword", state["SC_ITEMS.items[0].name"]);
        Assert.Equal(2, state["SC_ITEMS.items[1].id"]);
    }

    // ── ConvertJsonElement (ScenarioBuilder — BehaviorTreeBuilder도 이것을 사용) ──

    [Fact]
    public void ConvertJsonElement_Int()
    {
        var je = JsonSerializer.Deserialize<JsonElement>("42");
        var result = ScenarioBuilder.ConvertJsonElement(je);
        Assert.IsType<int>(result);
        Assert.Equal(42, (int)result);
    }

    [Fact]
    public void ConvertJsonElement_String()
    {
        var je = JsonSerializer.Deserialize<JsonElement>("\"hello\"");
        var result = ScenarioBuilder.ConvertJsonElement(je);
        Assert.IsType<string>(result);
        Assert.Equal("hello", (string)result);
    }

    [Fact]
    public void ConvertJsonElement_Long()
    {
        var je = JsonSerializer.Deserialize<JsonElement>("3000000000");
        var result = ScenarioBuilder.ConvertJsonElement(je);
        Assert.IsType<long>(result);
        Assert.Equal(3000000000L, (long)result);
    }

    // ── ConvertJsonElement (ScenarioBuilder) ──

    [Theory]
    [InlineData("42", 42)]
    [InlineData("\"test\"", "test")]
    [InlineData("true", true)]
    [InlineData("false", false)]
    public void ConvertJsonElement_BasicTypes(string json, object expected)
    {
        var je = JsonSerializer.Deserialize<JsonElement>(json);
        var result = ScenarioBuilder.ConvertJsonElement(je);
        Assert.Equal(expected, result);
    }

    // ── GetFieldType (SequenceAnalyzer) ──

    [Theory]
    [InlineData("charUid", "uid")]
    [InlineData("npcUid", "uid")]
    [InlineData("itemId", "id")]
    [InlineData("targetId", "id")]
    [InlineData("slot", "slot")]
    [InlineData("posX", "other")]
    [InlineData("items[16].itemId", "id")]
    public void GetFieldType_SuffixClassification(string fieldName, string expected)
    {
        Assert.Equal(expected, SequenceAnalyzer.GetFieldType(fieldName));
    }
}
