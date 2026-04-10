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
    public async Task<bool> ExecuteAsync(
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

                // field_variants: 누적된 값 중 랜덤 선택 (overrides보다 먼저, overrides가 우선)
                if (action.FieldVariants != null)
                    foreach (var (fk, variants) in action.FieldVariants)
                        if (variants.Count > 0)
                            fields[fk] = ScenarioBuilder.ConvertJsonElement(variants[Random.Shared.Next(variants.Count)]);

                if (overrides != null)
                    foreach (var kv in overrides)
                    {
                        var val = kv.Value is JsonElement je ? ScenarioBuilder.ConvertJsonElement(je) : kv.Value;
                        val = ResolveStateExpression(val, context.SessionState);
                        fields[kv.Key] = ScenarioBuilder.ResolveValue(val, _randomCache);
                    }

                var pkt = new ReplayPacket(name, "SEND", fields, TimeSpan.Zero);

                // 인터셉터 체이닝
                foreach (var ic in interceptors.OrderBy(ic => ic.Priority).Where(ic => ic.ShouldIntercept(pkt, context.World)))
                    pkt = await ic.PrepareAsync(session, pkt);

                var data = _builder.Build(pkt.Name, pkt.Fields);
                await stream.WriteAsync(data);
                context.Elapsed = TimeSpan.Zero;
                output.WriteLine($"[BT] SEND {pkt.Name} ({data.Length} bytes)");
            }

            if (ap.Direction == "RECV" || ap.Direction == "SEND")
            {
                // SEND 후 응답 대기
                if (ap.Direction == "SEND")
                {
                    var startTime = DateTime.Now;
                    for (int c = 0; c < count; c++)
                    {
                        var (got, len) = await PacketReplayer.WaitForDataAsync(stream, recvBuffer, timeoutMs);
                        if (got)
                            handler.OnResponse(recvBuffer, len, context);
                    }
                    int received = 0;
                    await PacketReplayer.DrainPendingDataAsync(stream, recvBuffer, handler, context, received, startTime);
                }
            }
        }

        return true;
    }

    /// <summary>{state_random:key} 표현식 해석.
    /// key가 배열 → random 0..Count-1, 숫자 → random 0..value-1.
    /// key가 array.field 형태 → 배열에서 랜덤 원소의 field 값 반환.</summary>
    internal static object ResolveStateExpression(object val, Dictionary<string, object> sessionState)
    {
        if (val is not string s) return val;
        if (!s.StartsWith("{state_random:") || !s.EndsWith("}")) return val;

        var key = s[14..^1];

        // 직접 키 매칭
        if (sessionState.TryGetValue(key, out var stateVal))
        {
            int bound = stateVal switch
            {
                List<object> list => list.Count,
                int i => i,
                long l => (int)l,
                _ => 0
            };
            return bound > 0 ? Random.Shared.Next(bound) : 0;
        }

        // array.field 패턴: 배열에서 랜덤 원소의 필드값
        var lastDot = key.LastIndexOf('.');
        if (lastDot > 0)
        {
            var arrayKey = key[..lastDot];
            var fieldName = key[(lastDot + 1)..];
            if (sessionState.TryGetValue(arrayKey, out var arrVal) && arrVal is List<object> list && list.Count > 0)
            {
                var idx = Random.Shared.Next(list.Count);
                var elemKey = $"{arrayKey}[{idx}].{fieldName}";
                if (sessionState.TryGetValue(elemKey, out var fieldVal))
                    return fieldVal;
            }
        }

        return val;
    }
}
