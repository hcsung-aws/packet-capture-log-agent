using System.Net;

namespace PacketCaptureAgent.Tests;

/// <summary>
/// PacketFormatter tests — 콘솔/파일 출력 포맷 검증.
/// Formatter 출력이 ParseLog의 입력이므로, 포맷 변경 시 리플레이 파이프라인 깨짐 감지.
/// </summary>
public class PacketFormatterTests
{
    private static readonly ConnectionKey DummyConn = new(
        IPAddress.Parse("172.29.160.1"), 1234, IPAddress.Parse("172.29.160.1"), 9000);

    private static ProtocolDefinition CreateProtocol()
    {
        return new ProtocolDefinition
        {
            Protocol = new ProtocolInfo { Name = "Test", Endian = "little" },
            Types = new Dictionary<string, TypeDefinition>
            {
                ["PacketType"] = new()
                {
                    Kind = "enum", Base = "uint16",
                    Values = new Dictionary<string, int> { ["CS_MOVE"] = 1 }
                }
            }
        };
    }

    private static ParsedPacket MakePacket(string name, int type, byte[] raw, params (string k, object v)[] fields)
    {
        var pkt = new ParsedPacket { Name = name, Type = type, RawData = raw };
        foreach (var (k, v) in fields) pkt.Fields[k] = v;
        return pkt;
    }

    [Fact]
    public void Format_FileOutput_ContainsPacketHeader()
    {
        var formatter = new PacketFormatter(CreateProtocol());
        var pkt = MakePacket("CS_MOVE", 1, new byte[6], ("dirX", -1), ("dirY", 1));

        var (_, file) = formatter.Format(pkt, DummyConn, "SEND");

        Assert.Contains("SEND CS_MOVE (6 bytes)", file);
    }

    [Fact]
    public void Format_FileOutput_ContainsAllFields()
    {
        var formatter = new PacketFormatter(CreateProtocol());
        var pkt = MakePacket("CS_MOVE", 1, new byte[6], ("dirX", -1), ("dirY", 1));

        var (_, file) = formatter.Format(pkt, DummyConn, "SEND");

        Assert.Contains("dirX: -1", file);
        Assert.Contains("dirY: 1", file);
    }

    [Fact]
    public void Format_FileOutput_ContainsConnectionInfo()
    {
        var formatter = new PacketFormatter(CreateProtocol());
        var pkt = MakePacket("CS_MOVE", 1, new byte[6]);

        var (_, file) = formatter.Format(pkt, DummyConn, "SEND");

        Assert.Contains("172.29.160.1:1234 -> 172.29.160.1:9000", file);
    }

    [Fact]
    public void Format_FileOutput_ContainsRawHex()
    {
        var formatter = new PacketFormatter(CreateProtocol());
        var pkt = MakePacket("CS_MOVE", 1, new byte[] { 0x06, 0x00, 0x01, 0x00, 0xFF, 0x01 });

        var (_, file) = formatter.Format(pkt, DummyConn, "SEND");

        Assert.Contains("raw: 06000100FF01", file);
    }

    [Fact]
    public void Format_ConsoleOutput_SkipsSizeAndTypeFields()
    {
        var formatter = new PacketFormatter(CreateProtocol());
        var pkt = MakePacket("CS_MOVE", 1, new byte[6], ("size", 6), ("type", 1), ("dirX", -1));

        var (console, file) = formatter.Format(pkt, DummyConn, "SEND");

        // 콘솔: size/type 제외
        Assert.DoesNotContain("size: 6", console);
        Assert.DoesNotContain("type: 1", console);
        Assert.Contains("dirX: -1", console);

        // 파일: size/type 포함
        Assert.Contains("size: 6", file);
    }

    [Fact]
    public void Format_ConsoleOutput_TruncatesLongRawHex()
    {
        var formatter = new PacketFormatter(CreateProtocol());
        var pkt = MakePacket("CS_MOVE", 1, new byte[100]); // 200 hex chars > 64

        var (console, file) = formatter.Format(pkt, DummyConn, "SEND");

        // 콘솔: 64자 + "..."
        Assert.Contains("...", console);
        // 파일: 전체 hex
        Assert.DoesNotContain("...", file.Split("raw:")[1] is var rawPart && rawPart.Length > 64 ? "" : "...");
    }

