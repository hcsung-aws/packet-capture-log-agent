using PacketCaptureAgent;

namespace PacketCaptureAgent.Tests;

public class SequenceAnalyzerTests
{
    static ReplayPacket Send(string name, Dictionary<string, object>? fields = null)
        => new(name, "SEND", fields ?? new(), TimeSpan.Zero);

    static ReplayPacket Recv(string name, Dictionary<string, object>? fields = null)
        => new(name, "RECV", fields ?? new(), TimeSpan.Zero);

    readonly SequenceAnalyzer _analyzer = new();

    [Fact]
    public void SendPacket_ClassifiedAsCore()
    {
        var packets = new List<ReplayPacket> { Send("CS_LOGIN") };
        var result = _analyzer.Classify(packets);
        Assert.Single(result);
        Assert.Equal(PacketRole.Core, result[0].Role);
    }

    [Fact]
    public void Heartbeat_ClassifiedAsNoise()
    {
        var packets = new List<ReplayPacket> { Send("CS_HEARTBEAT"), Recv("SC_HEARTBEAT") };
        var result = _analyzer.Classify(packets);
        Assert.All(result, c => Assert.Equal(PacketRole.Noise, c.Role));
    }

    [Fact]
    public void DirectResponse_ClassifiedAsCore()
    {
        var packets = new List<ReplayPacket>
        {
            Send("CS_LOGIN"),
            Recv("SC_LOGIN_RESULT", new() { { "accountUid", 12345 } })
        };
        var result = _analyzer.Classify(packets);
        Assert.Equal(PacketRole.Core, result[1].Role);
    }

    [Fact]
    public void ListResponse_ClassifiedAsCore()
    {
        var packets = new List<ReplayPacket>
        {
            Send("CS_CHAR_LIST"),
            Recv("SC_CHAR_LIST", new() { { "count", 1 } })
        };
        var result = _analyzer.Classify(packets);
        Assert.Equal(PacketRole.Core, result[1].Role);
    }

    [Fact]
    public void DataSource_FieldValueUsedInLaterSend()
    {
        var packets = new List<ReplayPacket>
        {
            Send("CS_LOGIN"),
            Recv("SC_CHAR_LIST", new() { { "charUid", 999888777 } }),
            Send("CS_CHAR_SELECT", new() { { "charUid", 999888777 } })
        };
        var result = _analyzer.Classify(packets);
        // SC_CHAR_LIST is both direct response (CS_CHAR → SC_CHAR_LIST) — wait, CS_LOGIN → SC_CHAR_LIST doesn't match
        // Actually CS_LOGIN → SC_LOGIN_RESULT pattern. SC_CHAR_LIST follows CS_LOGIN but isn't a direct response.
        // SC_CHAR_LIST has charUid=999888777 which is used in CS_CHAR_SELECT → DataSource
        Assert.Equal(PacketRole.DataSource, result[1].Role);
    }

    [Fact]
    public void UnsolicitedServerPush_ClassifiedAsConditional()
    {
        var packets = new List<ReplayPacket>
        {
            Send("CS_CHAR_SELECT"),
            Recv("SC_CHAR_INFO"),
            Recv("SC_NPC_SPAWN", new() { { "npcUid", 1 }, { "posX", 10 } }),
            Recv("SC_ATTENDANCE_INFO")
        };
        var result = _analyzer.Classify(packets);
        Assert.Equal(PacketRole.Core, result[1].Role);       // SC_CHAR_INFO = promoted first Conditional
        Assert.Equal(PacketRole.Conditional, result[2].Role); // SC_NPC_SPAWN (no send uses npcUid=1)
        Assert.Equal(PacketRole.Conditional, result[3].Role); // SC_ATTENDANCE_INFO
    }

    [Fact]
    public void NpcSpawn_WithUid_UsedInAttack_IsDataSource()
    {
        var packets = new List<ReplayPacket>
        {
            Send("CS_CHAR_SELECT"),
            Recv("SC_NPC_SPAWN", new() { { "npcUid", 5 } }),
            Send("CS_ATTACK", new() { { "targetUid", 5 } })
        };
        var result = _analyzer.Classify(packets);
        Assert.Equal(PacketRole.DataSource, result[1].Role);
    }

