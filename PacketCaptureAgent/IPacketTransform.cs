namespace PacketCaptureAgent;

/// <summary>
/// 패킷 데이터 변환 인터페이스 (복호화, 압축해제 등)
/// </summary>
public interface IPacketTransform
{
    string Name { get; }
    byte[] Transform(byte[] data, TransformContext context);
}

/// <summary>
/// Transform 간 상태 공유용 컨텍스트
/// </summary>
public class TransformContext
{
    public Dictionary<string, object> State { get; } = new();
    
    public void Set<T>(string key, T value) => State[key] = value!;
    public T? Get<T>(string key) => State.TryGetValue(key, out var v) ? (T)v : default;
    public bool Has(string key) => State.ContainsKey(key);
}

/// <summary>
/// Transform 팩토리 - JSON 정의에서 Transform 인스턴스 생성
/// </summary>
public static class TransformFactory
{
    public static IPacketTransform Create(TransformDefinition def)
    {
        return def.Type.ToLower() switch
        {
            "xtea" => new XteaDecryptor(def.Options),
            "rsa" => new RsaDecryptor(def.Options),
            _ => throw new NotSupportedException($"Unknown transform: {def.Type}")
        };
    }

    public static List<IPacketTransform> CreatePipeline(List<TransformDefinition>? definitions)
    {
        if (definitions == null || definitions.Count == 0)
            return new List<IPacketTransform>();
        
        return definitions.Select(Create).ToList();
    }
}
