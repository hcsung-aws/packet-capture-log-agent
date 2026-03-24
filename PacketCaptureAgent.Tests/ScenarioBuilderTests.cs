namespace PacketCaptureAgent.Tests;

public class ScenarioBuilderTests
{
    static ActionCatalog MakeCatalog() => new()
    {
        Protocol = "Test",
        Actions = new()
        {
            new CatalogAction
            {
                Id = "login", Name = "Login", Phase = "Login",
                Packets = new()
                {
                    new ActionPacket { Direction = "SEND", Name = "CS_LOGIN", Role = "Core",
                        Fields = new() { { "accountId", "test" }, { "password", "pass" } } },
                    new ActionPacket { Direction = "RECV", Name = "SC_LOGIN_RESULT", Role = "Core" },
                    new ActionPacket { Direction = "RECV", Name = "SC_CHAR_LIST", Role = "DataSource" }
                },
                DynamicFields = new(),
                Outputs = new() { "SC_CHAR_LIST.chars[0].charUid" },
                Dependencies = new()
            },
            new CatalogAction
            {
                Id = "char_select", Name = "CharSelect", Phase = "Enter Game",
                Packets = new()
                {
                    new ActionPacket { Direction = "SEND", Name = "CS_CHAR_SELECT", Role = "Core",
                        Fields = new() { { "charUid", 1001 } } },
                    new ActionPacket { Direction = "RECV", Name = "SC_CHAR_INFO", Role = "Core" }
                },
                DynamicFields = new()
                {
                    new ActionDynamicField { Packet = "CS_CHAR_SELECT", Field = "charUid", Source = "SC_CHAR_LIST.chars[0].charUid" }
                },
                Outputs = new(),
                Dependencies = new() { "login" }
            },
            new CatalogAction
            {
                Id = "attack", Name = "Attack", Phase = "Gameplay",
                Packets = new()
                {
                    new ActionPacket { Direction = "SEND", Name = "CS_ATTACK", Role = "Core",
                        Fields = new() { { "targetUid", 5001 } } },
                    new ActionPacket { Direction = "RECV", Name = "SC_ATTACK_RESULT", Role = "Core" }
                },
                DynamicFields = new()
                {
                    new ActionDynamicField { Packet = "CS_ATTACK", Field = "targetUid", Source = "SC_NPC_SPAWN.npcUid" }
                },
                Outputs = new(),
                Dependencies = new() { "char_select" }
            }
        }
    };

    // ── Validate ──

