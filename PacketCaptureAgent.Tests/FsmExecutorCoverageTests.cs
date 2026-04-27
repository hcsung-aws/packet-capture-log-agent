using System.Net;
using System.Net.Sockets;

namespace PacketCaptureAgent.Tests;

/// <summary>FsmExecutor + CoverageTracker 연결 테스트.</summary>
public class FsmExecutorCoverageTests
{
    private static ProtocolDefinition MinimalProtocol() => new()
    {
        Protocol = new ProtocolInfo
        {
            Name = "Test", Endian = "little",
            Header = new HeaderInfo
            {
                Size = 4, SizeField = "length", TypeField = "type",
                Fields = new List<HeaderFieldInfo>
                {
                    new() { Name = "length", Type = "uint16", Offset = 0 },
                    new() { Name = "type", Type = "uint16", Offset = 2 },
                }
            }
        },
        Packets = new List<PacketDefinition>()
    };

    [Fact]
    public async Task ExecuteOnStream_WithTracker_TracksFsmTransitions()
    {
        var tracker = new CoverageTracker();
        var catalog = new ActionCatalog { Actions = new() };
        var executor = new ActionExecutor(MinimalProtocol(), catalog, tracker: tracker);
        var fsmExec = new FsmExecutor(executor, TextWriter.Null, tracker: tracker);

        var fsm = new FsmDefinition
        {
            Name = "test",
            InitialState = "stateA",
            Transitions = new()
            {
                ["stateA"] = new() { ["stateB"] = 1.0f },
                // stateB has no transitions → FSM ends
            }
        };

        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, port);
        using var server = await listener.AcceptTcpClientAsync();
        listener.Stop();

        using var stream = client.GetStream();
        await fsmExec.ExecuteOnStreamAsync(fsm, "127.0.0.1", port, stream,
            new RawResponseHandler(), new ReplayContext(),
            new List<IReplayInterceptor>(), "stateA", timeoutMs: 100);

        Assert.Contains(("stateA", "stateB"), tracker.FsmTransitions);
        Assert.Contains("stateA", tracker.FsmStatesVisited);
        Assert.Contains("stateB", tracker.FsmStatesVisited);
    }

    [Fact]
    public async Task ExecuteOnStream_WithoutTracker_NoError()
    {
        var catalog = new ActionCatalog { Actions = new() };
        var executor = new ActionExecutor(MinimalProtocol(), catalog);
        var fsmExec = new FsmExecutor(executor, TextWriter.Null); // tracker=null

        var fsm = new FsmDefinition
        {
            Name = "test", InitialState = "s1",
            Transitions = new() { ["s1"] = new() { ["s2"] = 1.0f } }
        };

        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, port);
        using var server = await listener.AcceptTcpClientAsync();
        listener.Stop();

        using var stream = client.GetStream();
        await fsmExec.ExecuteOnStreamAsync(fsm, "127.0.0.1", port, stream,
            new RawResponseHandler(), new ReplayContext(),
            new List<IReplayInterceptor>(), "s1", timeoutMs: 100);
        // No exception = pass
    }
}
