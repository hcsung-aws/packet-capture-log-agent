using System.Numerics;
using System.Security.Cryptography;

namespace PacketCaptureAgent;

/// <summary>
/// RSA 복호화 (Tibia/Forgotten Server 로그인 패킷)
/// </summary>
public class RsaDecryptor : IPacketTransform
{
    public string Name => "RSA";
    
    private BigInteger _d; // private exponent
    private BigInteger _n; // modulus
    private bool _hasKey;
    private readonly int _offset;
    private readonly int _length;
    private readonly string? _xteaKeyOutput;
    private readonly bool _useRawRsa; // Tibia uses raw RSA (no padding)

    public RsaDecryptor(Dictionary<string, object>? options)
    {
        if (options == null) return;

        // PEM 키 파일 경로
        if (options.TryGetValue("private_key_file", out var keyFile))
            LoadPrivateKey(keyFile?.ToString());

        // RSA 복호화 시작 오프셋 (헤더 이후)
        if (options.TryGetValue("offset", out var off))
            _offset = Convert.ToInt32(off);

        // RSA 블록 길이 (기본 128 = 1024bit)
        _length = 128;
        if (options.TryGetValue("length", out var len))
            _length = Convert.ToInt32(len);

        // XTEA 키 추출 후 컨텍스트에 저장할 이름
        if (options.TryGetValue("xtea_key_output", out var output))
            _xteaKeyOutput = output?.ToString();

        // Raw RSA (no padding) - Tibia 프로토콜용
        _useRawRsa = true;
        if (options.TryGetValue("use_raw_rsa", out var raw))
            _useRawRsa = Convert.ToBoolean(raw);
    }

    public byte[] Transform(byte[] data, TransformContext context)
    {
        if (!_hasKey || data.Length < _offset + _length)
            return data;

        try
        {
            // RSA 블록 추출
            var encrypted = data.AsSpan(_offset, _length).ToArray();
            
            byte[] decrypted;
            if (_useRawRsa)
            {
                // Raw RSA 복호화 (Tibia 프로토콜)
                decrypted = RawRsaDecrypt(encrypted);
            }
            else
            {
                // 표준 PKCS#1 복호화
                using var rsa = RSA.Create();
                rsa.ImportParameters(new RSAParameters { D = _d.ToByteArray(true, true), Modulus = _n.ToByteArray(true, true) });
                decrypted = rsa.Decrypt(encrypted, RSAEncryptionPadding.Pkcs1);
            }

            // XTEA 키 추출 (Tibia: 복호화된 데이터의 처음 16바이트)
            if (_xteaKeyOutput != null && decrypted.Length >= 16)
            {
                var xteaKey = new uint[4];
                for (int i = 0; i < 4; i++)
                    xteaKey[i] = BitConverter.ToUInt32(decrypted, i * 4);
                context.Set(_xteaKeyOutput, xteaKey);
            }

            // 결과 조합: 헤더 + 복호화된 데이터 + 나머지
            var result = new byte[_offset + decrypted.Length + (data.Length - _offset - _length)];
            Array.Copy(data, 0, result, 0, _offset);
            Array.Copy(decrypted, 0, result, _offset, decrypted.Length);
            if (data.Length > _offset + _length)
                Array.Copy(data, _offset + _length, result, _offset + decrypted.Length, 
                    data.Length - _offset - _length);

            return result;
        }
        catch
        {
            return data; // 복호화 실패시 원본 반환
        }
    }

    private byte[] RawRsaDecrypt(byte[] encrypted)
    {
        // BigInteger는 little-endian, RSA는 big-endian
        var reversed = encrypted.Reverse().ToArray();
        var cipher = new BigInteger(reversed, isUnsigned: true);
        
        // RSA 복호화: plaintext = cipher^d mod n
        var plain = BigInteger.ModPow(cipher, _d, _n);
        
        // 결과를 바이트 배열로 변환 (big-endian)
        var result = plain.ToByteArray(isUnsigned: true);
        Array.Reverse(result);
        
        // 128바이트로 패딩 (앞에 0 추가)
        if (result.Length < _length)
        {
            var padded = new byte[_length];
            Array.Copy(result, 0, padded, _length - result.Length, result.Length);
            return padded;
        }
        
        return result;
    }

    private void LoadPrivateKey(string? path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;

        var pem = File.ReadAllText(path);
        using var rsa = RSA.Create();
        rsa.ImportFromPem(pem);
        
        var parameters = rsa.ExportParameters(true);
        _d = new BigInteger(parameters.D!, isUnsigned: true, isBigEndian: true);
        _n = new BigInteger(parameters.Modulus!, isUnsigned: true, isBigEndian: true);
        _hasKey = true;
    }
}
