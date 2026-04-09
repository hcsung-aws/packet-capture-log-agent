namespace PacketCaptureAgent;

/// <summary>응답 추적 핸들러 — 내부 핸들러 래핑, SessionState → 공유 상태 복사.</summary>
public class TrackingResponseHandler : IResponseHandler
{
    private readonly IResponseHandler _inner;
    private readonly Dictionary<string, object> _sharedState;

    public TrackingResponseHandler(IResponseHandler inner, Dictionary<string, object> sharedState)
    {
        _inner = inner;
        _sharedState = sharedState;
    }

    public int OnResponse(byte[] data, int length, ReplayContext context)
    {
        int count = _inner.OnResponse(data, length, context);
        foreach (var kv in context.SessionState)
            _sharedState[kv.Key] = kv.Value;
        return count;
    }
}
