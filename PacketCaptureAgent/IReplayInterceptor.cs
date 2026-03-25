using System.Net.Sockets;

namespace PacketCaptureAgent;

/// <summary>리플레이 중 특정 패킷을 가로채 사전 작업 수행 후 수정된 패킷 반환.</summary>
public interface IReplayInterceptor
{
    /// <summary>실행 우선순위. 낮을수록 먼저 실행. (0=필드주입, 100=게임로직)</summary>
    int Priority { get; }
    bool ShouldIntercept(ReplayPacket packet, GameWorldState world);
    /// <summary>사전 작업(이동 등) 수행 후 수정된 패킷 반환. 원래 루프가 이 패킷을 전송.</summary>
    ReplayPacket Prepare(ReplaySession session, ReplayPacket original);
}

/// <summary>인터셉터가 패킷 송수신에 사용하는 세션 헬퍼.</summary>
public class ReplaySession
{
    private readonly NetworkStream _stream;
    private readonly PacketBuilder _builder;
    private readonly IResponseHandler _handler;
    private readonly ReplayContext _context;
    private readonly byte[] _recvBuffer = new byte[65536];
    private readonly DateTime _startTime;

    public GameWorldState World => _context.World;

    public ReplaySession(NetworkStream stream, PacketBuilder builder,
        IResponseHandler handler, ReplayContext context, DateTime startTime)
    {
        _stream = stream;
        _builder = builder;
        _handler = handler;
        _context = context;
        _startTime = startTime;
    }

    public void SendPacket(string name, Dictionary<string, object> fields)
    {
        var data = _builder.Build(name, fields);
        _stream.Write(data);
        _context.Elapsed = DateTime.Now - _startTime;
        Console.WriteLine($"[{_context.Elapsed:mm\\:ss\\.fff}] SEND {name} (interceptor)");
    }

    public void ReceiveAndProcess(int timeoutMs = 5000)
    {
        _stream.ReadTimeout = timeoutMs;
        try
        {
            int len = _stream.Read(_recvBuffer);
            if (len > 0)
            {
                _context.Elapsed = DateTime.Now - _startTime;
                _handler.OnResponse(_recvBuffer, len, _context);
            }
        }
        catch (IOException) { }
    }
}
