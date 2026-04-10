using System.Net;
using System.Net.Sockets;

namespace PacketCaptureAgent;

/// <summary>TCP 프록시 서버. 패스스루 중계 + takeover 전환.</summary>
public class ProxyServer
{
    private readonly ProtocolDefinition _protocol;
    private readonly ActionCatalog _catalog;
    private readonly PacketObserver _observer;
    private readonly PacketParser _parser;
    private readonly TextWriter _output;
    private volatile bool _takeover;
    private volatile bool _running = true;

    public ProxyServer(ProtocolDefinition protocol, ActionCatalog catalog, TextWriter? output = null)
    {
        _protocol = protocol;
        _catalog = catalog;
        _observer = new PacketObserver(catalog);
        _parser = new PacketParser(protocol);
        _output = output ?? Console.Out;
    }

    public async Task RunAsync(int listenPort, string serverHost, int serverPort,
        string? fsmPath, string? behaviorPath, int? durationSec)
    {
        var listener = new TcpListener(IPAddress.Any, listenPort);
        listener.Start();
        _output.WriteLine($"[Proxy] Listening on port {listenPort}, target {serverHost}:{serverPort}");
        _output.WriteLine("[Proxy] Waiting for client connection...");

        using var clientTcp = await listener.AcceptTcpClientAsync();
        listener.Stop();
        _output.WriteLine("[Proxy] Client connected");

        using var serverTcp = new TcpClient();
        await serverTcp.ConnectAsync(serverHost, serverPort);
        _output.WriteLine($"[Proxy] Connected to server {serverHost}:{serverPort}");

        var clientStream = clientTcp.GetStream();
        var serverStream = serverTcp.GetStream();

        // 서버 응답 파싱용
        var connKey = new ConnectionKey(IPAddress.Parse(serverHost), serverPort, IPAddress.Loopback, 0);
        var serverTcpStream = new TcpStream(connKey);
        var clientConnKey = new ConnectionKey(IPAddress.Loopback, 0, IPAddress.Parse(serverHost), serverPort);
        var clientTcpStream = new TcpStream(clientConnKey);

        var context = new ReplayContext();
        var sharedState = new Dictionary<string, object>();

        _output.WriteLine("[Proxy] Passthrough mode (t: takeover, q: quit)\n");

        // 양방향 중계 태스크
        var clientToServer = RelayAsync(clientStream, serverStream, "C→S", clientTcpStream, context, sharedState, isClientToServer: true);
        var serverToClient = RelayAsync(serverStream, clientStream, "S→C", serverTcpStream, context, sharedState, isClientToServer: false);

        // 콘솔 입력 처리
        var consoleTask = Task.Run(async () =>
        {
            while (_running)
            {
                if (Console.KeyAvailable)
                {
                    var key = Console.ReadKey(true).Key;
                    if (key == ConsoleKey.T && !_takeover)
                    {
                        _takeover = true;
                        _output.WriteLine($"\n[Proxy] === TAKEOVER === (FSM state: {_observer.CurrentFsmState ?? "unknown"})");
                        await RunTakeoverAsync(serverStream, context, sharedState, serverHost, serverPort, fsmPath, behaviorPath, durationSec);
                        _takeover = false;
                        _output.WriteLine("[Proxy] === PASSTHROUGH restored ===\n");
                    }
                    else if (key == ConsoleKey.Q)
                    {
                        _running = false;
                        break;
                    }
                }
                await Task.Delay(50);
            }
        });

        await Task.WhenAny(clientToServer, serverToClient, consoleTask);
        _running = false;
        _output.WriteLine("[Proxy] Session ended");
    }

    private async Task RelayAsync(NetworkStream source, NetworkStream dest, string label,
        TcpStream tcpStream, ReplayContext context, Dictionary<string, object> sharedState,
        bool isClientToServer)
    {
        var buffer = new byte[65536];
        try
        {
            while (_running)
            {
                if (_takeover && isClientToServer)
                {
                    // takeover 중 클라이언트→서버 방향은 읽기만 하고 전달하지 않음
                    using var cts = new CancellationTokenSource(100);
                    try { int _ = await source.ReadAsync(buffer, cts.Token); }
                    catch (OperationCanceledException) { }
                    continue;
                }

                using var cts2 = new CancellationTokenSource(1000);
                int len;
                try { len = await source.ReadAsync(buffer, cts2.Token); }
                catch (OperationCanceledException) { continue; }

                if (len == 0) break;

                // 패킷 파싱 + 로깅
                tcpStream.Append(buffer.AsSpan(0, len));
                ParsedPacket? pkt;
                while ((pkt = _parser.TryParse(tcpStream)) != null)
                {
                    _output.WriteLine($"[{label}] {pkt.Name}");
                    if (isClientToServer)
                        _observer.OnSendPacket(pkt.Name);
                    else
                    {
                        // 서버 응답 → SessionState + GameWorldState 업데이트
                        foreach (var f in pkt.Fields)
                            FieldFlattener.Flatten(sharedState, $"{pkt.Name}.{f.Key}", f.Value);
                        context.World.Update(pkt.Name, pkt.Fields);
                    }
                }

                // 전달
                if (!_takeover || !isClientToServer)
                    await dest.WriteAsync(buffer.AsMemory(0, len));
            }
        }
        catch (IOException) { }
        catch (ObjectDisposedException) { }
    }

    private async Task RunTakeoverAsync(NetworkStream serverStream, ReplayContext context,
        Dictionary<string, object> sharedState, string host, int port,
        string? fsmPath, string? behaviorPath, int? durationSec)
    {
        var innerHandler = new ParsingResponseHandler(_protocol, host, port, _output);
        var handler = new TrackingResponseHandler(innerHandler, sharedState);
        var interceptors = new List<IReplayInterceptor>
        {
            new DynamicFieldInterceptor(new ScenarioBuilder().CollectAllDynamicFields(_catalog), sharedState),
            new ProximityInterceptor(_protocol.Semantics?.ProximityActions ?? new())
        };
        var syncHandler = new BtSyncHandler(handler, context, sharedState);
        var actionExecutor = new ActionExecutor(_protocol, _catalog);

        if (fsmPath != null)
        {
            var fsm = FsmDefinition.Load(fsmPath);
            var startState = _observer.CurrentFsmState ?? "move";
            _output.WriteLine($"[Proxy] FSM starting from state: {startState}");
            var executor = new FsmExecutor(actionExecutor, _output);
            await executor.ExecuteOnStreamAsync(fsm, host, port, serverStream, syncHandler, context, interceptors, startState, durationSec: durationSec);
        }
        else if (behaviorPath != null)
        {
            var tree = BehaviorTreeDefinition.Load(behaviorPath);
            _output.WriteLine($"[Proxy] BT starting, pre-observed actions: {_observer.ObservedActions.Count}");
            var executor = new BehaviorTreeExecutor(actionExecutor, _output, _protocol.Semantics);
            await executor.ExecuteOnStreamAsync(tree, serverStream, syncHandler, context, interceptors, preObserved: _observer.ObservedActions);
        }
        else
        {
            _output.WriteLine("[Proxy] No FSM/BT specified. Press 'p' to return to passthrough.");
            while (_takeover && _running)
            {
                if (Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.P)
                    break;
                await Task.Delay(50);
            }
        }
    }
}
