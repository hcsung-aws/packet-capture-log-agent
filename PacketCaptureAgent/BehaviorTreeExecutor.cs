using System.Net.Sockets;

namespace PacketCaptureAgent;

/// <summary>Behavior Tree 런타임 실행. 트리 순회 + 조건 평가 + ActionExecutor로 액션 실행.</summary>
public class BehaviorTreeExecutor
{
    private readonly ActionExecutor _actionExecutor;
    private readonly TextWriter _output;

    public BehaviorTreeExecutor(ActionExecutor actionExecutor, TextWriter? output = null)
    {
        _actionExecutor = actionExecutor;
        _output = output ?? Console.Out;
    }

    public void Execute(
        BehaviorTreeDefinition tree,
        string host, int port,
        IResponseHandler handler,
        ReplayContext context,
        List<IReplayInterceptor> interceptors,
        int timeoutMs = 5000)
    {
        _output.WriteLine($"=== Behavior Tree: {tree.Name} ===\n");

        using var client = new TcpClient();
        client.Connect(host, port);
        using var stream = client.GetStream();
        _output.WriteLine($"Connected to {host}:{port}\n");

        RunNode(tree.Root, stream, handler, context, interceptors, timeoutMs);

        _output.WriteLine($"\nBehavior Tree completed.");
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
