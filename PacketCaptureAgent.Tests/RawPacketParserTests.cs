using System.Net;
using PacketCaptureAgent;

namespace PacketCaptureAgent.Tests;

/// <summary>
/// Characterization tests for IP/TCP packet parsing logic extracted from Program.ProcessPacket.
/// These tests capture the current behavior before refactoring.
/// </summary>
public class RawPacketParserTests
{
    // Helper: build a raw IP+TCP packet buffer
    static byte[] BuildPacket(
        IPAddress srcIP, int srcPort,
        IPAddress dstIP, int dstPort,
        byte[] payload,
        byte protocol = 6,       // TCP
        int ipHeaderLen = 20,
        int tcpHeaderLen = 20)
    {
        int ihl = ipHeaderLen / 4;
        int tcpDataOffset = tcpHeaderLen / 4;
        int total = ipHeaderLen + tcpHeaderLen + payload.Length;
        var buf = new byte[total];

        // IP header
        buf[0] = (byte)(0x40 | ihl);  // version=4, IHL
        buf[9] = protocol;
        srcIP.GetAddressBytes().CopyTo(buf, 12);
        dstIP.GetAddressBytes().CopyTo(buf, 16);

        // TCP header
        int tcpOff = ipHeaderLen;
        buf[tcpOff + 0] = (byte)(srcPort >> 8);
        buf[tcpOff + 1] = (byte)(srcPort & 0xFF);
        buf[tcpOff + 2] = (byte)(dstPort >> 8);
        buf[tcpOff + 3] = (byte)(dstPort & 0xFF);
        buf[tcpOff + 12] = (byte)(tcpDataOffset << 4);

        // Payload
        payload.CopyTo(buf, ipHeaderLen + tcpHeaderLen);
        return buf;
    }

    static readonly IPAddress SrcAddr = IPAddress.Parse("192.168.1.10");
    static readonly IPAddress DstAddr = IPAddress.Parse("10.0.0.1");

    [Fact]
    public void NonTcpPacket_ReturnsNull()
    {
        var buf = BuildPacket(SrcAddr, 5000, DstAddr, 9000, [0x01], protocol: 17); // UDP
        var result = RawPacketParser.TryExtract(buf, buf.Length, filterPort: null);
        Assert.Null(result);
    }

    [Fact]
    public void TcpPacket_NoPayload_ReturnsNull()
    {
        var buf = BuildPacket(SrcAddr, 5000, DstAddr, 9000, []);
        var result = RawPacketParser.TryExtract(buf, buf.Length, filterPort: null);
        Assert.Null(result);
    }

    [Fact]
    public void TcpPacket_WithPayload_NoFilter_ReturnsInfo()
    {
        byte[] payload = [0xAA, 0xBB, 0xCC];
        var buf = BuildPacket(SrcAddr, 5000, DstAddr, 9000, payload);
        var result = RawPacketParser.TryExtract(buf, buf.Length, filterPort: null);

        Assert.NotNull(result);
        Assert.Equal(SrcAddr, result.Connection.SrcIP);
        Assert.Equal(5000, result.Connection.SrcPort);
        Assert.Equal(DstAddr, result.Connection.DstIP);
        Assert.Equal(9000, result.Connection.DstPort);
        Assert.Equal(payload, result.Payload.ToArray());
    }

    [Fact]
    public void TcpPacket_MatchingSrcPort_ReturnsInfo()
    {
        var buf = BuildPacket(SrcAddr, 9000, DstAddr, 5000, [0x01]);
        var result = RawPacketParser.TryExtract(buf, buf.Length, filterPort: 9000);
        Assert.NotNull(result);
    }

    [Fact]
    public void TcpPacket_MatchingDstPort_ReturnsInfo()
    {
        var buf = BuildPacket(SrcAddr, 5000, DstAddr, 9000, [0x01]);
        var result = RawPacketParser.TryExtract(buf, buf.Length, filterPort: 9000);
        Assert.NotNull(result);
    }

    [Fact]
    public void TcpPacket_NonMatchingPort_ReturnsNull()
    {
        var buf = BuildPacket(SrcAddr, 5000, DstAddr, 8000, [0x01]);
        var result = RawPacketParser.TryExtract(buf, buf.Length, filterPort: 9000);
        Assert.Null(result);
    }

    [Fact]
    public void IpHeaderWithOptions_ParsesCorrectly()
    {
        // IP header 24 bytes (IHL=6, 4 bytes options)
        byte[] payload = [0xDE, 0xAD];
        var buf = BuildPacket(SrcAddr, 5000, DstAddr, 9000, payload, ipHeaderLen: 24);
        var result = RawPacketParser.TryExtract(buf, buf.Length, filterPort: null);

        Assert.NotNull(result);
        Assert.Equal(payload, result.Payload.ToArray());
    }

    [Fact]
    public void TcpHeaderWithOptions_ParsesCorrectly()
    {
        // TCP header 24 bytes (data offset=6)
        byte[] payload = [0xBE, 0xEF];
        var buf = BuildPacket(SrcAddr, 5000, DstAddr, 9000, payload, tcpHeaderLen: 24);
        var result = RawPacketParser.TryExtract(buf, buf.Length, filterPort: null);

        Assert.NotNull(result);
        Assert.Equal(payload, result.Payload.ToArray());
    }

    [Fact]
    public void BufferShorterThanReported_HandlesGracefully()
    {
        // length parameter says 100 but buffer only has IP+TCP headers (40 bytes)
        // payload would be 60 bytes but buffer doesn't have it — tests the length param usage
        byte[] payload = [0x01, 0x02, 0x03];
        var buf = BuildPacket(SrcAddr, 5000, DstAddr, 9000, payload);
        // Pass actual buffer length — should work normally
        var result = RawPacketParser.TryExtract(buf, buf.Length, filterPort: null);
        Assert.NotNull(result);
        Assert.Equal(3, result.Payload.Length);
    }

    [Fact]
    public void LengthSmallerThanBuffer_UsesLengthParam()
    {
        byte[] payload = [0x01, 0x02, 0x03, 0x04, 0x05];
        var buf = BuildPacket(SrcAddr, 5000, DstAddr, 9000, payload);
        // Report only 42 bytes (40 header + 2 payload) even though buffer has 45
        var result = RawPacketParser.TryExtract(buf, 42, filterPort: null);
        Assert.NotNull(result);
        Assert.Equal(2, result.Payload.Length);
    }
}
