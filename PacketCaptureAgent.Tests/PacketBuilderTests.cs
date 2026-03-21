using System.Net;

namespace PacketCaptureAgent.Tests;

/// <summary>
/// PacketBuilder characterization tests — 현재 동작을 캡처하여 변경 시 사이드이펙트 감지.
/// </summary>
public class PacketBuilderTests
{
    private static ProtocolDefinition CreateProtocol()
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

    private static readonly ConnectionKey DummyKey = new(IPAddress.Loopback, 1000, IPAddress.Loopback, 2000);

    [Fact]
    public void Build_SimplePacket()
    {
        var builder = new PacketBuilder(CreateProtocol());
        var fields = new Dictionary<string, object> { ["dirX"] = -1, ["dirY"] = 1 };

        var data = builder.Build("CS_MOVE", fields);

        Assert.Equal(6, data.Length);
        Assert.Equal(6, BitConverter.ToUInt16(data, 0));   // length
        Assert.Equal(1, BitConverter.ToUInt16(data, 2));   // type
    }

    [Fact]
    public void Build_WithStringField()
    {
        var builder = new PacketBuilder(CreateProtocol());
        var fields = new Dictionary<string, object>
        {
            ["success"] = 1,
            ["accountUid"] = 42UL,
            ["message"] = "OK"
        };

        var data = builder.Build("SC_LOGIN_RESULT", fields);

        Assert.Equal(29, data.Length); // header(4) + uint8(1) + uint64(8) + string(16)
        Assert.Equal(29, BitConverter.ToUInt16(data, 0));
    }

    [Fact]
    public void Build_UnknownPacket_Throws()
    {
        var builder = new PacketBuilder(CreateProtocol());
        Assert.Throws<ArgumentException>(() =>
            builder.Build("NONEXISTENT", new Dictionary<string, object>()));
    }

    [Fact]
    public void Roundtrip_BuildThenParse()
    {
        var protocol = CreateProtocol();
        var builder = new PacketBuilder(protocol);
        var parser = new PacketParser(protocol);

        var fields = new Dictionary<string, object> { ["dirX"] = -1, ["dirY"] = 1 };
        var data = builder.Build("CS_MOVE", fields);

        var stream = new TcpStream(DummyKey);
        stream.Append(data);
        var parsed = parser.TryParse(stream);

        Assert.NotNull(parsed);
        Assert.Equal("CS_MOVE", parsed.Name);
        Assert.Equal((sbyte)-1, Convert.ToSByte(parsed.Fields["dirX"]));
        Assert.Equal((sbyte)1, Convert.ToSByte(parsed.Fields["dirY"]));
    }

    [Fact]
    public void Build_WithOverrides()
    {
        var builder = new PacketBuilder(CreateProtocol());
        var fields = new Dictionary<string, object> { ["dirX"] = 1, ["dirY"] = 0 };
        var overrides = new Dictionary<string, object> { ["dirX"] = -1 };

        var data = builder.Build("CS_MOVE", fields, overrides);

        // override가 적용되어 dirX=-1
        Assert.Equal((byte)0xFF, data[4]); // -1 as int8
    }

    [Fact]
    public void Build_NullHeaderFields_UsesDefaultHeader()
    {
        // Bug #3 수정 후: header.Fields가 null이면 기본 헤더(int32 size + int32 type) 사용
        var protocol = new ProtocolDefinition
        {
            Protocol = new ProtocolInfo
            {
                Name = "Test",
                Endian = "little",
                Header = new HeaderInfo { SizeField = "length", TypeField = "type" }
            },
            Packets = new List<PacketDefinition>
            {
                new() { Type = 1, Name = "TEST", Fields = new() }
            }
        };
        var builder = new PacketBuilder(protocol);

        var data = builder.Build("TEST", new Dictionary<string, object>());

        Assert.Equal(8, data.Length); // int32 size + int32 type
        Assert.Equal(8, BitConverter.ToInt32(data, 0));
        Assert.Equal(1, BitConverter.ToInt32(data, 4));
    }

