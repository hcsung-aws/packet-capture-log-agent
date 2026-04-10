namespace PacketCaptureAgent.Tests;

public class ConditionEvaluatorTests
{
    private static Dictionary<string, object> State(params (string k, object v)[] pairs)
        => pairs.ToDictionary(p => p.k, p => p.v);

    [Fact]
    public void EmptyExpression_ReturnsTrue()
        => Assert.True(ConditionEvaluator.Evaluate("", new()));

    [Fact]
    public void NullExpression_ReturnsTrue()
        => Assert.True(ConditionEvaluator.Evaluate(null!, new()));

    [Theory]
    [InlineData("count == 3", true)]
    [InlineData("count != 3", false)]
    [InlineData("count > 2", true)]
    [InlineData("count < 4", true)]
    [InlineData("count >= 3", true)]
    [InlineData("count <= 3", true)]
    [InlineData("count > 3", false)]
    public void NumericComparison(string expr, bool expected)
        => Assert.Equal(expected, ConditionEvaluator.Evaluate(expr, State(("count", 3))));

    [Fact]
    public void StringComparison_Equal()
        => Assert.True(ConditionEvaluator.Evaluate("name == hero", State(("name", "hero"))));

    [Fact]
    public void StringComparison_NotEqual()
        => Assert.True(ConditionEvaluator.Evaluate("name != villain", State(("name", "hero"))));

    [Fact]
    public void And_BothTrue()
        => Assert.True(ConditionEvaluator.Evaluate("a == 1 AND b == 2", State(("a", 1), ("b", 2))));

    [Fact]
    public void And_OneFalse()
        => Assert.False(ConditionEvaluator.Evaluate("a == 1 AND b == 9", State(("a", 1), ("b", 2))));

    [Fact]
    public void Or_OneTrue()
        => Assert.True(ConditionEvaluator.Evaluate("a == 9 OR b == 2", State(("a", 1), ("b", 2))));

    [Fact]
    public void Or_BothFalse()
        => Assert.False(ConditionEvaluator.Evaluate("a == 9 OR b == 9", State(("a", 1), ("b", 2))));

    [Fact]
    public void MissingKey_ReturnsFalse()
        => Assert.False(ConditionEvaluator.Evaluate("missing == 1", new()));

    [Fact]
    public void InvalidExpression_ReturnsFalse()
        => Assert.False(ConditionEvaluator.Evaluate("not a valid expr", new()));

    [Fact]
    public void LongValue_Comparison()
        => Assert.True(ConditionEvaluator.Evaluate("id == 999999999", State(("id", 999999999L))));

    [Fact]
    public void DoubleValue_Comparison()
        => Assert.True(ConditionEvaluator.Evaluate("rate > 0.5", State(("rate", 0.8))));
}