    [Fact]
    public void Validate_AllActionsExist_NoDeps_ReturnsEmpty()
    {
        var scenario = new ScenarioDefinition
        {
            Steps = new() { new() { Action = "login" }, new() { Action = "char_select" }, new() { Action = "attack" } }
        };
        var errors = new ScenarioBuilder().Validate(scenario, MakeCatalog());
        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_MissingAction_ReturnsError()
    {
        var scenario = new ScenarioDefinition
        {
            Steps = new() { new() { Action = "nonexistent" } }
        };
        var errors = new ScenarioBuilder().Validate(scenario, MakeCatalog());
        Assert.Single(errors);
        Assert.Contains("nonexistent", errors[0]);
    }

    [Fact]
    public void Validate_MissingDependency_ReturnsError()
    {
        var scenario = new ScenarioDefinition
        {
            Steps = new() { new() { Action = "attack" } }  // char_select 없음
        };
        var errors = new ScenarioBuilder().Validate(scenario, MakeCatalog());
        Assert.Single(errors);
        Assert.Contains("char_select", errors[0]);
    }

    // ── Build ──

    [Fact]
    public void Build_SingleAction_ProducesCorrectPackets()
    {
        var scenario = new ScenarioDefinition
        {
            Steps = new() { new() { Action = "login" } }
        };
        var packets = new ScenarioBuilder().Build(scenario, MakeCatalog());

        Assert.Equal(3, packets.Count);
        Assert.Equal("CS_LOGIN", packets[0].Name);
        Assert.Equal("SEND", packets[0].Direction);
        Assert.Equal("test", packets[0].Fields["accountId"]);
        Assert.Equal("SC_LOGIN_RESULT", packets[1].Name);
        Assert.Equal("RECV", packets[1].Direction);
        Assert.Equal("SC_CHAR_LIST", packets[2].Name);
    }

    [Fact]
    public void Build_RepeatAction_DuplicatesPackets()
    {
        var scenario = new ScenarioDefinition
        {
            Steps = new() { new() { Action = "attack", Repeat = 3 } }
        };
        var packets = new ScenarioBuilder().Build(scenario, MakeCatalog());

        // attack = 2 packets (SEND + RECV) × 3 = 6
        Assert.Equal(6, packets.Count);
        Assert.Equal(3, packets.Count(p => p.Name == "CS_ATTACK"));
        Assert.Equal(3, packets.Count(p => p.Name == "SC_ATTACK_RESULT"));
    }

    [Fact]
    public void Build_Overrides_AppliedToSendFields()
    {
        var scenario = new ScenarioDefinition
        {
            Steps = new() { new() { Action = "login", Overrides = new() { { "accountId", "loadtest_001" } } } }
        };
        var packets = new ScenarioBuilder().Build(scenario, MakeCatalog());

        Assert.Equal("loadtest_001", packets[0].Fields["accountId"]);
        Assert.Equal("pass", packets[0].Fields["password"]); // 미변경
    }

    [Fact]
    public void Build_MultipleSteps_SequentialTimestamps()
    {
        var scenario = new ScenarioDefinition
        {
            Steps = new() { new() { Action = "login" }, new() { Action = "char_select" } }
        };
        var packets = new ScenarioBuilder().Build(scenario, MakeCatalog());

        // 5 packets total, timestamps should be sequential
        Assert.Equal(5, packets.Count);
        for (int i = 1; i < packets.Count; i++)
            Assert.True(packets[i].Timestamp > packets[i - 1].Timestamp);
    }

    [Fact]
    public void Build_RecvMultipleCount_ExpandsPackets()
    {
        var catalog = new ActionCatalog
        {
            Protocol = "Test",
            Actions = new()
            {
                new CatalogAction
                {
                    Id = "enter", Name = "Enter",
                    Packets = new()
                    {
                        new ActionPacket { Direction = "SEND", Name = "CS_ENTER", Role = "Core", Fields = new() },
                        new ActionPacket { Direction = "RECV", Name = "SC_NPC_SPAWN ×5", Role = "Conditional" }
                    },
                    DynamicFields = new(), Outputs = new(), Dependencies = new()
                }
            }
        };
        var scenario = new ScenarioDefinition { Steps = new() { new() { Action = "enter" } } };
        var packets = new ScenarioBuilder().Build(scenario, catalog);

        Assert.Equal(6, packets.Count); // 1 SEND + 5 RECV
        Assert.Equal(5, packets.Count(p => p.Name == "SC_NPC_SPAWN"));
    }

    // ── CollectDynamicFields ──

    [Fact]
    public void CollectDynamicFields_GathersFromAllSteps()
    {
        var scenario = new ScenarioDefinition
        {
            Steps = new() { new() { Action = "char_select" }, new() { Action = "attack" } }
        };
        var fields = new ScenarioBuilder().CollectDynamicFields(scenario, MakeCatalog());

        Assert.Equal(2, fields.Count);
        Assert.Contains(fields, f => f.Field == "charUid");
        Assert.Contains(fields, f => f.Field == "targetUid");
    }

    // ── DynamicFieldInterceptor ──

    [Fact]
    public void DynamicFieldInterceptor_InjectsFromSharedState()
    {
        var dynamicFields = new List<ActionDynamicField>
        {
            new() { Packet = "CS_CHAR_SELECT", Field = "charUid", Source = "SC_CHAR_LIST.chars[0].charUid" }
        };
        var sharedState = new Dictionary<string, object>
        {
            { "SC_CHAR_LIST.chars[0].charUid", 9999 }
        };

        var interceptor = new DynamicFieldInterceptor(dynamicFields, sharedState);
        var packet = new ReplayPacket("CS_CHAR_SELECT", "SEND", new() { { "charUid", 1001 } }, TimeSpan.Zero);

        Assert.True(interceptor.ShouldIntercept(packet, new GameWorldState()));

        var modified = interceptor.Prepare(null!, packet);
        Assert.Equal(9999, modified.Fields["charUid"]);
    }

    [Fact]
    public void DynamicFieldInterceptor_SkipsRecvPackets()
    {
        var dynamicFields = new List<ActionDynamicField>
        {
            new() { Packet = "CS_ATTACK", Field = "targetUid", Source = "SC_NPC_SPAWN.npcUid" }
        };
        var interceptor = new DynamicFieldInterceptor(dynamicFields, new());
        var recv = new ReplayPacket("SC_ATTACK_RESULT", "RECV", new(), TimeSpan.Zero);

        Assert.False(interceptor.ShouldIntercept(recv, new GameWorldState()));
    }

    [Fact]
    public void DynamicFieldInterceptor_NoMatch_ReturnsOriginal()
    {
        var interceptor = new DynamicFieldInterceptor(new(), new());
        var packet = new ReplayPacket("CS_MOVE", "SEND", new() { { "dirX", 1 } }, TimeSpan.Zero);

        Assert.False(interceptor.ShouldIntercept(packet, new GameWorldState()));
    }
}