    // --- 배열 빌드 ---

    [Fact]
    public void Build_ArrayOfPrimitives()
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
                        new() { Name = "count", Type = "uint8" },
                        new() { Name = "ids", Type = "array", CountField = "count", Element = "uint16" },
                    }
                }
            }
        };
        var builder = new PacketBuilder(protocol);
        var fields = new Dictionary<string, object>
        {
            ["count"] = 3,
            ["ids"] = new List<object> { 10, 20, 30 }
        };

        var data = builder.Build("TEST", fields);

        // header(4) + count(1) + 3*uint16(6) = 11
        Assert.Equal(11, data.Length);
        Assert.Equal(11, BitConverter.ToUInt16(data, 0));
        Assert.Equal(10, BitConverter.ToUInt16(data, 5));
        Assert.Equal(20, BitConverter.ToUInt16(data, 7));
        Assert.Equal(30, BitConverter.ToUInt16(data, 9));
    }

    // --- 구조체 빌드 ---

    [Fact]
    public void Build_StructField()
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
                    Type = 1, Name = "TEST",
                    Fields = new List<FieldDefinition>
                    {
                        new() { Name = "count", Type = "uint8" },
                        new() { Name = "items", Type = "array", CountField = "count", Element = "Entry" },
                    }
                }
            }
        };
        var builder = new PacketBuilder(protocol);
        var fields = new Dictionary<string, object>
        {
            ["count"] = 1,
            ["items"] = new List<object>
            {
                new Dictionary<string, object> { ["id"] = 5, ["value"] = 999 }
            }
        };

        var data = builder.Build("TEST", fields);

        // header(4) + count(1) + Entry(2+4=6) = 11
        Assert.Equal(11, data.Length);
        Assert.Equal(5, BitConverter.ToUInt16(data, 5));
        Assert.Equal(999U, BitConverter.ToUInt32(data, 7));
    }

    // --- 라운드트립 확대 ---

    [Fact]
    public void Roundtrip_StringField()
    {
        var protocol = CreateProtocol();
        var builder = new PacketBuilder(protocol);
        var parser = new PacketParser(protocol);

        var fields = new Dictionary<string, object>
        {
            ["success"] = 1,
            ["accountUid"] = 42UL,
            ["message"] = "Hello"
        };
        var data = builder.Build("SC_LOGIN_RESULT", fields);

        var stream = new TcpStream(DummyKey);
        stream.Append(data);
        var parsed = parser.TryParse(stream);

        Assert.NotNull(parsed);
        Assert.Equal("Hello", parsed.Fields["message"]);
        Assert.Equal(42UL, Convert.ToUInt64(parsed.Fields["accountUid"]));
    }

    [Fact]
    public void Roundtrip_StructArray()
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
                    Type = 1, Name = "TEST",
                    Fields = new List<FieldDefinition>
                    {
                        new() { Name = "count", Type = "uint8" },
                        new() { Name = "items", Type = "array", CountField = "count", Element = "Entry" },
                    }
                }
            }
        };
        var builder = new PacketBuilder(protocol);
        var parser = new PacketParser(protocol);

        var fields = new Dictionary<string, object>
        {
            ["count"] = 2,
            ["items"] = new List<object>
            {
                new Dictionary<string, object> { ["id"] = 1, ["value"] = 100 },
                new Dictionary<string, object> { ["id"] = 2, ["value"] = 200 },
            }
        };
        var data = builder.Build("TEST", fields);

        var stream = new TcpStream(DummyKey);
        stream.Append(data);
        var parsed = parser.TryParse(stream);

        Assert.NotNull(parsed);
        var items = Assert.IsType<List<object>>(parsed.Fields["items"]);
        Assert.Equal(2, items.Count);
        var entry1 = Assert.IsType<Dictionary<string, object>>(items[0]);
        Assert.Equal(100U, Convert.ToUInt32(entry1["value"]));
    }
}
