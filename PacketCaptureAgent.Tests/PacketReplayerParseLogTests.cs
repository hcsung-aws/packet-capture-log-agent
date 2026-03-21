namespace PacketCaptureAgent.Tests;

/// <summary>
/// PacketReplayer.ParseLog tests — 로그 텍스트 → ReplayPacket 리스트 변환 검증.
/// </summary>
public class PacketReplayerParseLogTests
{
    private static PacketReplayer CreateReplayer()
    {
        var protocol = new ProtocolDefinition
        {
            Protocol = new ProtocolInfo
            {
                Name = "Test", Endian = "little",
                Header = new HeaderInfo
                {
                    Size = 4, SizeField = "length", TypeField = "type",
                    Fields = new List<HeaderFieldInfo>
                    {
                        new() { Name = "length", Type = "uint16", Offset = 0 },
                        new() { Name = "type", Type = "uint16", Offset = 2 },
                    }
                }
            }
        };
        return new PacketReplayer(protocol);
    }

    private static string WriteTempLog(string content)
    {
        var path = Path.GetTempFileName();
        File.WriteAllText(path, content);
        return path;
    }

    [Fact]
    public void ParseLog_SingleSendPacket()
    {
        var log = WriteTempLog(
            "[00:00:01.100] SEND CS_MOVE (6 bytes)\n" +
            "  dirX: -1\n" +
            "  dirY: 1\n");
        try
        {
            var packets = CreateReplayer().ParseLog(log);

            Assert.Single(packets);
            Assert.Equal("CS_MOVE", packets[0].Name);
            Assert.Equal("SEND", packets[0].Direction);
            Assert.Equal(-1, Convert.ToInt32(packets[0].Fields["dirX"]));
            Assert.Equal(1, Convert.ToInt32(packets[0].Fields["dirY"]));
        }
        finally { File.Delete(log); }
    }

    [Fact]
    public void ParseLog_Timestamp_Parsed()
    {
        var log = WriteTempLog("[01:02:03.456] SEND CS_MOVE (6 bytes)\n  dirX: 0\n");
        try
        {
            var packets = CreateReplayer().ParseLog(log);
            // TimeSpan(days=0, hours=1, minutes=2, seconds=3, ms=456)
            Assert.Equal(new TimeSpan(0, 1, 2, 3, 456), packets[0].Timestamp);
        }
        finally { File.Delete(log); }
    }

    [Fact]
    public void ParseLog_SendAndRecv()
    {
        var log = WriteTempLog(
            "[00:00:01.000] SEND CS_LOGIN (68 bytes)\n" +
            "  accountId: \"test\"\n" +
            "[00:00:01.050] RECV SC_LOGIN_RESULT (77 bytes)\n" +
            "  success: 1\n" +
            "  accountUid: 42\n");
        try
        {
            var packets = CreateReplayer().ParseLog(log);

            Assert.Equal(2, packets.Count);
            Assert.Equal("SEND", packets[0].Direction);
            Assert.Equal("RECV", packets[1].Direction);
            Assert.Equal("test", packets[1].Fields["success"].ToString() == "1" ? "test" : "");
            Assert.Equal(1, Convert.ToInt32(packets[1].Fields["success"]));
        }
        finally { File.Delete(log); }
    }

    [Fact]
    public void ParseLog_SkipsRawAndConnectionLines()
    {
        var log = WriteTempLog(
            "[00:00:01.000] SEND CS_MOVE (6 bytes)\n" +
            "  172.29.160.1:1234 -> 172.29.160.1:9000\n" +
            "  dirX: 1\n" +
            "  raw: 0600010001FF\n");
        try
        {
            var packets = CreateReplayer().ParseLog(log);

            Assert.Single(packets);
            Assert.True(packets[0].Fields.ContainsKey("dirX"));
            Assert.False(packets[0].Fields.ContainsKey("raw"));
        }
        finally { File.Delete(log); }
    }

    [Fact]
    public void ParseLog_StringValue_UnquotesCorrectly()
    {
        var log = WriteTempLog(
            "[00:00:01.000] RECV SC_LOGIN_RESULT (77 bytes)\n" +
            "  message: \"Hello World\"\n");
        try
        {
            var packets = CreateReplayer().ParseLog(log);
            Assert.Equal("Hello World", packets[0].Fields["message"]);
        }
        finally { File.Delete(log); }
    }

    [Fact]
    public void ParseLog_EnumValue_ExtractsNumber()
    {
        var log = WriteTempLog(
            "[00:00:01.000] RECV SC_TEST (10 bytes)\n" +
            "  status: 3 (ACTIVE)\n");
        try
        {
            var packets = CreateReplayer().ParseLog(log);
            Assert.Equal(3, Convert.ToInt32(packets[0].Fields["status"]));
        }
        finally { File.Delete(log); }
    }

    [Fact]
    public void ParseLog_EmptyFile_ReturnsEmpty()
    {
        var log = WriteTempLog("");
        try
        {
            var packets = CreateReplayer().ParseLog(log);
            Assert.Empty(packets);
        }
        finally { File.Delete(log); }
    }

    [Fact]
    public void ParseLog_MultiplePackets_PreservesOrder()
    {
        var log = WriteTempLog(
            "[00:00:01.000] SEND CS_MOVE (6 bytes)\n" +
            "  dirX: 1\n" +
            "[00:00:02.000] SEND CS_MOVE (6 bytes)\n" +
            "  dirX: -1\n" +
            "[00:00:03.000] SEND CS_MOVE (6 bytes)\n" +
            "  dirX: 0\n");
        try
        {
            var packets = CreateReplayer().ParseLog(log);

            Assert.Equal(3, packets.Count);
            Assert.Equal(1, Convert.ToInt32(packets[0].Fields["dirX"]));
            Assert.Equal(-1, Convert.ToInt32(packets[1].Fields["dirX"]));
            Assert.Equal(0, Convert.ToInt32(packets[2].Fields["dirX"]));
        }
        finally { File.Delete(log); }
    }
}
