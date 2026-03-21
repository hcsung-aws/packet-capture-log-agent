using System.Net;

namespace PacketCaptureAgent.Tests;

/// <summary>
/// PacketParser characterization tests — 현재 동작을 캡처하여 변경 시 사이드이펙트 감지.
/// </summary>
public class PacketParserTests
{
    // mmorpg_simulator 프로토콜 구조: uint16 length + uint16 type (little-endian)
    private static ProtocolDefinition CreateSimpleProtocol()
    {
        return new ProtocolDefinition
        {
            Protocol = new ProtocolInfo
            {
                Name = "Test",
                Endian = "little",
                Header = new HeaderInfo
                {
                    Size = 4,
                    SizeField = "length",
                    TypeField = "type",
                    Fields = new List<HeaderFieldInfo>
                    {
                        new() { Name = "length", Type = "uint16", Offset = 0 },
                        new() { Name = "type", Type = "uint16", Offset = 2 },
                    }
                }
            },
            Packets = new List<PacketDefinition>
            {
                new()
                {
                    Type = 1, Name = "CS_MOVE",
                    Fields = new List<FieldDefinition>
                    {
                        new() { Name = "dirX", Type = "int8" },
                        new() { Name = "dirY", Type = "int8" },
                    }
                },
                new()
                {
                    Type = 2, Name = "SC_LOGIN_RESULT",
                    Fields = new List<FieldDefinition>
                    {
                        new() { Name = "success", Type = "uint8" },
                        new() { Name = "accountUid", Type = "uint64" },
                        new() { Name = "message", Type = "string", Length = 16 },
                    }
                },
            }
        };
    }

    private static ProtocolDefinition CreateProtocolWithStructArray()
    {
        return new ProtocolDefinition
        {
            Protocol = new ProtocolInfo
            {
                Name = "Test",
                Endian = "little",
                Header = new HeaderInfo
                {
                    Size = 4,
                    SizeField = "length",
                    TypeField = "type",
                    Fields = new List<HeaderFieldInfo>
                    {
                        new() { Name = "length", Type = "uint16", Offset = 0 },
                        new() { Name = "type", Type = "uint16", Offset = 2 },
                    }
                }
            },
            Types = new Dictionary<string, TypeDefinition>
            {
                ["Entry"] = new()
                {
                    Kind = "struct",
                    Fields = new List<FieldDefinition>
                    {
                        new() { Name = "id", Type = "uint16" },
                        new() { Name = "value", Type = "uint32" },
                    }
                }
            },
            Packets = new List<PacketDefinition>
            {
                new()
                {
                    Type = 10, Name = "SC_LIST",
                    Fields = new List<FieldDefinition>
                    {
                        new() { Name = "count", Type = "uint8" },
                        new() { Name = "items", Type = "array", CountField = "count", Element = "Entry" },
                    }
                }
            }
        };
    }

    private static readonly ConnectionKey DummyKey = new(IPAddress.Loopback, 1000, IPAddress.Loopback, 2000);

    private static TcpStream FeedData(byte[] data)
    {
        var stream = new TcpStream(DummyKey);
        stream.Append(data);
        return stream;
    }

    // --- 기본 타입 파싱 ---

    [Fact]
    public void Parse_SimplePacket_Int8Fields()
    {
        var parser = new PacketParser(CreateSimpleProtocol());
        // CS_MOVE: length=6, type=1, dirX=-1, dirY=1
        var data = new byte[] { 6, 0, 1, 0, 0xFF, 0x01 };
        var stream = FeedData(data);

        var result = parser.TryParse(stream);

        Assert.NotNull(result);
        Assert.Equal("CS_MOVE", result.Name);
        Assert.Equal((sbyte)-1, Convert.ToSByte(result.Fields["dirX"]));
        Assert.Equal((sbyte)1, Convert.ToSByte(result.Fields["dirY"]));
    }

    [Fact]
    public void Parse_StringAndUint64Fields()
    {
        var parser = new PacketParser(CreateSimpleProtocol());
        // SC_LOGIN_RESULT: length=29, type=2, success=1, accountUid=42, message="OK" (16 bytes padded)
        var data = new byte[29];
        BitConverter.GetBytes((ushort)29).CopyTo(data, 0);  // length
        BitConverter.GetBytes((ushort)2).CopyTo(data, 2);   // type
        data[4] = 1;                                          // success
        BitConverter.GetBytes((ulong)42).CopyTo(data, 5);   // accountUid
        data[13] = (byte)'O'; data[14] = (byte)'K';         // message (null-terminated in 16 bytes)

        var stream = FeedData(data);
        var result = parser.TryParse(stream);

        Assert.NotNull(result);
        Assert.Equal("SC_LOGIN_RESULT", result.Name);
        Assert.Equal((byte)1, Convert.ToByte(result.Fields["success"]));
        Assert.Equal(42UL, Convert.ToUInt64(result.Fields["accountUid"]));
        Assert.Equal("OK", result.Fields["message"]);
    }

