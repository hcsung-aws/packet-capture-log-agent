namespace PacketCaptureAgent.Tests;

public class ActionCatalogBuilderTests
{
    static ReplayPacket Send(string name, Dictionary<string, object>? fields = null)
        => new(name, "SEND", fields ?? new(), TimeSpan.Zero);
    static ReplayPacket Recv(string name, Dictionary<string, object>? fields = null)
        => new(name, "RECV", fields ?? new(), TimeSpan.Zero);

    static ProtocolDefinition MinimalProtocol(params string[] packetNames)
    {
        var proto = new ProtocolDefinition
        {
            Protocol = new ProtocolInfo { Name = "Test", Endian = "little" },
            Packets = packetNames.Select((n, i) => new PacketDefinition { Name = n, Type = i + 1 }).ToList()
        };
        return proto;
    }

    [Fact]
    public void BuildActions_CreatesActionPerSendPacket()
    {
        var packets = new List<ReplayPacket>
        {
            Send("CS_LOGIN", new() { { "accountId", "test" } }),
            Recv("SC_LOGIN_RESULT", new() { { "result", 0 } }),
            Send("CS_MOVE", new() { { "dirX", 1 } }),
            Recv("SC_MOVE_RESULT", new() { { "posX", 10 } })
        };
        var analyzer = new SequenceAnalyzer();
        var classified = analyzer.Classify(packets);
        var protocol = MinimalProtocol("CS_LOGIN", "SC_LOGIN_RESULT", "CS_MOVE", "SC_MOVE_RESULT");

        var builder = new ActionCatalogBuilder();
        var actions = builder.BuildActions(packets, classified, new(), protocol, "test.log");

        Assert.Equal(2, actions.Count);
        Assert.Equal("login", actions[0].Id);
        Assert.Equal("Login", actions[0].Name);
        Assert.Equal("move", actions[1].Id);
    }

    [Fact]
    public void BuildActions_MergesRepeatedActions()
    {
        var packets = new List<ReplayPacket>
        {
            Send("CS_MOVE", new() { { "dirX", 1 } }),
            Recv("SC_MOVE_RESULT"),
            Send("CS_MOVE", new() { { "dirX", -1 } }),
            Recv("SC_MOVE_RESULT")
        };
        var analyzer = new SequenceAnalyzer();
        var classified = analyzer.Classify(packets);
        var protocol = MinimalProtocol("CS_MOVE", "SC_MOVE_RESULT");

        var builder = new ActionCatalogBuilder();
        var actions = builder.BuildActions(packets, classified, new(), protocol, "test.log");

        Assert.Single(actions);
        Assert.Equal(2, actions[0].RepeatCount);
    }

    [Fact]
    public void BuildActions_AttachesDynamicFields()
    {
        var packets = new List<ReplayPacket>
        {
            Send("CS_LOGIN"),
            Recv("SC_CHAR_LIST", new() { { "chars[0].charUid", 1001 } }),
            Send("CS_CHAR_SELECT", new() { { "charUid", 1001 } })
        };
        var analyzer = new SequenceAnalyzer();
        var classified = analyzer.Classify(packets);
        var dynamicFields = analyzer.DetectDynamicFields(packets);
        var protocol = MinimalProtocol("CS_LOGIN", "SC_CHAR_LIST", "CS_CHAR_SELECT");

        var builder = new ActionCatalogBuilder();
        var actions = builder.BuildActions(packets, classified, dynamicFields, protocol, "test.log");

        var selectAction = actions.First(a => a.Id == "char_select");
        Assert.Single(selectAction.DynamicFields);
        Assert.Equal("charUid", selectAction.DynamicFields[0].Field);
        Assert.Contains("SC_CHAR_LIST", selectAction.DynamicFields[0].Source);
    }

    [Fact]
    public void BuildActions_ComputesDependencies()
    {
        var packets = new List<ReplayPacket>
        {
            Send("CS_LOGIN"),
            Recv("SC_CHAR_LIST", new() { { "chars[0].charUid", 1001 } }),
            Send("CS_CHAR_SELECT", new() { { "charUid", 1001 } })
        };
        var analyzer = new SequenceAnalyzer();
        var classified = analyzer.Classify(packets);
        var dynamicFields = analyzer.DetectDynamicFields(packets);
        var protocol = MinimalProtocol("CS_LOGIN", "SC_CHAR_LIST", "CS_CHAR_SELECT");

        var builder = new ActionCatalogBuilder();
        var actions = builder.BuildActions(packets, classified, dynamicFields, protocol, "test.log");

        var selectAction = actions.First(a => a.Id == "char_select");
        Assert.Contains("login", selectAction.Dependencies);
    }

    [Fact]
    public void Merge_AddsNewActions_KeepsExisting()
    {
        var protocol = MinimalProtocol("CS_LOGIN", "CS_MOVE", "CS_ATTACK");
        var existing = new ActionCatalog
        {
            Protocol = "Test",
            Actions = new() { new CatalogAction { Id = "attack", Name = "Attack" } }
        };
        var newActions = new List<CatalogAction>
        {
            new() { Id = "login", Name = "Login" },
            new() { Id = "move", Name = "Move" }
        };

        var builder = new ActionCatalogBuilder();
        var merged = builder.Merge(existing, newActions, protocol);

        Assert.Equal(3, merged.Actions.Count);
        Assert.Contains(merged.Actions, a => a.Id == "attack");
        Assert.Contains(merged.Actions, a => a.Id == "login");
        Assert.Contains(merged.Actions, a => a.Id == "move");
    }

    [Fact]
    public void Merge_UpdatesExistingAction()
    {
        var protocol = MinimalProtocol("CS_LOGIN");
        var existing = new ActionCatalog
        {
            Protocol = "Test",
            Actions = new() { new CatalogAction { Id = "login", Name = "Login", RepeatCount = 1 } }
        };
        var newActions = new List<CatalogAction>
        {
            new() { Id = "login", Name = "Login", RepeatCount = 3, SourceLog = "new.log" }
        };

        var builder = new ActionCatalogBuilder();
        var merged = builder.Merge(existing, newActions, protocol);

        Assert.Single(merged.Actions);
        Assert.Equal(3, merged.Actions[0].RepeatCount);
        Assert.Equal("new.log", merged.Actions[0].SourceLog);
    }

    [Fact]
    public void Merge_RemovesStaleActions()
    {
        // 프로토콜에서 CS_ATTACK이 삭제된 경우
        var protocol = MinimalProtocol("CS_LOGIN");
        var existing = new ActionCatalog
        {
            Protocol = "Test",
            Actions = new()
            {
                new CatalogAction { Id = "login", Name = "Login" },
                new CatalogAction { Id = "attack", Name = "Attack" }
            }
        };

        var builder = new ActionCatalogBuilder();
        var merged = builder.Merge(existing, new(), protocol);

        Assert.Single(merged.Actions);
        Assert.Equal("login", merged.Actions[0].Id);
    }
}