    [Fact]
    public void Notification_ClassifiedAsNoise()
    {
        var packets = new List<ReplayPacket>
        {
            Send("CS_ATTACK"),
            Recv("SC_ATTACK_RESULT"),
            Recv("SC_EXP_UPDATE", new() { { "exp", 100 } }),
            Recv("SC_LEVEL_UP", new() { { "level", 2 } })
        };
        var result = _analyzer.Classify(packets);
        Assert.Equal(PacketRole.Noise, result[2].Role);
        Assert.Equal(PacketRole.Noise, result[3].Role);
    }

    [Fact]
    public void GroupPackets_RepeatedPairs_Merged()
    {
        var classified = new List<ClassifiedPacket>
        {
            new(Send("CS_MOVE"), PacketRole.Core),
            new(Recv("SC_MOVE_RESULT"), PacketRole.Core),
            new(Send("CS_MOVE"), PacketRole.Core),
            new(Recv("SC_MOVE_RESULT"), PacketRole.Core),
            new(Send("CS_MOVE"), PacketRole.Core),
            new(Recv("SC_MOVE_RESULT"), PacketRole.Core),
        };
        var groups = _analyzer.GroupPackets(classified);
        // 3 repeated pairs → merged into CS_MOVE ×3, SC_MOVE_RESULT ×3
        Assert.Equal(2, groups.Count);
        Assert.Equal("CS_MOVE", groups[0].Name);
        Assert.Equal(3, groups[0].Count);
        Assert.Equal("SC_MOVE_RESULT", groups[1].Name);
        Assert.Equal(3, groups[1].Count);
    }

    [Fact]
    public void GroupPackets_ConsecutiveNpcSpawn_Grouped()
    {
        var classified = new List<ClassifiedPacket>
        {
            new(Recv("SC_NPC_SPAWN"), PacketRole.Conditional),
            new(Recv("SC_NPC_SPAWN"), PacketRole.Conditional),
            new(Recv("SC_NPC_SPAWN"), PacketRole.Conditional),
        };
        var groups = _analyzer.GroupPackets(classified);
        Assert.Single(groups);
        Assert.Equal(3, groups[0].Count);
    }

    [Fact]
    public void IsDirectResponse_Patterns()
    {
        Assert.True(SequenceAnalyzer.IsDirectResponse("CS_LOGIN", "SC_LOGIN_RESULT"));
        Assert.True(SequenceAnalyzer.IsDirectResponse("CS_CHAR_LIST", "SC_CHAR_LIST"));
        Assert.True(SequenceAnalyzer.IsDirectResponse("CS_CHAR_SELECT", "SC_CHAR_SELECT"));
        Assert.True(SequenceAnalyzer.IsDirectResponse("CS_MOVE", "SC_MOVE_RESULT"));
        Assert.False(SequenceAnalyzer.IsDirectResponse("CS_LOGIN", "SC_CHAR_LIST"));
        Assert.False(SequenceAnalyzer.IsDirectResponse("CS_CHAR_SELECT", "SC_NPC_SPAWN"));
    }

    [Fact]
    public void FormatDiagram_ProducesOutput()
    {
        var packets = new List<ReplayPacket>
        {
            Send("CS_LOGIN", new() { { "accountId", "test" } }),
            Recv("SC_LOGIN_RESULT", new() { { "accountUid", 12345 } })
        };
        var classified = _analyzer.Classify(packets);
        var groups = _analyzer.GroupPackets(classified);
        var diagram = _analyzer.FormatDiagram(groups);

        Assert.Contains("CS_LOGIN", diagram);
        Assert.Contains("SC_LOGIN_RESULT", diagram);
        Assert.Contains("Client", diagram);
        Assert.Contains("Server", diagram);
    }

