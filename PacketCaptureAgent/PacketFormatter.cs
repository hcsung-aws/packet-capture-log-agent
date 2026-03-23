using System.Net;
using System.Text;

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
            AppendField(sb, name, name, value);
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
            AppendField(sb, name, name, value);
        
        sb.Append($"  raw: {Convert.ToHexString(packet.RawData)}");
        return sb.ToString();
    }

    private void AppendField(StringBuilder sb, string key, string fieldName, object value)
    {
        if (value is List<object> list)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] is Dictionary<string, object> structFields)
                    foreach (var (fn, fv) in structFields)
                        AppendField(sb, $"{key}[{i}].{fn}", fn, fv);
                else
                    sb.AppendLine($"  {key}[{i}]: {FormatValue(fieldName, list[i])}");
            }
        }
        else if (value is Dictionary<string, object> fields)
        {
            foreach (var (fn, fv) in fields)
                AppendField(sb, $"{key}.{fn}", fn, fv);
        }
        else
        {
            sb.AppendLine($"  {key}: {FormatValue(fieldName, value)}");
        }
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
