using System.Buffers.Binary;

namespace PacketCaptureAgent.Tests;

/// <summary>
/// Transform 라운드트립 테스트 — Encrypt→Decrypt, Decrypt→Encrypt가 원본을 복원하는지 검증.
/// PacketBuilder→PacketParser 파이프라인 통합 테스트 포함.
/// </summary>
public class TransformTests
{
    #region XTEA Round-trip

    [Fact]
    public void Xtea_EncryptDecrypt_RoundTrip()
    {
        var options = new Dictionary<string, object> { ["key"] = "0123456789ABCDEF0123456789ABCDEF" };
        var transform = new XteaTransform(options);
        var ctx = new TransformContext();

        // 8바이트 블록 (XTEA 최소 단위)
        var original = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08 };
        var encrypted = transform.ReverseTransform(original, ctx);
        var decrypted = transform.Transform(encrypted, ctx);

        Assert.Equal(original, decrypted);
        Assert.NotEqual(original, encrypted); // 암호화되면 원본과 달라야 함
    }

    [Fact]
    public void Xtea_DecryptEncrypt_RoundTrip()
    {
        var options = new Dictionary<string, object> { ["key"] = "DEADBEEFCAFEBABE1122334455667788" };
        var transform = new XteaTransform(options);
        var ctx = new TransformContext();

        var original = new byte[] { 0xAA, 0xBB, 0xCC, 0xDD, 0x11, 0x22, 0x33, 0x44 };
        var decrypted = transform.Transform(original, ctx);
        var reEncrypted = transform.ReverseTransform(decrypted, ctx);

        Assert.Equal(original, reEncrypted);
    }

    [Fact]
    public void Xtea_MultiBlock_RoundTrip()
    {
        var options = new Dictionary<string, object> { ["key"] = "0123456789ABCDEF0123456789ABCDEF" };
        var transform = new XteaTransform(options);
        var ctx = new TransformContext();

        // 24바이트 = 3블록
        var original = new byte[24];
        for (int i = 0; i < 24; i++) original[i] = (byte)(i * 7 + 3);

        var encrypted = transform.ReverseTransform(original, ctx);
        var decrypted = transform.Transform(encrypted, ctx);

        Assert.Equal(original, decrypted);
    }

    [Fact]
    public void Xtea_NoKey_Passthrough()
    {
        var transform = new XteaTransform(null);
        var ctx = new TransformContext();
        var data = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };

        Assert.Equal(data, transform.Transform(data, ctx));
        Assert.Equal(data, transform.ReverseTransform(data, ctx));
    }

    [Fact]
    public void Xtea_ContextKey_RoundTrip()
    {
        var options = new Dictionary<string, object> { ["key_from_context"] = "xtea_key" };
        var transform = new XteaTransform(options);
        var ctx = new TransformContext();
        ctx.Set("xtea_key", new uint[] { 0x01234567, 0x89ABCDEF, 0xFEDCBA98, 0x76543210 });

        var original = new byte[] { 0x10, 0x20, 0x30, 0x40, 0x50, 0x60, 0x70, 0x80 };
        var encrypted = transform.ReverseTransform(original, ctx);
        var decrypted = transform.Transform(encrypted, ctx);

        Assert.Equal(original, decrypted);
        Assert.NotEqual(original, encrypted);
    }

    #endregion

    #region TransformFactory

    [Fact]
    public void TransformFactory_CreatesPipeline_WithDirection()
    {
        var defs = new List<TransformDefinition>
        {
            new() { Type = "xtea", Direction = "C2S", Options = new() { ["key"] = "0123456789ABCDEF0123456789ABCDEF" } },
            new() { Type = "xtea", Direction = "S2C", Options = new() { ["key"] = "AABBCCDDAABBCCDDAABBCCDDAABBCCDD" } },
        };

        var c2s = TransformFactory.CreatePipeline(defs, "C2S");
        var s2c = TransformFactory.CreatePipeline(defs, "S2C");
        var all = TransformFactory.CreatePipeline(defs);

        Assert.Single(c2s);
        Assert.Single(s2c);
        Assert.Equal(2, all.Count);
    }

    [Fact]
    public void TransformFactory_NullDirection_IncludedInAll()
    {
        var defs = new List<TransformDefinition>
        {
            new() { Type = "xtea", Options = new() { ["key"] = "0123456789ABCDEF0123456789ABCDEF" } },
            new() { Type = "xtea", Direction = "C2S", Options = new() { ["key"] = "AABBCCDDAABBCCDDAABBCCDDAABBCCDD" } },
        };

        var c2s = TransformFactory.CreatePipeline(defs, "C2S");
        Assert.Equal(2, c2s.Count); // direction=null도 포함
    }

    #endregion

    #region PacketBuilder + PacketParser 통합

    [Fact]
    public void PacketBuilder_WithTransform_Parser_RoundTrip()
    {
        var key = "0123456789ABCDEF0123456789ABCDEF";
        var protocol = CreateProtocol();
        protocol.Transforms = new List<TransformDefinition>
        {
            new() { Type = "xtea", Options = new() { ["key"] = key } }
        };

        // Builder: 패킷 빌드 → 암호화
        var encTransforms = TransformFactory.CreatePipeline(protocol.Transforms, "C2S");
        var builder = new PacketBuilder(protocol, encTransforms);
        var fields = new Dictionary<string, object> { ["value"] = 42 };
        var encrypted = builder.Build("TEST_PKT", fields);

        // Parser: 복호화 → 파싱
        var parser = new PacketParser(protocol);
        var stream = new TcpStream(new ConnectionKey(
            System.Net.IPAddress.Loopback, 1, System.Net.IPAddress.Loopback, 2));
        stream.Append(encrypted);
        var parsed = parser.TryParse(stream);

        Assert.NotNull(parsed);
        Assert.Equal("TEST_PKT", parsed.Name);
        Assert.Equal(42, Convert.ToInt32(parsed.Fields["value"]));
    }

    [Fact]
    public void PacketBuilder_NoTransform_BackwardCompatible()
    {
        var protocol = CreateProtocol();
        var builder = new PacketBuilder(protocol); // transforms 미전달
        var fields = new Dictionary<string, object> { ["value"] = 99 };
        var data = builder.Build("TEST_PKT", fields);

        // 평문 파싱 가능
        var parser = new PacketParser(protocol);
        var stream = new TcpStream(new ConnectionKey(
            System.Net.IPAddress.Loopback, 1, System.Net.IPAddress.Loopback, 2));
        stream.Append(data);
        var parsed = parser.TryParse(stream);

        Assert.NotNull(parsed);
        Assert.Equal(99, Convert.ToInt32(parsed.Fields["value"]));
    }

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
                    Name = "TEST_PKT",
                    Type = 0x0001,
                    Fields = new List<FieldDefinition>
                    {
                        new() { Name = "value", Type = "int32" }
                    }
                }
            }
        };
    }

    #endregion
}
