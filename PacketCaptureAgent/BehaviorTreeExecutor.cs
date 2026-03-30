using System.Net.Sockets;

namespace PacketCaptureAgent;

/// <summary>Behavior Tree 런타임 실행. 트리 순회 + 조건 평가 + ActionExecutor로 액션 실행.</summary>
public class BehaviorTreeExecutor
{
    private readonly ActionExecutor _actionExecutor;
    private readonly TextWriter _output;

    private static readonly Random _rng = new();

    public BehaviorTreeExecutor(ActionExecutor actionExecutor, TextWriter? output = null)
    {
        _actionExecutor = actionExecutor;
        _output = output ?? Console.Out;
    }

    /// <param name="durationSec">실행 시간(초). null=1회, 0=무한, >0=해당 시간만큼 루프.</param>
    public void Execute(
        BehaviorTreeDefinition tree,
        string host, int port,
        IResponseHandler handler,
        ReplayContext context,
        List<IReplayInterceptor> interceptors,
        int timeoutMs = 5000,
        int? durationSec = null)
    {
        _output.WriteLine($"=== Behavior Tree: {tree.Name} ===\n");

        using var client = new TcpClient();
        client.Connect(host, port);
        using var stream = client.GetStream();
        _output.WriteLine($"Connected to {host}:{port}\n");

        var sw = System.Diagnostics.Stopwatch.StartNew();
        int iteration = 0;
        do
        {
            iteration++;
            if (durationSec.HasValue)
                _output.WriteLine($"--- Iteration {iteration} (elapsed: {sw.Elapsed:mm\\:ss}) ---\n");

            RunNode(tree.Root, stream, handler, context, interceptors, timeoutMs);

            if (!durationSec.HasValue) break; // 1회 실행
            if (durationSec.Value > 0 && sw.Elapsed.TotalSeconds >= durationSec.Value) break;
        } while (true);

        _output.WriteLine($"\nBehavior Tree completed. ({iteration} iterations, {sw.Elapsed:mm\\:ss})");
    }

    private bool RunNode(
        BtNode node,
        NetworkStream stream,
        IResponseHandler handler,
        ReplayContext context,
        List<IReplayInterceptor> interceptors,
        int timeoutMs)
    {
        // 조건 체크 (노드에 condition이 있으면)
        if (node.Condition != null && !ConditionEvaluator.Evaluate(node.Condition, context.SessionState))
            return false;

        // weight 체크 (1.0 미만이면 확률 실행)
        if (node.Weight < 1.0f && _rng.NextSingle() >= node.Weight)
            return true; // 스킵하되 실패가 아님 (Sequence 계속 진행)

        return node switch
        {
            BtAction action => RunAction(action, stream, handler, context, interceptors, timeoutMs),
            BtSequence seq => RunSequence(seq, stream, handler, context, interceptors, timeoutMs),
            BtSelector sel => RunSelector(sel, stream, handler, context, interceptors, timeoutMs),
            BtRepeat rep => RunRepeat(rep, stream, handler, context, interceptors, timeoutMs),
            _ => false
        };
    }

    private bool RunAction(BtAction node, NetworkStream stream, IResponseHandler handler,
        ReplayContext context, List<IReplayInterceptor> interceptors, int timeoutMs)
    {
        return _actionExecutor.Execute(node.Id, stream, handler, context, interceptors, _output, node.Overrides, timeoutMs);
    }

    private bool RunSequence(BtSequence node, NetworkStream stream, IResponseHandler handler,
        ReplayContext context, List<IReplayInterceptor> interceptors, int timeoutMs)
    {
        foreach (var child in node.Children)
            if (!RunNode(child, stream, handler, context, interceptors, timeoutMs))
                return false;
        return true;
    }

    private bool RunSelector(BtSelector node, NetworkStream stream, IResponseHandler handler,
        ReplayContext context, List<IReplayInterceptor> interceptors, int timeoutMs)
    {
        foreach (var child in node.Children)
            if (RunNode(child, stream, handler, context, interceptors, timeoutMs))
                return true;
        return false;
    }

    private bool RunRepeat(BtRepeat node, NetworkStream stream, IResponseHandler handler,
        ReplayContext context, List<IReplayInterceptor> interceptors, int timeoutMs)
    {
        for (int i = 0; i < node.Count; i++)
            if (!RunNode(node.Child, stream, handler, context, interceptors, timeoutMs))
                return false;
        return true;
    }
}
