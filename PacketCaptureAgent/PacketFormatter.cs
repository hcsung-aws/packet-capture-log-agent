using System.Net;
using System.Text;
using System.Text.Json;

namespace PacketCaptureAgent;

public class PacketFormatter
{
    private readonly ProtocolDefinition _protocol;

    public PacketFormatter(ProtocolDefinition protocol) => _protocol = protocol;

    public (string console, string file) Format(ParsedPacket packet, ConnectionKey conn, string direction)
    {
        var console = FormatConsole(packet, conn, direction);
        var file = FormatFile(packet, conn, direction);
        return (console, file);
    }

    private string FormatConsole(ParsedPacket packet, ConnectionKey conn, string direction)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"[{DateTime.Now:HH:mm:ss.fff}] {direction} {packet.Name} ({packet.RawData.Length} bytes)");
        sb.AppendLine($"  {conn.SrcIP}:{conn.SrcPort} -> {conn.DstIP}:{conn.DstPort}");
        
        foreach (var (name, value) in packet.Fields)
        {
            if (name == "size" || name == "type") continue;
            var displayValue = FormatValue(name, value);
            sb.AppendLine($"  {name}: {displayValue}");
        }
        
        var rawHex = Convert.ToHexString(packet.RawData);
        var shortHex = rawHex.Length > 64 ? rawHex[..64] + "..." : rawHex;
        sb.Append($"  raw: {shortHex}");
        return sb.ToString();
    }

    private string FormatFile(ParsedPacket packet, ConnectionKey conn, string direction)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"[{DateTime.Now:HH:mm:ss.fff}] {direction} {packet.Name} ({packet.RawData.Length} bytes)");
        sb.AppendLine($"  {conn.SrcIP}:{conn.SrcPort} -> {conn.DstIP}:{conn.DstPort}");
        
        foreach (var (name, value) in packet.Fields)
        {
            var displayValue = FormatValue(name, value);
            sb.AppendLine($"  {name}: {displayValue}");
        }
        
        sb.Append($"  raw: {Convert.ToHexString(packet.RawData)}");
        return sb.ToString();
    }

    public string FormatJson(ParsedPacket packet, ConnectionKey conn, string direction)
    {
        var obj = new
        {
            timestamp = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss.fff"),
            direction,
            packet = packet.Name,
            src = $"{conn.SrcIP}:{conn.SrcPort}",
            dst = $"{conn.DstIP}:{conn.DstPort}",
            fields = packet.Fields,
            raw = Convert.ToHexString(packet.RawData)
        };
        return JsonSerializer.Serialize(obj);
    }

    private string FormatValue(string fieldName, object value)
    {
        if (value is string s)
            return $"\"{s}\"";
        
        if (fieldName == "type" && value is int typeVal)
        {
            var enumDef = _protocol.Types.GetValueOrDefault("PacketType");
            if (enumDef?.Values != null)
            {
                var name = enumDef.Values.FirstOrDefault(kv => kv.Value == typeVal).Key;
                if (name != null)
                    return $"{typeVal} ({name})";
            }
        }
        
        return value?.ToString() ?? "";
    }
}
