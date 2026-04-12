namespace PacketCaptureAgent;

/// <summary>응답 처리 시 SessionState를 ReplayContext와 sharedState 양쪽에 동기화.</summary>
class BtSyncHandler : IResponseHandler
{
    private readonly IResponseHandler _inner;
    private readonly ReplayContext _context;
    private readonly Dictionary<string, object> _sharedState;

    public BtSyncHandler(IResponseHandler inner, ReplayContext context, Dictionary<string, object> sharedState)
    { _inner = inner; _context = context; _sharedState = sharedState; }

    public int OnResponse(byte[] data, int length, ReplayContext context)
    {
        int count = _inner.OnResponse(data, length, _context);
        foreach (var kv in _context.SessionState)
            _sharedState[kv.Key] = kv.Value;
        return count;
    }
}
