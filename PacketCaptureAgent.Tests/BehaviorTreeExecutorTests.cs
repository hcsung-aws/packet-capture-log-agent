namespace PacketCaptureAgent.Tests;

/// <summary>BehaviorTreeExecutor 단위 테스트 — 조건 평가 + 상태 표현식 해석.</summary>
public class BehaviorTreeExecutorTests
{
    // ── ConditionEvaluator (BT 실행 시 사용) ──

    [Theory]
    [InlineData("SC_CHAR.level == 10", true)]
    [InlineData("SC_CHAR.level >= 5", true)]
    [InlineData("SC_CHAR.level > 10", false)]
    [InlineData("SC_CHAR.level != 5", true)]
    public void ConditionEvaluator_NumericComparison(string expr, bool expected)
    {
        var state = new Dictionary<string, object> { ["SC_CHAR.level"] = 10 };
        Assert.Equal(expected, ConditionEvaluator.Evaluate(expr, state));
    }

    [Fact]
    public void ConditionEvaluator_And()
    {
        var state = new Dictionary<string, object> { ["a"] = 1, ["b"] = 2 };
        Assert.True(ConditionEvaluator.Evaluate("a == 1 AND b == 2", state));
        Assert.False(ConditionEvaluator.Evaluate("a == 1 AND b == 9", state));
    }

    [Fact]
    public void ConditionEvaluator_Or()
    {
        var state = new Dictionary<string, object> { ["a"] = 1 };
        Assert.True(ConditionEvaluator.Evaluate("a == 1 OR a == 2", state));
        Assert.True(ConditionEvaluator.Evaluate("a == 9 OR a == 1", state));
        Assert.False(ConditionEvaluator.Evaluate("a == 9 OR a == 8", state));
    }

    [Fact]
    public void ConditionEvaluator_MissingKey_ReturnsFalse()
    {
        var state = new Dictionary<string, object>();
        Assert.False(ConditionEvaluator.Evaluate("missing == 1", state));
    }

    [Fact]
    public void ConditionEvaluator_EmptyExpression_ReturnsTrue()
    {
        Assert.True(ConditionEvaluator.Evaluate("", new()));
        Assert.True(ConditionEvaluator.Evaluate("  ", new()));
    }

    // ── ActionExecutor.ResolveStateExpression ──

    [Fact]
    public void ResolveStateExpression_DirectKey_ReturnsRandomBound()
    {
        var state = new Dictionary<string, object> { ["count"] = 5 };
        var result = ActionExecutor.ResolveStateExpression("{state_random:count}", state);
        Assert.IsType<int>(result);
        Assert.InRange((int)result, 0, 4);
    }

    [Fact]
    public void ResolveStateExpression_ArrayField_ReturnsFieldValue()
    {
        var state = new Dictionary<string, object>
        {
            ["items"] = new List<object> { "a", "b", "c" },
            ["items[0].id"] = 10,
            ["items[1].id"] = 20,
            ["items[2].id"] = 30
        };
        var result = ActionExecutor.ResolveStateExpression("{state_random:items.id}", state);
        Assert.Contains(result, new object[] { 10, 20, 30 });
    }

    [Fact]
    public void ResolveStateExpression_NonPattern_ReturnsOriginal()
    {
        var state = new Dictionary<string, object>();
        Assert.Equal("hello", ActionExecutor.ResolveStateExpression("hello", state));
        Assert.Equal(42, ActionExecutor.ResolveStateExpression(42, state));
    }
}