    [Fact]
    public void DetectDynamicFields_FindsRecvToSendDependency()
    {
        var packets = new List<ReplayPacket>
        {
            Send("CS_LOGIN", new() { { "accountId", "test" } }),
            Recv("SC_CHAR_LIST", new() { { "chars[0].charUid", 1001 } }),
            Send("CS_CHAR_SELECT", new() { { "charUid", 1001 } })
        };

        var result = _analyzer.DetectDynamicFields(packets);

        Assert.Single(result);
        Assert.Equal("CS_CHAR_SELECT", result[0].SendPacket);
        Assert.Equal("charUid", result[0].SendField);
        Assert.Equal("SC_CHAR_LIST", result[0].SourcePacket);
        Assert.Equal("chars[0].charUid", result[0].SourceField);
    }

    [Fact]
    public void DetectDynamicFields_DeduplicatesRepeatedSend()
    {
        var packets = new List<ReplayPacket>
        {
            Recv("SC_NPC_SPAWN", new() { { "npcUid", 5 } }),
            Send("CS_ATTACK", new() { { "targetUid", 5 } }),
            Send("CS_ATTACK", new() { { "targetUid", 5 } })
        };

        var result = _analyzer.DetectDynamicFields(packets);

        Assert.Single(result);
    }

    [Fact]
    public void DetectDynamicFields_SuffixTypeFilter_PrefersUidOverId()
    {
        var packets = new List<ReplayPacket>
        {
            Recv("SC_INVENTORY_LIST", new() { { "items[16].itemId", 5 } }),
            Recv("SC_NPC_SPAWN", new() { { "npcUid", 5 } }),
            Send("CS_ATTACK", new() { { "targetUid", 5 } })
        };

        var result = _analyzer.DetectDynamicFields(packets);

        Assert.Single(result);
        Assert.Equal("SC_NPC_SPAWN", result[0].SourcePacket);
        Assert.Equal("npcUid", result[0].SourceField);
    }

    [Fact]
    public void DetectDynamicFields_ManualMapping_OverridesAuto()
    {
        var packets = new List<ReplayPacket>
        {
            Recv("SC_NPC_SPAWN", new() { { "npcUid", 5 } }),
            Recv("SC_OTHER", new() { { "otherUid", 5 } }),
            Send("CS_ATTACK", new() { { "targetUid", 5 } })
        };
        var manual = new List<FieldMapping>
        {
            new() { Target = "CS_ATTACK.targetUid", Source = "SC_OTHER.otherUid" }
        };

        var result = _analyzer.DetectDynamicFields(packets, manual);

        Assert.Single(result);
        Assert.Equal("SC_OTHER", result[0].SourcePacket);
    }

    [Fact]
    public void DetectDynamicFields_StaticMapping_SuppressesAutoDetect()
    {
        var packets = new List<ReplayPacket>
        {
            Recv("SC_NPC_SPAWN", new() { { "npcUid", 5 } }),
            Send("CS_ATTACK", new() { { "targetUid", 5 } })
        };
        var manual = new List<FieldMapping>
        {
            new() { Target = "CS_ATTACK.targetUid", Source = "static" }
        };

        var result = _analyzer.DetectDynamicFields(packets, manual);

        Assert.Empty(result);
    }

    [Fact]
    public void DetectDynamicFields_ExternalMapping_IncludedInResult()
    {
        var packets = new List<ReplayPacket>
        {
            Send("CS_LOGIN", new() { { "accountId", "test" } })
        };
        var manual = new List<FieldMapping>
        {
            new() { Target = "CS_LOGIN.accountId", Source = "external" }
        };

        var result = _analyzer.DetectDynamicFields(packets, manual);

        Assert.Single(result);
        Assert.Equal("external", result[0].SourcePacket);
    }

    [Fact]
    public void GetFieldType_ClassifiesSuffixCorrectly()
    {
        Assert.Equal("uid", SequenceAnalyzer.GetFieldType("targetUid"));
        Assert.Equal("uid", SequenceAnalyzer.GetFieldType("npcUid"));
        Assert.Equal("uid", SequenceAnalyzer.GetFieldType("chars[0].charUid"));
        Assert.Equal("id", SequenceAnalyzer.GetFieldType("itemId"));
        Assert.Equal("id", SequenceAnalyzer.GetFieldType("items[16].itemId"));
        Assert.Equal("slot", SequenceAnalyzer.GetFieldType("slot"));
        Assert.Equal("other", SequenceAnalyzer.GetFieldType("count"));
    }
}
