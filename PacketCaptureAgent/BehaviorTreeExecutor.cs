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

    public void Execute(
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

        RunNode(tree.Root, stream, handler, context, interceptors, timeoutMs);

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

    private bool RunNode(
        BtNode node,
        NetworkStream stream,
        IResponseHandler handler,
        ReplayContext context,
        List<IReplayInterceptor> interceptors,
        int timeoutMs)
    {
        // validation: 조건 무시 — 모든 분기 강제 실행
        // (조건은 녹화 당시 세션 고유값이므로 다른 세션에서는 매칭 안 됨)

        // weight 스킵 제거 — 모든 액션 실행

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
        // 이미 검증한 액션은 스킵 (같은 액션 63번 실행 불필요)
        if (!_validatedActions.Add(node.Id))
        {
            _skippedCount++;
            return true;
        }

        _totalActions++;
        bool ok = _actionExecutor.Execute(node.Id, stream, handler, context, interceptors, _output, node.Overrides, timeoutMs);
        bool isInteraction = _interactionActions.Any(p => node.Id.Contains(p));

        if (ok)
            _successCount++;
        else if (isInteraction)
        { _successCount++; _output.WriteLine($"  [EXPECTED] {node.Id} (interaction — failure allowed)"); }
        else
        { _failCount++; _failures.Add((node.Id, "execution failed")); _output.WriteLine($"  [FAIL] {node.Id}"); }

        return true; // validation: 실패해도 계속 진행
    }

    private bool RunSequence(BtSequence node, NetworkStream stream, IResponseHandler handler,
        ReplayContext context, List<IReplayInterceptor> interceptors, int timeoutMs)
    {
        foreach (var child in node.Children)
            RunNode(child, stream, handler, context, interceptors, timeoutMs);
        return true; // validation: 항상 계속
    }

    private bool RunSelector(BtSelector node, NetworkStream stream, IResponseHandler handler,
        ReplayContext context, List<IReplayInterceptor> interceptors, int timeoutMs)
    {
        // validation: 조건 맞는 모든 자식 실행
        foreach (var child in node.Children)
            RunNode(child, stream, handler, context, interceptors, timeoutMs);
        return true;
    }

    private bool RunRepeat(BtRepeat node, NetworkStream stream, IResponseHandler handler,
        ReplayContext context, List<IReplayInterceptor> interceptors, int timeoutMs)
    {
        // validation: 1회만 실행 (같은 액션 N번 반복은 검증 불필요)
        RunNode(node.Child, stream, handler, context, interceptors, timeoutMs);
        return true;
    }
}
