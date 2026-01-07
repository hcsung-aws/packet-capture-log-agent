using System.Buffers.Binary;

namespace PacketCaptureAgent;

/// <summary>
/// XTEA 복호화 (Tibia/Forgotten Server 프로토콜)
/// </summary>
public class XteaDecryptor : IPacketTransform
{
    public string Name => "XTEA";
    
    private readonly uint[] _key = new uint[4];
    private readonly string? _keyContextName;
    private const uint Delta = 0x9E3779B9;
    private const int Rounds = 32;

    public XteaDecryptor(Dictionary<string, object>? options)
    {
        if (options == null) return;
        
        // 고정 키 또는 컨텍스트에서 키 가져오기
        if (options.TryGetValue("key", out var keyObj) && keyObj is string keyHex)
            ParseKey(keyHex);
        
        if (options.TryGetValue("key_from_context", out var ctxName))
            _keyContextName = ctxName?.ToString();
    }

    public byte[] Transform(byte[] data, TransformContext context)
    {
        // 컨텍스트에서 키 가져오기 (RSA에서 추출된 키)
        if (_keyContextName != null && context.Has(_keyContextName))
        {
            var key = context.Get<uint[]>(_keyContextName);
            if (key != null) Array.Copy(key, _key, 4);
        }

        if (_key[0] == 0 && _key[1] == 0 && _key[2] == 0 && _key[3] == 0)
            return data; // 키 없으면 패스스루

        return Decrypt(data);
    }

    private void ParseKey(string hex)
    {
        var bytes = Convert.FromHexString(hex);
        if (bytes.Length >= 16)
        {
            for (int i = 0; i < 4; i++)
                _key[i] = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(i * 4));
        }
    }

    private byte[] Decrypt(byte[] data)
    {
        // XTEA는 8바이트 블록 단위
        int blockCount = data.Length / 8;
        if (blockCount == 0) return data;

        var result = new byte[data.Length];
        Array.Copy(data, result, data.Length);

        for (int block = 0; block < blockCount; block++)
        {
            int offset = block * 8;
            uint v0 = BinaryPrimitives.ReadUInt32LittleEndian(result.AsSpan(offset));
            uint v1 = BinaryPrimitives.ReadUInt32LittleEndian(result.AsSpan(offset + 4));

            uint sum = unchecked(Delta * Rounds);
            for (int i = 0; i < Rounds; i++)
            {
                v1 -= (((v0 << 4) ^ (v0 >> 5)) + v0) ^ (sum + _key[(sum >> 11) & 3]);
                sum -= Delta;
                v0 -= (((v1 << 4) ^ (v1 >> 5)) + v1) ^ (sum + _key[sum & 3]);
            }

            BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(offset), v0);
            BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(offset + 4), v1);
        }

        return result;
    }
}