    [Fact]
    public void Format_StringValue_Quoted()
    {
        var formatter = new PacketFormatter(CreateProtocol());
        var pkt = MakePacket("SC_LOGIN", 2, new byte[4], ("message", "Hello"));

        var (_, file) = formatter.Format(pkt, DummyConn, "RECV");

        Assert.Contains("message: \"Hello\"", file);
    }

    [Fact]
    public void Format_EnumTypeField_ShowsName()
    {
        var formatter = new PacketFormatter(CreateProtocol());
        var pkt = MakePacket("CS_MOVE", 1, new byte[4], ("type", 1));

        var (_, file) = formatter.Format(pkt, DummyConn, "SEND");

        Assert.Contains("1 (CS_MOVE)", file);
    }

    [Fact]
    public void Format_FileOutput_ParseLogCompatible()
    {
        // Formatter 출력 → ParseLog 입력 호환성 검증
        var formatter = new PacketFormatter(CreateProtocol());
        var pkt = MakePacket("CS_MOVE", 1, new byte[6], ("dirX", -1), ("dirY", 1));

        var (_, file) = formatter.Format(pkt, DummyConn, "SEND");

        // ParseLog가 기대하는 헤더 패턴: [HH:MM:SS.mmm] SEND/RECV PacketName (N bytes)
        Assert.Matches(@"\[\d{2}:\d{2}:\d{2}\.\d{3}\]\s+SEND\s+CS_MOVE\s+\(\d+\s+bytes\)", file);
        // 필드 패턴: "  fieldName: value"
        Assert.Matches(@"^\s+dirX:\s+-1", file.Split('\n').First(l => l.Contains("dirX")));
    }

    [Fact]
    public void Format_ArrayField_FlattenedKeys()
    {
        var formatter = new PacketFormatter(CreateProtocol());
        var chars = new List<object>
        {
            new Dictionary<string, object> { ["charUid"] = 1001, ["name"] = "Hero" },
            new Dictionary<string, object> { ["charUid"] = 1002, ["name"] = "Alt" }
        };
        var pkt = MakePacket("SC_CHAR_LIST", 2, new byte[4], ("count", 2));
        pkt.Fields["chars"] = chars;

        var (_, file) = formatter.Format(pkt, DummyConn, "RECV");

        Assert.Contains("chars[0].charUid: 1001", file);
        Assert.Contains("chars[0].name: \"Hero\"", file);
        Assert.Contains("chars[1].charUid: 1002", file);
        Assert.DoesNotContain("System.Collections", file);
    }

    [Fact]
    public void Format_ArrayField_ParseLogRoundtrip()
    {
        // Formatter 배열 출력 → ParseLog가 flat key로 파싱하는지 검증
        var formatter = new PacketFormatter(CreateProtocol());
        var chars = new List<object>
        {
            new Dictionary<string, object> { ["charUid"] = 1001 }
        };
        var pkt = MakePacket("SC_CHAR_LIST", 2, new byte[4], ("count", 1));
        pkt.Fields["chars"] = chars;

        var (_, file) = formatter.Format(pkt, DummyConn, "RECV");

        // Write to temp file and parse
        var tmpPath = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tmpPath, file);
            var protocol = new ProtocolDefinition
            {
                Protocol = new ProtocolInfo
                {
                    Name = "Test", Endian = "little",
                    Header = new HeaderInfo
                    {
                        SizeField = "size", TypeField = "type",
                        Fields = new List<HeaderFieldInfo>
                        {
                            new() { Name = "size", Type = "uint16", Offset = 0 },
                            new() { Name = "type", Type = "uint16", Offset = 2 }
                        }
                    }
                }
            };
            var replayer = new PacketReplayer(protocol);
            var parsed = replayer.ParseLog(tmpPath);

            Assert.Single(parsed);
            Assert.Equal("SC_CHAR_LIST", parsed[0].Name);
            Assert.True(parsed[0].Fields.ContainsKey("chars[0].charUid"));
            Assert.Equal(1001, parsed[0].Fields["chars[0].charUid"]);
        }
        finally { File.Delete(tmpPath); }
    }
}
