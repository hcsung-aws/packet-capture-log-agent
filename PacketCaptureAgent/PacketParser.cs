using System.Text;

namespace PacketCaptureAgent;

public class ParsedPacket
{
    public string Name { get; set; } = "";
    public int Type { get; set; }
    public Dictionary<string, object> Fields { get; } = new();
    public byte[] RawData { get; set; } = Array.Empty<byte>();
}

public class PacketParser
{
    private readonly ProtocolDefinition _protocol;
    private readonly bool _littleEndian;
    private readonly int _headerSize;
    private readonly int _sizeOffset;
    private readonly int _typeOffset;
    private readonly string _sizeType;
    private readonly string _typeType;
    private readonly List<IPacketTransform> _transforms;
    private readonly TransformContext _transformContext = new();

    public PacketParser(ProtocolDefinition protocol)
    {
        _protocol = protocol;
        _littleEndian = protocol.Protocol.Endian == "little";
        _transforms = TransformFactory.CreatePipeline(protocol.Transforms);

        var header = protocol.Protocol.Header;
        if (header.Fields != null && header.Fields.Count > 0)
        {
            // 동적 헤더
            var sizeField = header.Fields.FirstOrDefault(f => f.Name == header.SizeField);
            var typeField = header.Fields.FirstOrDefault(f => f.Name == header.TypeField);
            
            _sizeOffset = sizeField?.Offset ?? 0;
            _sizeType = sizeField?.Type ?? "int32";
            _typeOffset = typeField?.Offset ?? 4;
            _typeType = typeField?.Type ?? "int32";
            
            _headerSize = header.Fields.Max(f => f.Offset + GetTypeSize(f.Type));
        }
        else
        {
            // 기본 헤더: int32 size + int32 type
            _sizeOffset = 0;
            _sizeType = "int32";
            _typeOffset = 4;
            _typeType = "int32";
            _headerSize = 8;
        }
    }

    public ParsedPacket? TryParse(TcpStream stream)
    {
        if (stream.Available < _headerSize) return null;

        Span<byte> header = stackalloc byte[_headerSize];
        if (!stream.TryPeek(header)) return null;

        int packetSize = ReadIntByType(header, _sizeOffset, _sizeType);
        if (packetSize <= 0 || packetSize > 65535) return null;
        if (stream.Available < packetSize) return null;

        var data = new byte[packetSize];
        if (!stream.TryRead(data)) return null;

        // Transform 파이프라인 적용 (복호화 등)
        foreach (var transform in _transforms)
            data = transform.Transform(data, _transformContext);

        int packetType = ReadIntByType(data, _typeOffset, _typeType);
        var def = _protocol.GetPacketByType(packetType);

        var packet = new ParsedPacket
        {
            Name = def?.Name ?? $"Unknown({packetType})",
            Type = packetType,
            RawData = data
        };

        if (def != null)
            ParseFields(data, def.Fields, packet.Fields);

        return packet;
    }

    private void ParseFields(byte[] data, List<FieldDefinition> fields, Dictionary<string, object> result)
    {
        int offset = 0;
        foreach (var field in fields)
        {
            int remaining = data.Length - offset;
            var (value, size) = ReadField(data, offset, field, result, remaining);
            result[field.Name] = value;
            offset += size;
        }
    }

    private (object value, int size) ReadField(byte[] data, int offset, FieldDefinition field, 
        Dictionary<string, object> parsedFields, int remaining)
    {
        if (offset >= data.Length) return ("", 0);

        // 배열 타입
        if (field.Type == "array" && field.Element != null)
        {
            int count = 0;
            if (field.CountField != null && parsedFields.TryGetValue(field.CountField, out var countVal))
                count = Convert.ToInt32(countVal);

            var elementSize = GetTypeSize(field.Element);
            var list = new List<object>();
            int arrayOffset = offset;
            
            for (int i = 0; i < count && arrayOffset + elementSize <= data.Length; i++)
            {
                var elemField = new FieldDefinition { Type = field.Element };
                var (val, sz) = ReadField(data, arrayOffset, elemField, parsedFields, 0);
                list.Add(val);
                arrayOffset += sz;
            }
            return (list, arrayOffset - offset);
        }

        return field.Type switch
        {
            "int8" => ((sbyte)data[offset], 1),
            "uint8" => (data[offset], 1),
            "int16" => (ReadInt16(data, offset), 2),
            "uint16" => (ReadUInt16(data, offset), 2),
            "int32" => (ReadInt32(data, offset), 4),
            "uint32" => (ReadUInt32(data, offset), 4),
            "int64" => (ReadInt64(data, offset), 8),
            "uint64" => (ReadUInt64(data, offset), 8),
            "float" => (ReadFloat(data, offset), 4),
            "double" => (ReadDouble(data, offset), 8),
            "bool" => (data[offset] != 0, 1),
            "string" => ReadString(data, offset, field.GetLength(remaining)),
            "bytes" => ReadBytes(data, offset, field.GetLength(remaining)),
            _ => TryReadCustomType(data, offset, field.Type, parsedFields, remaining)
        };
    }