    [Fact]
    public void Parse_InsufficientData_ReturnsNull()
    {
        var parser = new PacketParser(CreateSimpleProtocol());
        // 헤더만 있고 페이로드 부족: length=10이지만 실제 4바이트만
        var data = new byte[] { 10, 0, 1, 0 };
        var stream = FeedData(data);

        Assert.Null(parser.TryParse(stream));
        Assert.Equal(4, stream.Available); // 데이터 소비 안 됨
    }

    [Fact]
    public void Parse_UnknownType_ReturnsUnknownName()
    {
        var parser = new PacketParser(CreateSimpleProtocol());
        // type=999 (미정의)
        var data = new byte[] { 4, 0, 0xE7, 0x03 };
        var stream = FeedData(data);

        var result = parser.TryParse(stream);

        Assert.NotNull(result);
        Assert.StartsWith("Unknown", result.Name);
    }

    [Fact]
    public void Parse_MultiplePackets_Sequential()
    {
        var parser = new PacketParser(CreateSimpleProtocol());
        // 2개 CS_MOVE 패킷 연속
        var data = new byte[] {
            6, 0, 1, 0, 1, 0,    // dirX=1, dirY=0
            6, 0, 1, 0, 0, 1,    // dirX=0, dirY=1
        };
        var stream = FeedData(data);

        var p1 = parser.TryParse(stream);
        var p2 = parser.TryParse(stream);

        Assert.NotNull(p1);
        Assert.NotNull(p2);
        Assert.Equal((sbyte)1, Convert.ToSByte(p1.Fields["dirX"]));
        Assert.Equal((sbyte)1, Convert.ToSByte(p2.Fields["dirY"]));
        Assert.Null(parser.TryParse(stream)); // 더 이상 없음
    }

    // --- 배열 + 구조체 파싱 ---

    [Fact]
    public void Parse_StructArray_CorrectData()
    {
        var parser = new PacketParser(CreateProtocolWithStructArray());
        // SC_LIST: length=17, type=10, count=2, items=[{id=1,value=100},{id=2,value=200}]
        // Entry: uint16(2) + uint32(4) = 6 bytes each
        var data = new byte[17];
        BitConverter.GetBytes((ushort)17).CopyTo(data, 0);
        BitConverter.GetBytes((ushort)10).CopyTo(data, 2);
        data[4] = 2; // count
        BitConverter.GetBytes((ushort)1).CopyTo(data, 5);
        BitConverter.GetBytes((uint)100).CopyTo(data, 7);
        BitConverter.GetBytes((ushort)2).CopyTo(data, 11);
        BitConverter.GetBytes((uint)200).CopyTo(data, 13);

        var stream = FeedData(data);
        var result = parser.TryParse(stream);

        Assert.NotNull(result);
        Assert.Equal("SC_LIST", result.Name);
        Assert.Equal((byte)2, Convert.ToByte(result.Fields["count"]));
        var items = Assert.IsType<List<object>>(result.Fields["items"]);
        Assert.Equal(2, items.Count);
    }

    [Fact]
    public void Parse_StructArray_InsufficientData_StopsEarly()
    {
        // count=2이지만 데이터가 1개분 + 2바이트만 → 정확한 bounds 체크로 1개만 파싱
        var parser = new PacketParser(CreateProtocolWithStructArray());
        var data = new byte[13]; // header(4) + count(1) + entry1(6) + partial(2)
        BitConverter.GetBytes((ushort)13).CopyTo(data, 0);
        BitConverter.GetBytes((ushort)10).CopyTo(data, 2);
        data[4] = 2;
        BitConverter.GetBytes((ushort)1).CopyTo(data, 5);
        BitConverter.GetBytes((uint)100).CopyTo(data, 7);
        BitConverter.GetBytes((ushort)2).CopyTo(data, 11);

        var stream = FeedData(data);
        var result = parser.TryParse(stream);

        Assert.NotNull(result);
        var items = Assert.IsType<List<object>>(result.Fields["items"]);
        Assert.Single(items);
    }

