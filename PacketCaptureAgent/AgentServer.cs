using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PacketCaptureAgent;

/// <summary>에이전트 HTTP 서버. 매니저로부터 부하 테스트 명령을 수신하여 실행.</summary>
public class AgentServer
{
    private readonly ProtocolDefinition _protocol;
    private readonly string _protocolPath;
    private LoadTestRunner? _runner;
    private LoadTestResult? _result;
    private string? _error;

    public AgentServer(ProtocolDefinition protocol, string protocolPath)
    {
        _protocol = protocol;
        _protocolPath = protocolPath;
    }

    public void Run(int port)
    {
        var listener = new HttpListener();
        listener.Prefixes.Add($"http://+:{port}/");
        listener.Start();
        Console.WriteLine($"=== Agent Mode ===");
        Console.WriteLine($"Port: {port}");
        Console.WriteLine($"Protocol: {_protocolPath}");
        Console.WriteLine("Ctrl+C로 종료\n");

        while (listener.IsListening)
        {
            try { HandleRequest(listener.GetContext()); }
            catch (HttpListenerException) { break; }
        }
    }

    private void HandleRequest(HttpListenerContext ctx)
    {
        var path = ctx.Request.Url?.AbsolutePath ?? "/";
        var method = ctx.Request.HttpMethod;

        try
        {
            if (method == "POST" && path == "/run") HandleRun(ctx);
            else if (method == "GET" && path == "/status") HandleStatus(ctx);
            else if (method == "GET" && path == "/result") HandleResult(ctx);
            else Respond(ctx, 404, """{"error":"not found"}""");
        }
        catch (Exception ex)
        {
            Respond(ctx, 500, JsonSerializer.Serialize(new { error = ex.Message }));
        }
    }

    private void HandleRun(HttpListenerContext ctx)
    {
        if (_runner is { IsRunning: true })
        {
            Respond(ctx, 409, """{"error":"already running"}""");
            return;
        }

        using var reader = new StreamReader(ctx.Request.InputStream);
        var body = reader.ReadToEnd();
        var req = JsonSerializer.Deserialize(body, AgentRunRequestContext.Default.AgentRunRequest);
        if (req == null) { Respond(ctx, 400, """{"error":"invalid body"}"""); return; }

        // 시나리오 + 카탈로그 로드
        var catalogPath = Path.Combine(Path.GetDirectoryName(_protocolPath) ?? ".", "..", "actions",
            $"{Path.GetFileNameWithoutExtension(_protocolPath)}_actions.json");
        var catalog = ActionCatalogBuilder.LoadCatalog(catalogPath);
        if (catalog == null) { Respond(ctx, 400, $"{{\"error\":\"catalog not found: {catalogPath}\"}}"); return; }

        var scenario = ScenarioBuilder.Load(req.Scenario);
        if (scenario == null) { Respond(ctx, 400, $"{{\"error\":\"scenario not found: {req.Scenario}\"}}"); return; }

        var options = new ReplayOptions
        {
            Mode = req.Mode switch { "timing" => ReplayMode.Timing, "response" => ReplayMode.Response, _ => ReplayMode.Hybrid },
            Speed = req.Speed
        };

        var parts = req.Target.Split(':');
        var host = parts[0];
        var port = int.Parse(parts[1]);
        var logDir = Path.Combine(Path.GetDirectoryName(_protocolPath) ?? ".", "..", "logs");

        _result = null;
        _error = null;
        _runner = new LoadTestRunner();

        _ = Task.Run(async () =>
        {
            try { _result = await _runner.RunAsync(_protocol, scenario, catalog, host, port, req.Clients, options, logDir); }
            catch (Exception ex) { _error = ex.Message; _runner.IsRunning = false; }
        });

        Respond(ctx, 200, """{"status":"started"}""");
    }

    private void HandleStatus(HttpListenerContext ctx)
    {
        if (_runner == null) { Respond(ctx, 200, """{"status":"idle"}"""); return; }

        var status = _runner.IsRunning ? "running" : (_error != null ? "failed" : "completed");
        var json = JsonSerializer.Serialize(new
        {
            status,
            connected = _runner.Connected,
            completed = _runner.Completed,
            failed = _runner.FailedCount,
            total = _runner.TotalClients
        });
        Respond(ctx, 200, json);
    }

    private void HandleResult(HttpListenerContext ctx)
    {
        if (_error != null) { Respond(ctx, 200, JsonSerializer.Serialize(new { error = _error })); return; }
        if (_result == null) { Respond(ctx, 200, """{"error":"no result yet"}"""); return; }
        Respond(ctx, 200, _result.ToJson());
    }

    private static void Respond(HttpListenerContext ctx, int code, string json)
    {
        ctx.Response.StatusCode = code;
        ctx.Response.ContentType = "application/json";
        var buf = Encoding.UTF8.GetBytes(json);
        ctx.Response.ContentLength64 = buf.Length;
        ctx.Response.OutputStream.Write(buf);
        ctx.Response.Close();
    }
}

public record AgentRunRequest(string Target, int Clients, string Scenario, string Mode = "hybrid", double Speed = 1.0);

[JsonSerializable(typeof(AgentRunRequest))]
internal partial class AgentRunRequestContext : JsonSerializerContext { }
