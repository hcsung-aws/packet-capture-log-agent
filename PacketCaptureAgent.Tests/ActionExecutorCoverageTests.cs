using System.Net;
using System.Net.Sockets;

namespace PacketCaptureAgent.Tests;

/// <summary>ActionExecutor + CoverageTracker 연결 테스트.</summary>
public class ActionExecutorCoverageTests
{
    private static ProtocolDefinition CreateProtocol() => new()
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
        Packets = new List<PacketDefinition>
        {
            new() { Type = 1, Name = "CS_LOGIN", Fields = new List<FieldDefinition>() },
        }
    };

    private static ActionCatalog CreateCatalog() => new()
    {
        Actions = new List<CatalogAction>
        {
            new()
            {
                Id = "login", Name = "Login",
                Packets = new List<ActionPacket>
                {
                    new() { Direction = "SEND", Name = "CS_LOGIN", Role = "request" },
                    new() { Direction = "RECV", Name = "SC_LOGIN_OK", Role = "response" },
                }
            }
        }
    };

    [Fact]
    public async Task ExecuteAsync_WithTracker_TracksSendAndRecv()
    {
        var tracker = new CoverageTracker();
        var protocol = CreateProtocol();
        var executor = new ActionExecutor(protocol, CreateCatalog(), tracker: tracker);

        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, port);
        using var serverSide = await listener.AcceptTcpClientAsync();
        listener.Stop();

        var handler = new RawResponseHandler();
        var context = new ReplayContext();
        using var stream = client.GetStream();

        await executor.ExecuteAsync("login", stream, handler, context,
            new List<IReplayInterceptor>(), TextWriter.Null, timeoutMs: 100);

        Assert.Contains("CS_LOGIN", tracker.SentPackets);
        Assert.Contains("SC_LOGIN_OK", tracker.ReceivedPackets);
    }

    [Fact]
    public async Task ExecuteAsync_WithoutTracker_NoError()
    {
        var protocol = CreateProtocol();
        var executor = new ActionExecutor(protocol, CreateCatalog()); // tracker=null

        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, port);
        using var serverSide = await listener.AcceptTcpClientAsync();
        listener.Stop();

        var handler = new RawResponseHandler();
        var context = new ReplayContext();
        using var stream = client.GetStream();

        var result = await executor.ExecuteAsync("login", stream, handler, context,
            new List<IReplayInterceptor>(), TextWriter.Null, timeoutMs: 100);

        Assert.True(result);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownAction_ReturnsFalse_NoTracking()
    {
        var tracker = new CoverageTracker();
        var executor = new ActionExecutor(CreateProtocol(), CreateCatalog(), tracker: tracker);

        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, port);
        using var serverSide = await listener.AcceptTcpClientAsync();
        listener.Stop();

        using var stream = client.GetStream();
        var result = await executor.ExecuteAsync("nonexistent", stream,
            new RawResponseHandler(), new ReplayContext(),
            new List<IReplayInterceptor>(), TextWriter.Null, timeoutMs: 100);

        Assert.False(result);
        Assert.Empty(tracker.SentPackets);
        Assert.Empty(tracker.ReceivedPackets);
    }
}
