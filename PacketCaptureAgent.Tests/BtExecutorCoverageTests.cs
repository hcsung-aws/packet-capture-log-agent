using System.Net;
using System.Net.Sockets;

namespace PacketCaptureAgent.Tests;

/// <summary>BehaviorTreeExecutor + CoverageTracker 연결 테스트.</summary>
public class BtExecutorCoverageTests
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
    public async Task ExecuteOnStream_WithTracker_TracksBtNodes()
    {
        var tracker = new CoverageTracker();
        var catalog = new ActionCatalog { Actions = new() };
        var executor = new ActionExecutor(MinimalProtocol(), catalog, tracker: tracker);
        var btExec = new BehaviorTreeExecutor(executor, TextWriter.Null, tracker: tracker);

        var tree = new BehaviorTreeDefinition
        {
            Name = "test",
            Root = new BtSequence
            {
                Children = new List<BtNode>
                {
                    new BtAction { Id = "action_a" },
                    new BtAction { Id = "action_b" },
                }
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
        await btExec.ExecuteOnStreamAsync(tree, stream, new RawResponseHandler(),
            new ReplayContext(), new List<IReplayInterceptor>(), timeoutMs: 100);

        Assert.Contains("action_a", tracker.BtNodesExecuted);
        Assert.Contains("action_b", tracker.BtNodesExecuted);
    }

    [Fact]
    public async Task ExecuteOnStream_WithoutTracker_NoError()
    {
        var catalog = new ActionCatalog { Actions = new() };
        var executor = new ActionExecutor(MinimalProtocol(), catalog);
        var btExec = new BehaviorTreeExecutor(executor, TextWriter.Null); // tracker=null

        var tree = new BehaviorTreeDefinition
        {
            Name = "test",
            Root = new BtAction { Id = "action_x" }
        };

        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, port);
        using var server = await listener.AcceptTcpClientAsync();
        listener.Stop();

        using var stream = client.GetStream();
        await btExec.ExecuteOnStreamAsync(tree, stream, new RawResponseHandler(),
            new ReplayContext(), new List<IReplayInterceptor>(), timeoutMs: 100);
        // No exception = pass
    }
}