    private (object, int) TryReadCustomType(byte[] data, int offset, string typeName,
        Dictionary<string, object> parsedFields, int remaining)
    {
        if (_protocol.Types.TryGetValue(typeName, out var typeDef))
        {
            if (typeDef.Kind == "struct" && typeDef.Fields != null)
            {
                var structFields = new Dictionary<string, object>();
                int structOffset = offset;
                foreach (var f in typeDef.Fields)
                {
                    var (val, sz) = ReadField(data, structOffset, f, structFields, remaining - (structOffset - offset));
                    structFields[f.Name] = val;
                    structOffset += sz;
                }
                return (structFields, structOffset - offset);
            }
            if (typeDef.Kind == "enum" && typeDef.Base != null)
            {
                return ReadField(data, offset, new FieldDefinition { Type = typeDef.Base }, parsedFields, remaining);
            }
        }
        return ("", 0);
    }

    private int ReadIntByType(ReadOnlySpan<byte> data, int offset, string type) => type switch
    {
        "int8" => (sbyte)data[offset],
        "uint8" => data[offset],
        "int16" => ReadInt16(data, offset),
        "uint16" => ReadUInt16(data, offset),
        "int32" => ReadInt32(data, offset),
        "uint32" => (int)ReadUInt32(data, offset),
        _ => ReadInt32(data, offset)
    };

    private int GetTypeSize(string type) => type switch
    {
        "int8" or "uint8" or "bool" => 1,
        "int16" or "uint16" => 2,
        "int32" or "uint32" or "float" => 4,
        "int64" or "uint64" or "double" => 8,
        _ => 4
    };

    private (string, int) ReadString(byte[] data, int offset, int length)
    {
        if (length == 0 || offset + length > data.Length)
            length = data.Length - offset;
        if (length <= 0) return ("", 0);
        
        int end = Array.IndexOf(data, (byte)0, offset, length);
        int strLen = end >= 0 ? end - offset : length;
        return (Encoding.UTF8.GetString(data, offset, strLen), length);
    }

    private (byte[], int) ReadBytes(byte[] data, int offset, int length)
    {
        if (length == 0 || offset + length > data.Length)
            length = data.Length - offset;
        if (length <= 0) return (Array.Empty<byte>(), 0);
        
        var result = new byte[length];
        Array.Copy(data, offset, result, 0, length);
        return (result, length);
    }

    private short ReadInt16(ReadOnlySpan<byte> data, int offset) =>
        _littleEndian ? BitConverter.ToInt16(data.Slice(offset)) 
                      : System.Buffers.Binary.BinaryPrimitives.ReadInt16BigEndian(data.Slice(offset));

    private ushort ReadUInt16(ReadOnlySpan<byte> data, int offset) =>
        _littleEndian ? BitConverter.ToUInt16(data.Slice(offset))
                      : System.Buffers.Binary.BinaryPrimitives.ReadUInt16BigEndian(data.Slice(offset));

    private int ReadInt32(ReadOnlySpan<byte> data, int offset) =>
        _littleEndian ? BitConverter.ToInt32(data.Slice(offset))
                      : System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(data.Slice(offset));

    private uint ReadUInt32(ReadOnlySpan<byte> data, int offset) =>
        _littleEndian ? BitConverter.ToUInt32(data.Slice(offset))
                      : System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(data.Slice(offset));

    private long ReadInt64(ReadOnlySpan<byte> data, int offset) =>
        _littleEndian ? BitConverter.ToInt64(data.Slice(offset))
                      : System.Buffers.Binary.BinaryPrimitives.ReadInt64BigEndian(data.Slice(offset));

    private ulong ReadUInt64(ReadOnlySpan<byte> data, int offset) =>
        _littleEndian ? BitConverter.ToUInt64(data.Slice(offset))
                      : System.Buffers.Binary.BinaryPrimitives.ReadUInt64BigEndian(data.Slice(offset));

    private float ReadFloat(ReadOnlySpan<byte> data, int offset) =>
        _littleEndian ? BitConverter.ToSingle(data.Slice(offset))
                      : System.Buffers.Binary.BinaryPrimitives.ReadSingleBigEndian(data.Slice(offset));

    private double ReadDouble(ReadOnlySpan<byte> data, int offset) =>
        _littleEndian ? BitConverter.ToDouble(data.Slice(offset))
                      : System.Buffers.Binary.BinaryPrimitives.ReadDoubleBigEndian(data.Slice(offset));
}
