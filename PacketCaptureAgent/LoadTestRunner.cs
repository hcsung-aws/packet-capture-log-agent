namespace PacketCaptureAgent;

public class LoadTestRunner
{
    public static void Run(
        ProtocolDefinition protocol,
        ScenarioDefinition scenario,
        ActionCatalog catalog,
        string host, int port,
        int clientCount,
        ReplayOptions options)
    {
        Console.WriteLine($"=== Load Test ===\n");
        Console.WriteLine($"시나리오: {scenario.Name}");
        Console.WriteLine($"클라이언트: {clientCount}개");
        Console.WriteLine($"대상: {host}:{port}\n");

        var builder = new ScenarioBuilder();
        var dynamicFields = builder.CollectDynamicFields(scenario, catalog);
        var results = new ReplayResult[clientCount];
        int connected = 0, completed = 0, failed = 0;

        var tasks = new Task[clientCount];
        for (int i = 0; i < clientCount; i++)
        {
            int idx = i;
            tasks[i] = Task.Run(() =>
            {
                var packets = builder.Build(scenario, catalog);
                var sharedState = new Dictionary<string, object>();
                var innerHandler = new ParsingResponseHandler(protocol, host, port, TextWriter.Null);
                var handler = new TrackingResponseHandler(innerHandler, sharedState);
                var interceptors = new List<IReplayInterceptor>();
                if (dynamicFields.Count > 0)
                    interceptors.Add(new DynamicFieldInterceptor(dynamicFields, sharedState));
                interceptors.Add(new NpcAttackInterceptor());

                Interlocked.Increment(ref connected);
                Console.WriteLine($"  [{connected}/{clientCount}] Client {idx + 1} 시작");

                var replayer = new PacketReplayer(protocol);
                results[idx] = replayer.Replay(host, port, packets, handler, options, interceptors, TextWriter.Null);

                if (results[idx].Error != null)
                    Interlocked.Increment(ref failed);
                else
                    Interlocked.Increment(ref completed);

                Console.WriteLine($"  [{completed + failed}/{clientCount}] Client {idx + 1} {(results[idx].Error != null ? "실패" : "완료")}");
            });
        }

        Task.WaitAll(tasks);

        // 요약
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
    }
}
