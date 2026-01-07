using System.Text;
using System.Text.Json;

namespace PacketCaptureAgent;

public class PacketBuilder
{
    private readonly ProtocolDefinition _protocol;
    private readonly bool _littleEndian;
    private readonly int _sizeOffset;
    private readonly string _sizeType;

    public PacketBuilder(ProtocolDefinition protocol)
    {
        _protocol = protocol;
        _littleEndian = protocol.Protocol.Endian == "little";

        var header = protocol.Protocol.Header;
        if (header.Fields != null && header.Fields.Count > 0)
        {
            var sizeField = header.Fields.FirstOrDefault(f => f.Name == header.SizeField);
            _sizeOffset = sizeField?.Offset ?? 0;
            _sizeType = sizeField?.Type ?? "int32";
        }
        else
        {
            _sizeOffset = 0;
            _sizeType = "int32";
        }
    }

    public byte[] Build(string packetName, Dictionary<string, object> fields, Dictionary<string, object>? overrides = null)
    {
        var def = _protocol.Packets.FirstOrDefault(p => p.Name == packetName)
            ?? throw new ArgumentException($"Unknown packet: {packetName}");

        var merged = new Dictionary<string, object>(fields);
        if (overrides != null)
            foreach (var kv in overrides)
                merged[kv.Key] = kv.Value;

        using var ms = new MemoryStream();
        foreach (var field in def.Fields)
            WriteField(ms, field, merged.GetValueOrDefault(field.Name), merged);

        var data = ms.ToArray();
        
        // size 필드 업데이트
        WriteSizeToBuffer(data, data.Length);
        return data;
    }

    private void WriteSizeToBuffer(byte[] data, int size)
    {
        byte[] sizeBytes = _sizeType switch
        {
            "int8" or "uint8" => new[] { (byte)size },
            "int16" or "uint16" => BitConverter.GetBytes((short)size),
            _ => BitConverter.GetBytes(size)
        };
        if (!_littleEndian && sizeBytes.Length > 1)
            Array.Reverse(sizeBytes);
        Array.Copy(sizeBytes, 0, data, _sizeOffset, sizeBytes.Length);
    }

    private void WriteField(MemoryStream ms, FieldDefinition field, object? value, Dictionary<string, object> allFields)
    {
        // 배열 타입
        if (field.Type == "array" && field.Element != null)
        {
            var list = GetList(value);
            foreach (var item in list)
            {
                var elemField = new FieldDefinition { Type = field.Element };
                WriteField(ms, elemField, item, allFields);
            }
            return;
        }

        switch (field.Type)
        {
            case "int8": ms.WriteByte((byte)Convert.ToSByte(value ?? 0)); break;
            case "uint8": ms.WriteByte(Convert.ToByte(value ?? 0)); break;
            case "int16": WriteInt16(ms, Convert.ToInt16(value ?? 0)); break;
            case "uint16": WriteUInt16(ms, Convert.ToUInt16(value ?? 0)); break;
            case "int32": WriteInt32(ms, Convert.ToInt32(value ?? 0)); break;
            case "uint32": WriteUInt32(ms, Convert.ToUInt32(value ?? 0)); break;
            case "int64": WriteInt64(ms, Convert.ToInt64(value ?? 0)); break;
            case "uint64": WriteUInt64(ms, Convert.ToUInt64(value ?? 0)); break;
            case "float": WriteFloat(ms, Convert.ToSingle(value ?? 0f)); break;
            case "double": WriteDouble(ms, Convert.ToDouble(value ?? 0d)); break;
            case "bool": ms.WriteByte((byte)(Convert.ToBoolean(value) ? 1 : 0)); break;
            case "string": WriteString(ms, value?.ToString() ?? "", field.GetLength()); break;
            case "bytes": WriteBytes(ms, GetBytes(value), field.GetLength()); break;
            default: TryWriteCustomType(ms, field.Type, value, allFields); break;
        }
    }

    private void TryWriteCustomType(MemoryStream ms, string typeName, object? value, Dictionary<string, object> allFields)
    {
        if (_protocol.Types.TryGetValue(typeName, out var typeDef))
        {
            if (typeDef.Kind == "struct" && typeDef.Fields != null)
            {
                var structFields = GetDict(value);
                foreach (var f in typeDef.Fields)
                    WriteField(ms, f, structFields.GetValueOrDefault(f.Name), structFields);
            }
            else if (typeDef.Kind == "enum" && typeDef.Base != null)
            {
                WriteField(ms, new FieldDefinition { Type = typeDef.Base }, value, allFields);
            }
        }
    }

    private List<object> GetList(object? value)
    {
        if (value is List<object> list) return list;
        if (value is JsonElement je && je.ValueKind == JsonValueKind.Array)
            return je.EnumerateArray().Select(e => (object)e).ToList();
        return new List<object>();
    }

    private Dictionary<string, object> GetDict(object? value)
    {
        if (value is Dictionary<string, object> dict) return dict;
        if (value is JsonElement je && je.ValueKind == JsonValueKind.Object)
            return je.EnumerateObject().ToDictionary(p => p.Name, p => (object)p.Value);
        return new Dictionary<string, object>();
    }

    private byte[] GetBytes(object? value)
    {
        if (value is byte[] bytes) return bytes;
        if (value is JsonElement je && je.ValueKind == JsonValueKind.Array)
            return je.EnumerateArray().Select(e => (byte)e.GetInt32()).ToArray();
        return Array.Empty<byte>();
    }

    private void WriteString(MemoryStream ms, string value, int length)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        var buffer = new byte[length];
        Array.Copy(bytes, buffer, Math.Min(bytes.Length, length - 1));
        ms.Write(buffer);
    }

    private void WriteBytes(MemoryStream ms, byte[] value, int length)
    {
        var buffer = new byte[length];
        Array.Copy(value, buffer, Math.Min(value.Length, length));
        ms.Write(buffer);
    }

    private void WriteInt16(MemoryStream ms, short value)
    {
        var bytes = BitConverter.GetBytes(value);
        if (!_littleEndian) Array.Reverse(bytes);
        ms.Write(bytes);
    }

    private void WriteUInt16(MemoryStream ms, ushort value)
    {
        var bytes = BitConverter.GetBytes(value);
        if (!_littleEndian) Array.Reverse(bytes);
        ms.Write(bytes);
    }

    private void WriteInt32(MemoryStream ms, int value)
    {
        var bytes = BitConverter.GetBytes(value);
        if (!_littleEndian) Array.Reverse(bytes);
        ms.Write(bytes);
    }

    private void WriteUInt32(MemoryStream ms, uint value)
    {
        var bytes = BitConverter.GetBytes(value);
        if (!_littleEndian) Array.Reverse(bytes);
        ms.Write(bytes);
    }

    private void WriteInt64(MemoryStream ms, long value)
    {
        var bytes = BitConverter.GetBytes(value);
        if (!_littleEndian) Array.Reverse(bytes);
        ms.Write(bytes);
    }

    private void WriteUInt64(MemoryStream ms, ulong value)
    {
        var bytes = BitConverter.GetBytes(value);
        if (!_littleEndian) Array.Reverse(bytes);
        ms.Write(bytes);
    }

    private void WriteFloat(MemoryStream ms, float value)
    {
        var bytes = BitConverter.GetBytes(value);
        if (!_littleEndian) Array.Reverse(bytes);
        ms.Write(bytes);
    }

    private void WriteDouble(MemoryStream ms, double value)
    {
        var bytes = BitConverter.GetBytes(value);
        if (!_littleEndian) Array.Reverse(bytes);
        ms.Write(bytes);
    }
}
