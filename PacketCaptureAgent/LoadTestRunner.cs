using System.Text.Json;
using System.Text.Json.Serialization;

namespace PacketCaptureAgent;

public record LoadTestResult(
    int Total, int Success, int Failed,
    long TotalSent, long TotalReceived,
    double AvgDurationSec, double MinDurationSec, double MaxDurationSec,
    string[] Errors)
{
    public string ToJson() => JsonSerializer.Serialize(this, LoadTestResultContext.Default.LoadTestResult);
    public static LoadTestResult FromJson(string json) => JsonSerializer.Deserialize(json, LoadTestResultContext.Default.LoadTestResult)!;
}

[JsonSerializable(typeof(LoadTestResult))]
internal partial class LoadTestResultContext : JsonSerializerContext { }

public class LoadTestRunner
{
    // 진행 상황 (에이전트 모드에서 폴링용)
    public int Connected;
    public int Completed;
    public int FailedCount;
    public int TotalClients;
    public bool IsRunning;

    public async Task<LoadTestResult> RunAsync(
        ProtocolDefinition protocol,
        ScenarioDefinition scenario,
        ActionCatalog catalog,
        string host, int port,
        int clientCount,
        ReplayOptions options,
        string? logDir = null)
    {
        TotalClients = clientCount;
        IsRunning = true;

        Console.WriteLine($"=== Load Test ===\n");
        Console.WriteLine($"시나리오: {scenario.Name}");
        Console.WriteLine($"클라이언트: {clientCount}개");
        Console.WriteLine($"대상: {host}:{port}\n");

        var builder = new ScenarioBuilder();
        var dynamicFields = builder.CollectDynamicFields(scenario, catalog);
        var results = new ReplayResult[clientCount];

        var tasks = new Task[clientCount];
        for (int i = 0; i < clientCount; i++)
        {
            int idx = i;
            tasks[i] = Task.Run(async () =>
            {
                using var logger = logDir != null ? new ReplayLogger(logDir, idx + 1) : null;
                var output = (TextWriter?)logger ?? TextWriter.Null;

                var packets = builder.Build(scenario, catalog);
                var sharedState = new Dictionary<string, object>();
                var innerHandler = new ParsingResponseHandler(protocol, host, port, output);
                var handler = new TrackingResponseHandler(innerHandler, sharedState);
                var interceptors = new List<IReplayInterceptor>();
                if (dynamicFields.Count > 0)
                    interceptors.Add(new DynamicFieldInterceptor(dynamicFields, sharedState));
                interceptors.Add(new ProximityInterceptor(protocol.Semantics?.ProximityActions ?? new()));

                Interlocked.Increment(ref Connected);
                Console.WriteLine($"  [{Connected}/{clientCount}] Client {idx + 1} 시작");

                var replayer = new PacketReplayer(protocol);
                results[idx] = await replayer.ReplayAsync(host, port, packets, handler, options, interceptors, output);

                if (results[idx].Error != null)
                    Interlocked.Increment(ref FailedCount);
                else
                    Interlocked.Increment(ref Completed);

                Console.WriteLine($"  [{Completed + FailedCount}/{clientCount}] Client {idx + 1} {(results[idx].Error != null ? "실패" : "완료")}");
            });
        }

        await Task.WhenAll(tasks);
        IsRunning = false;

        var success = results.Where(r => r.Error == null).ToArray();
        var errors = results.Where(r => r.Error != null).ToArray();

        Console.WriteLine($"\n=== Results ===");
        Console.WriteLine($"클라이언트: {clientCount} (성공: {success.Length}, 실패: {errors.Length})");

        if (success.Length > 0)
        {
            Console.WriteLine($"패킷: sent {success.Sum(r => r.Sent)}, received {success.Sum(r => r.Received)}");
            Console.WriteLine($"소요시간: avg {success.Average(r => r.Duration.TotalSeconds):F1}s, " +
                $"min {success.Min(r => r.Duration.TotalSeconds):F1}s, " +
                $"max {success.Max(r => r.Duration.TotalSeconds):F1}s");
        }

        foreach (var e in errors)
            Console.WriteLine($"  ❌ {e.Error}");

        return new LoadTestResult(
            clientCount, success.Length, errors.Length,
            success.Sum(r => r.Sent), success.Sum(r => r.Received),
            success.Length > 0 ? success.Average(r => r.Duration.TotalSeconds) : 0,
            success.Length > 0 ? success.Min(r => r.Duration.TotalSeconds) : 0,
            success.Length > 0 ? success.Max(r => r.Duration.TotalSeconds) : 0,
            errors.Select(r => r.Error!).ToArray());
    }
}
