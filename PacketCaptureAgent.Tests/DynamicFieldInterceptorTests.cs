namespace PacketCaptureAgent.Tests;

public class DynamicFieldInterceptorTests
{
    private static ReplayPacket SendPacket(string name, Dictionary<string, object>? fields = null)
        => new(name, "SEND", fields ?? new(), TimeSpan.Zero);

    private static ReplayPacket RecvPacket(string name)
        => new(name, "RECV", new(), TimeSpan.Zero);

    [Fact]
    public void ShouldIntercept_SendWithMapping_ReturnsTrue()
    {
        var fields = new List<ActionDynamicField>
        {
            new() { Packet = "CS_ATTACK", Field = "targetUid", Source = "npcUid" }
        };
        var interceptor = new DynamicFieldInterceptor(fields, new());
        var packet = SendPacket("CS_ATTACK");

        Assert.True(interceptor.ShouldIntercept(packet, new GameWorldState()));
    }

    [Fact]
    public void ShouldIntercept_SendWithoutMapping_ReturnsFalse()
    {
        var interceptor = new DynamicFieldInterceptor(new(), new());
        Assert.False(interceptor.ShouldIntercept(SendPacket("CS_MOVE"), new GameWorldState()));
    }

    [Fact]
    public void ShouldIntercept_RecvPacket_ReturnsFalse()
    {
        var fields = new List<ActionDynamicField>
        {
            new() { Packet = "SC_RESULT", Field = "x", Source = "y" }
        };
        var interceptor = new DynamicFieldInterceptor(fields, new());
        Assert.False(interceptor.ShouldIntercept(RecvPacket("SC_RESULT"), new GameWorldState()));
    }

    [Fact]
    public async Task PrepareAsync_InjectsFieldFromSharedState()
    {
        var sharedState = new Dictionary<string, object> { ["npcUid"] = 42UL };
        var fields = new List<ActionDynamicField>
        {
            new() { Packet = "CS_ATTACK", Field = "targetUid", Source = "npcUid" }
        };
        var interceptor = new DynamicFieldInterceptor(fields, sharedState);
        var original = SendPacket("CS_ATTACK", new() { ["targetUid"] = 0UL });

        var result = await interceptor.PrepareAsync(null!, original);

        Assert.Equal(42UL, result.Fields["targetUid"]);
    }

    [Fact]
    public async Task PrepareAsync_SourceMissing_KeepsOriginal()
    {
        var fields = new List<ActionDynamicField>
        {
            new() { Packet = "CS_ATTACK", Field = "targetUid", Source = "missing" }
        };
        var interceptor = new DynamicFieldInterceptor(fields, new());
        var original = SendPacket("CS_ATTACK", new() { ["targetUid"] = 99 });

        var result = await interceptor.PrepareAsync(null!, original);

        Assert.Equal(99, result.Fields["targetUid"]);
    }

    [Fact]
    public async Task PrepareAsync_NoMapping_ReturnsOriginal()
    {
        var interceptor = new DynamicFieldInterceptor(new(), new());
        var original = SendPacket("CS_MOVE", new() { ["dir"] = 1 });

        var result = await interceptor.PrepareAsync(null!, original);

        Assert.Same(original, result);
    }

    [Fact]
    public void Priority_IsZero()
        => Assert.Equal(0, new DynamicFieldInterceptor(new(), new()).Priority);
}