    [Fact]
    public void Parse_StructArray_BoundsCheckCorrectlyStops()
    {
        // Bug #2 수정 후: GetTypeSize("Entry")=6(실제 크기)이므로,
        // 4바이트만 있으면 bounds 체크에서 정확히 중단 (예외 없음)
        var parser = new PacketParser(CreateProtocolWithStructArray());
        var data = new byte[15]; // header(4) + count(1) + entry1(6) + 4바이트(< Entry 6바이트)
        BitConverter.GetBytes((ushort)15).CopyTo(data, 0);
        BitConverter.GetBytes((ushort)10).CopyTo(data, 2);
        data[4] = 2;
        BitConverter.GetBytes((ushort)1).CopyTo(data, 5);
        BitConverter.GetBytes((uint)100).CopyTo(data, 7);
        BitConverter.GetBytes((ushort)2).CopyTo(data, 11);
        data[13] = 0xFF; data[14] = 0xFF;

        var stream = FeedData(data);
        var result = parser.TryParse(stream);

        // 수정 후: 11+6=17 > 15 → bounds 체크에서 안전하게 중단
        Assert.NotNull(result);
        var items = Assert.IsType<List<object>>(result.Fields["items"]);
        Assert.Single(items);
    }

    // --- big-endian ---

    private static ProtocolDefinition CreateBigEndianProtocol()
    {
        return new ProtocolDefinition
        {
            Protocol = new ProtocolInfo
            {
                Name = "Test", Endian = "big",
                Header = new HeaderInfo
                {
                    Size = 4, SizeField = "length", TypeField = "type",
                    Fields = new List<HeaderFieldInfo>
                    {
                        new() { Name = "length", Type = "uint16", Offset = 0 },
                        new() { Name = "type", Type = "uint16", Offset = 2 },
                    }
                }
            },
            Packets = new List<PacketDefinition>
            {
                new()
                {
                    Type = 1, Name = "TEST",
                    Fields = new List<FieldDefinition>
                    {
                        new() { Name = "value", Type = "uint32" },
                    }
                }
            }
        };
    }

    [Fact]
    public void Parse_BigEndian_Uint32()
    {
        var parser = new PacketParser(CreateBigEndianProtocol());
        var data = new byte[] { 0x00, 0x08, 0x00, 0x01, 0x00, 0x00, 0x01, 0x00 };
        var stream = FeedData(data);

        var result = parser.TryParse(stream);

        Assert.NotNull(result);
        Assert.Equal(256U, Convert.ToUInt32(result.Fields["value"]));
    }

    // --- float / double ---

    [Fact]
    public void Parse_FloatAndDouble()
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
            },
            Packets = new List<PacketDefinition>
            {
                new()
                {
                    Type = 1, Name = "TEST",
                    Fields = new List<FieldDefinition>
                    {
                        new() { Name = "f", Type = "float" },
                        new() { Name = "d", Type = "double" },
                    }
                }
            }
        };
        var parser = new PacketParser(protocol);
        var data = new byte[16]; // header(4) + float(4) + double(8)
        BitConverter.GetBytes((ushort)16).CopyTo(data, 0);
        BitConverter.GetBytes((ushort)1).CopyTo(data, 2);
        BitConverter.GetBytes(3.14f).CopyTo(data, 4);
        BitConverter.GetBytes(2.718281828).CopyTo(data, 8);

        var stream = FeedData(data);
        var result = parser.TryParse(stream);

        Assert.NotNull(result);
        Assert.Equal(3.14f, Convert.ToSingle(result.Fields["f"]), 0.001f);
        Assert.Equal(2.718281828, Convert.ToDouble(result.Fields["d"]), 0.000001);
    }

    // --- enum ---

    [Fact]
    public void Parse_EnumField_ReturnsBaseValue()
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
            },
            Types = new Dictionary<string, TypeDefinition>
            {
                ["Status"] = new() { Kind = "enum", Base = "uint8" }
            },
            Packets = new List<PacketDefinition>
            {
                new()
                {
                    Type = 1, Name = "TEST",
                    Fields = new List<FieldDefinition> { new() { Name = "status", Type = "Status" } }
                }
            }
        };
        var parser = new PacketParser(protocol);
        var data = new byte[] { 5, 0, 1, 0, 2 };
        var stream = FeedData(data);

        var result = parser.TryParse(stream);

        Assert.NotNull(result);
        Assert.Equal((byte)2, Convert.ToByte(result.Fields["status"]));
    }

    // --- remaining length string ---

    [Fact]
    public void Parse_RemainingLengthString()
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
            },
            Packets = new List<PacketDefinition>
            {
                new()
                {
                    Type = 1, Name = "TEST",
                    Fields = new List<FieldDefinition>
                    {
                        new() { Name = "id", Type = "uint8" },
                        new() { Name = "msg", Type = "string", Length = "remaining" },
                    }
                }
            }
        };
        var parser = new PacketParser(protocol);
        var data = new byte[] { 7, 0, 1, 0, 42, (byte)'H', (byte)'i' };
        var stream = FeedData(data);

        var result = parser.TryParse(stream);

        Assert.NotNull(result);
        Assert.Equal((byte)42, Convert.ToByte(result.Fields["id"]));
        Assert.Equal("Hi", result.Fields["msg"]);
    }
}
