using System.Net.Sockets;

namespace PacketCaptureAgent;

/// <summary>Behavior Tree 런타임 실행. Validation 모드: weight 스킵 없이 전체 실행 + 성공/실패 로깅.</summary>
public class BehaviorTreeExecutor
{
    private readonly ActionExecutor _actionExecutor;
    private readonly TextWriter _output;
    private readonly HashSet<string> _interactionActions;

    // Validation 결과
    private int _totalActions, _successCount, _failCount, _skippedCount;
    private readonly List<(string action, string reason)> _failures = new();
    private readonly HashSet<string> _validatedActions = new();

    public BehaviorTreeExecutor(ActionExecutor actionExecutor, TextWriter? output = null,
        SemanticsDefinition? semantics = null)
    {
        _actionExecutor = actionExecutor;
        _output = output ?? Console.Out;

        // 상호작용 필수 액션 패턴 수집 (실패 허용)
        _interactionActions = new HashSet<string>();
        if (semantics?.StateConditions != null)
            foreach (var sc in semantics.StateConditions)
                _interactionActions.Add(sc.ActionPattern);
    }

    public async Task ExecuteAsync(
        BehaviorTreeDefinition tree,
        string host, int port,
        IResponseHandler handler,
        ReplayContext context,
        List<IReplayInterceptor> interceptors,
        int timeoutMs = 5000,
        int? durationSec = null)
    {
        _output.WriteLine($"=== Behavior Tree Validation: {tree.Name} ===\n");

        using var client = new TcpClient();
        client.Connect(host, port);
        using var stream = client.GetStream();
        _output.WriteLine($"Connected to {host}:{port}\n");

        _totalActions = _successCount = _failCount = _skippedCount = 0;
        _failures.Clear();
        _validatedActions.Clear();

        await RunNodeAsync(tree.Root, stream, handler, context, interceptors, timeoutMs);

        // 결과 요약
        _output.WriteLine($"\n=== Validation Summary ===");
        _output.WriteLine($"Total: {_totalActions}, Success: {_successCount}, Failed: {_failCount}, Skipped(condition): {_skippedCount}");
        if (_failures.Count > 0)
        {
            _output.WriteLine("Failed actions:");
            foreach (var (action, reason) in _failures)
                _output.WriteLine($"  {action}: {reason}");
        }
    }

    private async Task<bool> RunNodeAsync(
        BtNode node,
        NetworkStream stream,
        IResponseHandler handler,
        ReplayContext context,
        List<IReplayInterceptor> interceptors,
        int timeoutMs)
    {
        return node switch
        {
            BtAction action => await RunActionAsync(action, stream, handler, context, interceptors, timeoutMs),
            BtSequence seq => await RunSequenceAsync(seq, stream, handler, context, interceptors, timeoutMs),
            BtSelector sel => await RunSelectorAsync(sel, stream, handler, context, interceptors, timeoutMs),
            BtRepeat rep => await RunRepeatAsync(rep, stream, handler, context, interceptors, timeoutMs),
            _ => false
        };
    }

    private async Task<bool> RunActionAsync(BtAction node, NetworkStream stream, IResponseHandler handler,
        ReplayContext context, List<IReplayInterceptor> interceptors, int timeoutMs)
    {
        if (!_validatedActions.Add(node.Id))
        {
            _skippedCount++;
            return true;
        }

        _totalActions++;
        bool ok = await _actionExecutor.ExecuteAsync(node.Id, stream, handler, context, interceptors, _output, node.Overrides, timeoutMs);
        bool isInteraction = _interactionActions.Any(p => node.Id.Contains(p));

        if (ok)
            _successCount++;
        else if (isInteraction)
        { _successCount++; _output.WriteLine($"  [EXPECTED] {node.Id} (interaction — failure allowed)"); }
        else
        { _failCount++; _failures.Add((node.Id, "execution failed")); _output.WriteLine($"  [FAIL] {node.Id}"); }

        return true;
    }

    private async Task<bool> RunSequenceAsync(BtSequence node, NetworkStream stream, IResponseHandler handler,
        ReplayContext context, List<IReplayInterceptor> interceptors, int timeoutMs)
    {
        foreach (var child in node.Children)
            await RunNodeAsync(child, stream, handler, context, interceptors, timeoutMs);
        return true;
    }

    private async Task<bool> RunSelectorAsync(BtSelector node, NetworkStream stream, IResponseHandler handler,
        ReplayContext context, List<IReplayInterceptor> interceptors, int timeoutMs)
    {
        foreach (var child in node.Children)
            await RunNodeAsync(child, stream, handler, context, interceptors, timeoutMs);
        return true;
    }

    private async Task<bool> RunRepeatAsync(BtRepeat node, NetworkStream stream, IResponseHandler handler,
        ReplayContext context, List<IReplayInterceptor> interceptors, int timeoutMs)
    {
        await RunNodeAsync(node.Child, stream, handler, context, interceptors, timeoutMs);
        return true;
    }
}
