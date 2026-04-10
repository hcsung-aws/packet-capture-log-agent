namespace PacketCaptureAgent.Tests;

public class FieldFlattenerTests
{
    [Fact]
    public void SimpleValue()
    {
        var state = new Dictionary<string, object>();
        FieldFlattener.Flatten(state, "hp", 100);
        Assert.Equal(100, state["hp"]);
    }

    [Fact]
    public void ListOfPrimitives()
    {
        var state = new Dictionary<string, object>();
        var list = new List<object> { 10, 20, 30 };
        FieldFlattener.Flatten(state, "items", list);

        Assert.Same(list, state["items"]);
        Assert.Equal(10, state["items[0]"]);
        Assert.Equal(20, state["items[1]"]);
        Assert.Equal(30, state["items[2]"]);
    }

    [Fact]
    public void ListOfStructs()
    {
        var state = new Dictionary<string, object>();
        var list = new List<object>
        {
            new Dictionary<string, object> { ["name"] = "sword", ["dmg"] = 10 },
            new Dictionary<string, object> { ["name"] = "shield", ["dmg"] = 0 }
        };
        FieldFlattener.Flatten(state, "equip", list);

        Assert.Equal("sword", state["equip[0].name"]);
        Assert.Equal(10, state["equip[0].dmg"]);
        Assert.Equal("shield", state["equip[1].name"]);
    }

    [Fact]
    public void NestedStruct()
    {
        var state = new Dictionary<string, object>();
        var nested = new Dictionary<string, object>
        {
            ["x"] = 5,
            ["y"] = 10
        };
        FieldFlattener.Flatten(state, "pos", nested);

        Assert.Equal(5, state["pos.x"]);
        Assert.Equal(10, state["pos.y"]);
    }

    [Fact]
    public void DeeplyNested()
    {
        var state = new Dictionary<string, object>();
        var inner = new Dictionary<string, object> { ["z"] = 99 };
        var outer = new Dictionary<string, object> { ["inner"] = inner };
        FieldFlattener.Flatten(state, "a", outer);

        Assert.Equal(99, state["a.inner.z"]);
    }
}
