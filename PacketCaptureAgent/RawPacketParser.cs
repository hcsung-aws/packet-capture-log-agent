using System.Net;

namespace PacketCaptureAgent;

public record TcpPacketInfo(ConnectionKey Connection, ReadOnlyMemory<byte> Payload);

public static class RawPacketParser
{
    public static TcpPacketInfo? TryExtract(byte[] buffer, int length, int? filterPort)
    {
        int ipHeaderLen = (buffer[0] & 0x0F) * 4;
        int protocol = buffer[9];

        if (protocol != 6) return null;

        var srcIP = new IPAddress(new ReadOnlySpan<byte>(buffer, 12, 4));
        var dstIP = new IPAddress(new ReadOnlySpan<byte>(buffer, 16, 4));

        int tcpOffset = ipHeaderLen;
        int srcPort = (buffer[tcpOffset] << 8) | buffer[tcpOffset + 1];
        int dstPort = (buffer[tcpOffset + 2] << 8) | buffer[tcpOffset + 3];

        if (filterPort.HasValue && srcPort != filterPort && dstPort != filterPort)
            return null;

        int tcpHeaderLen = ((buffer[tcpOffset + 12] >> 4) & 0x0F) * 4;
        int payloadOffset = ipHeaderLen + tcpHeaderLen;
        int payloadLen = length - payloadOffset;

        if (payloadLen <= 0) return null;

        var connection = new ConnectionKey(srcIP, srcPort, dstIP, dstPort);
        var payload = new ReadOnlyMemory<byte>(buffer, payloadOffset, payloadLen);
        return new TcpPacketInfo(connection, payload);
    }
}
