using System.Numerics;
using System.Security.Cryptography;

namespace PacketCaptureAgent;

/// <summary>
/// RSA 암호화/복호화 (Tibia/Forgotten Server 로그인 패킷)
/// </summary>
public class RsaTransform : IPacketTransform
{
    public string Name => "RSA";
    
    private BigInteger _d; // private exponent (decrypt)
    private BigInteger _e; // public exponent (encrypt)
    private BigInteger _n; // modulus
    private bool _hasKey;
    private readonly int _offset;
    private readonly int _length;
    private readonly string? _xteaKeyOutput;
    private readonly bool _useRawRsa;

    public RsaTransform(Dictionary<string, object>? options)
    {
        if (options == null) return;

        if (options.TryGetValue("private_key_file", out var keyFile))
            LoadPrivateKey(keyFile?.ToString());

        if (options.TryGetValue("offset", out var off))
            _offset = Convert.ToInt32(off);

        _length = 128;
        if (options.TryGetValue("length", out var len))
            _length = Convert.ToInt32(len);

        if (options.TryGetValue("xtea_key_output", out var output))
            _xteaKeyOutput = output?.ToString();

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
            var encrypted = data.AsSpan(_offset, _length).ToArray();
            byte[] decrypted = _useRawRsa ? RawRsaOp(encrypted, _d) : StdRsaDecrypt(encrypted);

            if (_xteaKeyOutput != null && decrypted.Length >= 16)
            {
                var xteaKey = new uint[4];
                for (int i = 0; i < 4; i++)
                    xteaKey[i] = BitConverter.ToUInt32(decrypted, i * 4);
                context.Set(_xteaKeyOutput, xteaKey);
            }

            var result = new byte[_offset + decrypted.Length + (data.Length - _offset - _length)];
            Array.Copy(data, 0, result, 0, _offset);
            Array.Copy(decrypted, 0, result, _offset, decrypted.Length);
            if (data.Length > _offset + _length)
                Array.Copy(data, _offset + _length, result, _offset + decrypted.Length, 
                    data.Length - _offset - _length);

            return result;
        }
        catch { return data; }
    }

    public byte[] ReverseTransform(byte[] data, TransformContext context)
    {
        if (!_hasKey || data.Length < _offset)
            return data;

        try
        {
            var plainLen = data.Length - _offset;
            var plain = data.AsSpan(_offset, plainLen).ToArray();
            byte[] encrypted = _useRawRsa ? RawRsaOp(plain, _e) : StdRsaEncrypt(plain);

            var result = new byte[_offset + encrypted.Length];
            Array.Copy(data, 0, result, 0, _offset);
            Array.Copy(encrypted, 0, result, _offset, encrypted.Length);
            return result;
        }
        catch { return data; }
    }

    private byte[] RawRsaOp(byte[] input, BigInteger exponent)
    {
        var reversed = input.Reverse().ToArray();
        var value = new BigInteger(reversed, isUnsigned: true);
        var output = BigInteger.ModPow(value, exponent, _n);
        var result = output.ToByteArray(isUnsigned: true);
        Array.Reverse(result);

        if (result.Length < _length)
        {
            var padded = new byte[_length];
            Array.Copy(result, 0, padded, _length - result.Length, result.Length);
            return padded;
        }
        return result;
    }

    private byte[] StdRsaDecrypt(byte[] encrypted)
    {
        using var rsa = RSA.Create();
        rsa.ImportParameters(new RSAParameters { D = _d.ToByteArray(true, true), Modulus = _n.ToByteArray(true, true) });
        return rsa.Decrypt(encrypted, RSAEncryptionPadding.Pkcs1);
    }

    private byte[] StdRsaEncrypt(byte[] plain)
    {
        using var rsa = RSA.Create();
        rsa.ImportParameters(new RSAParameters { Exponent = _e.ToByteArray(true, true), Modulus = _n.ToByteArray(true, true) });
        return rsa.Encrypt(plain, RSAEncryptionPadding.Pkcs1);
    }

    private void LoadPrivateKey(string? path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;

        var pem = File.ReadAllText(path);
        using var rsa = RSA.Create();
        rsa.ImportFromPem(pem);
        
        var parameters = rsa.ExportParameters(true);
        _d = new BigInteger(parameters.D!, isUnsigned: true, isBigEndian: true);
        _e = new BigInteger(parameters.Exponent!, isUnsigned: true, isBigEndian: true);
        _n = new BigInteger(parameters.Modulus!, isUnsigned: true, isBigEndian: true);
        _hasKey = true;
    }
}
