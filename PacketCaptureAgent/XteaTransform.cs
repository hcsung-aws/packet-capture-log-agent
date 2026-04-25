using System.Buffers.Binary;

namespace PacketCaptureAgent;

/// <summary>
/// XTEA 암호화/복호화 (Tibia/Forgotten Server 프로토콜)
/// </summary>
public class XteaTransform : IPacketTransform
{
    public string Name => "XTEA";
    
    private readonly uint[] _key = new uint[4];
    private readonly string? _keyContextName;
    private const uint Delta = 0x9E3779B9;
    private const int Rounds = 32;

    public XteaTransform(Dictionary<string, object>? options)
    {
        if (options == null) return;
        
        if (options.TryGetValue("key", out var keyObj) && keyObj is string keyHex)
            ParseKey(keyHex);
        
        if (options.TryGetValue("key_from_context", out var ctxName))
            _keyContextName = ctxName?.ToString();
    }

    private uint[] ResolveKey(TransformContext context)
    {
        if (_keyContextName != null && context.Has(_keyContextName))
        {
            var key = context.Get<uint[]>(_keyContextName);
            if (key != null) return key;
        }
        return _key;
    }

    public byte[] Transform(byte[] data, TransformContext context)
    {
        var key = ResolveKey(context);
        if (key[0] == 0 && key[1] == 0 && key[2] == 0 && key[3] == 0)
            return data;
        return ProcessBlocks(data, key, decrypt: true);
    }

    public byte[] ReverseTransform(byte[] data, TransformContext context)
    {
        var key = ResolveKey(context);
        if (key[0] == 0 && key[1] == 0 && key[2] == 0 && key[3] == 0)
            return data;
        return ProcessBlocks(data, key, decrypt: false);
    }

    private void ParseKey(string hex)
    {
        var bytes = Convert.FromHexString(hex);
        if (bytes.Length >= 16)
            for (int i = 0; i < 4; i++)
                _key[i] = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(i * 4));
    }

    private static byte[] ProcessBlocks(byte[] data, uint[] key, bool decrypt)
    {
        int blockCount = data.Length / 8;
        if (blockCount == 0) return data;

        var result = new byte[data.Length];
        Array.Copy(data, result, data.Length);

        for (int block = 0; block < blockCount; block++)
        {
            int offset = block * 8;
            uint v0 = BinaryPrimitives.ReadUInt32LittleEndian(result.AsSpan(offset));
            uint v1 = BinaryPrimitives.ReadUInt32LittleEndian(result.AsSpan(offset + 4));

            if (decrypt)
            {
                uint sum = unchecked(Delta * Rounds);
                for (int i = 0; i < Rounds; i++)
                {
                    v1 -= (((v0 << 4) ^ (v0 >> 5)) + v0) ^ (sum + key[(sum >> 11) & 3]);
                    sum -= Delta;
                    v0 -= (((v1 << 4) ^ (v1 >> 5)) + v1) ^ (sum + key[sum & 3]);
                }
            }
            else
            {
                uint sum = 0;
                for (int i = 0; i < Rounds; i++)
                {
                    v0 += (((v1 << 4) ^ (v1 >> 5)) + v1) ^ (sum + key[sum & 3]);
                    sum += Delta;
                    v1 += (((v0 << 4) ^ (v0 >> 5)) + v0) ^ (sum + key[(sum >> 11) & 3]);
                }
            }

            BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(offset), v0);
            BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(offset + 4), v1);
        }

        return result;
    }
}
