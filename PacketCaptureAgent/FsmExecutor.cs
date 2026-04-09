using System.Net.Sockets;

namespace PacketCaptureAgent;

/// <summary>FSM 전이 확률 기반 실행. 매 스텝마다 전이 확률로 다음 액션을 선택.</summary>
public class FsmExecutor
{
    private readonly ActionExecutor _actionExecutor;
    private readonly TextWriter _output;

    public FsmExecutor(ActionExecutor actionExecutor, TextWriter? output = null)
    {
        _actionExecutor = actionExecutor;
        _output = output ?? Console.Out;
    }

    /// <param name="durationSec">실행 시간(초). null=1회(전이 불가까지), 0=무한, >0=해당 시간.</param>
    public async Task ExecuteAsync(
        FsmDefinition fsm,
        string host, int port,
        IResponseHandler handler,
        ReplayContext context,
        List<IReplayInterceptor> interceptors,
        int timeoutMs = 5000,
        int? durationSec = null)
    {
        _output.WriteLine($"=== FSM: {fsm.Name} (states: {fsm.Transitions.Count}) ===\n");

        var sw = System.Diagnostics.Stopwatch.StartNew();
        string currentState = fsm.InitialState;
        int step = 0;
        TcpClient? client = null;
        NetworkStream? stream = null;

        try
        {
            while (true)
            {
                step++;

                // connect: TCP 연결 수립
                if (currentState == "connect")
                {
                    _output.WriteLine($"[FSM] Step {step}: connect → {host}:{port}");
                    client?.Dispose();
                    client = new TcpClient();
                    client.Connect(host, port);
                    stream = client.GetStream();
                    _output.WriteLine($"Connected to {host}:{port}\n");
                }
                // disconnect: TCP 연결 종료
                else if (currentState == "disconnect")
                {
                    _output.WriteLine($"[FSM] Step {step}: disconnect");
                    stream?.Dispose(); stream = null;
                    client?.Dispose(); client = null;
                    context.SessionState.Clear();
                    context.World = new GameWorldState();
                }
                // 일반 액션 실행
                else
                {
                    if (stream == null)
                    {
                        _output.WriteLine($"[FSM] Step {step}: {currentState} — no connection, restarting");
                        currentState = fsm.InitialState;
                        continue;
                    }
                    _output.WriteLine($"[FSM] Step {step}: {currentState}");
                    await _actionExecutor.ExecuteAsync(currentState, stream, handler, context, interceptors, _output, timeoutMs: timeoutMs);
                }

                // 시간 체크
                if (durationSec.HasValue && durationSec.Value > 0 && sw.Elapsed.TotalSeconds >= durationSec.Value)
                    break;

                // 다음 상태 선택
                var next = SelectNextState(fsm, currentState);
                if (next == null)
                {
                    _output.WriteLine($"[FSM] No transition from '{currentState}', disconnecting");
                    if (stream != null) { stream.Dispose(); stream = null; client?.Dispose(); client = null; }
                    if (!durationSec.HasValue) break;
                    currentState = fsm.InitialState;
                    continue;
                }
                currentState = next;
            }
        }
        finally
        {
            stream?.Dispose();
            client?.Dispose();
        }

        _output.WriteLine($"\nFSM completed. ({step} steps, {sw.Elapsed:mm\\:ss})");
    }

    internal static string? SelectNextState(FsmDefinition fsm, string currentState)
    {
        if (!fsm.Transitions.TryGetValue(currentState, out var targets) || targets.Count == 0)
            return null;

        float roll = Random.Shared.NextSingle();
        float cumulative = 0;
        foreach (var (state, prob) in targets)
        {
            cumulative += prob;
            if (roll < cumulative) return state;
        }
        return targets.Last().Key;
    }
}
