using System.Net.Sockets;
using System.Text.Json;

namespace PacketCaptureAgent;

/// <summary>단일 ActionCatalog 액션 실행. BT 리프 노드의 실행 단위.
/// 기존 PacketBuilder, IResponseHandler, IReplayInterceptor를 재사용.</summary>
public class ActionExecutor
{
    private readonly ProtocolDefinition _protocol;
    private readonly PacketBuilder _builder;
    private readonly ActionCatalog _catalog;
    private readonly Dictionary<string, string> _randomCache = new();

    public ActionExecutor(ProtocolDefinition protocol, ActionCatalog catalog)
    {
        _protocol = protocol;
        _builder = new PacketBuilder(protocol);
        _catalog = catalog;
    }

    /// <summary>단일 액션의 패킷을 전송하고 응답을 처리. SessionState 업데이트 포함.</summary>
    public bool Execute(
        string actionId,
        NetworkStream stream,
        IResponseHandler handler,
        ReplayContext context,
        List<IReplayInterceptor> interceptors,
        TextWriter output,
        Dictionary<string, object>? overrides = null,
        int timeoutMs = 5000)
    {
        var action = _catalog.Actions.FirstOrDefault(a => a.Id == actionId);
        if (action == null) { output.WriteLine($"  ⚠ Action '{actionId}' not found"); return false; }

        var session = new ReplaySession(stream, _builder, handler, context, DateTime.Now, output);
        var recvBuffer = new byte[65536];

        foreach (var ap in action.Packets)
        {
            var (name, count) = ScenarioBuilder.ParsePacketName(ap.Name);

            if (ap.Direction == "SEND")
            {
                var fields = new Dictionary<string, object>();
                if (ap.Fields != null)
                    foreach (var kv in ap.Fields)
                        fields[kv.Key] = kv.Value is JsonElement je ? ScenarioBuilder.ConvertJsonElement(je) : kv.Value;
                if (overrides != null)
                    foreach (var kv in overrides)
                        fields[kv.Key] = ScenarioBuilder.ResolveValue(kv.Value, _randomCache);

                var pkt = new ReplayPacket(name, "SEND", fields, TimeSpan.Zero);

                // 인터셉터 체이닝
                foreach (var ic in interceptors.OrderBy(ic => ic.Priority).Where(ic => ic.ShouldIntercept(pkt, context.World)))
                    pkt = ic.Prepare(session, pkt);

                var data = _builder.Build(pkt.Name, pkt.Fields);
                stream.Write(data);
                context.Elapsed = TimeSpan.Zero;
                output.WriteLine($"[BT] SEND {pkt.Name} ({data.Length} bytes)");
            }

            if (ap.Direction == "RECV" || ap.Direction == "SEND")
            {
                // SEND 후 응답 대기
                if (ap.Direction == "SEND")
                {
                    for (int c = 0; c < count; c++) // drain expected recv count after send
                    {
                        if (WaitForResponse(stream, recvBuffer, timeoutMs, out var len))
                            handler.OnResponse(recvBuffer, len, context);
                    }
                    // drain additional
                    Thread.Sleep(50);
                    while (stream.DataAvailable)
                    {
                        try { stream.ReadTimeout = 100; int len = stream.Read(recvBuffer); if (len > 0) handler.OnResponse(recvBuffer, len, context); }
                        catch (IOException) { break; }
                    }
                }
            }
        }

        return true;
    }

    private static bool WaitForResponse(NetworkStream stream, byte[] buffer, int timeoutMs, out int length)
    {
        length = 0;
        stream.ReadTimeout = timeoutMs;
        try { length = stream.Read(buffer); return length > 0; }
        catch (IOException) { return false; }
    }
}
