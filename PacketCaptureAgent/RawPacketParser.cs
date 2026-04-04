using System.Net;

namespace PacketCaptureAgent;

public record TcpPacketInfo(ConnectionKey Connection, ReadOnlyMemory<byte> Payload, byte TcpFlags = 0);

public static class RawPacketParser
{
    public const byte FlagFIN = 0x01, FlagSYN = 0x02, FlagRST = 0x04;

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

        byte tcpFlags = buffer[tcpOffset + 13];
        int tcpHeaderLen = ((buffer[tcpOffset + 12] >> 4) & 0x0F) * 4;
        int payloadOffset = ipHeaderLen + tcpHeaderLen;
        int payloadLen = length - payloadOffset;

        // SYN/FIN/RST는 페이로드 없어도 반환
        if (payloadLen <= 0 && (tcpFlags & (FlagSYN | FlagFIN | FlagRST)) == 0)
            return null;

        var connection = new ConnectionKey(srcIP, srcPort, dstIP, dstPort);
        var payload = payloadLen > 0 ? new ReadOnlyMemory<byte>(buffer, payloadOffset, payloadLen) : ReadOnlyMemory<byte>.Empty;
        return new TcpPacketInfo(connection, payload, tcpFlags);
    }
}
