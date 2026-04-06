using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PacketCaptureAgent;

public record AgentEntry(string Url);

[JsonSerializable(typeof(AgentEntry[]))]
internal partial class AgentEntryContext : JsonSerializerContext { }

/// <summary>매니저: 에이전트들에 부하 테스트를 분배하고 결과를 집계.</summary>
public class ManagerRunner
{
    public static async Task RunAsync(string agentsJsonPath, string target, string scenario,
        int totalClients, string mode = "hybrid", double speed = 1.0)
    {
        var agents = JsonSerializer.Deserialize(File.ReadAllText(agentsJsonPath), AgentEntryContext.Default.AgentEntryArray);
        if (agents == null || agents.Length == 0) { Console.WriteLine("에이전트 목록이 비어 있습니다."); return; }

        int perAgent = totalClients / agents.Length;
        int remainder = totalClients % agents.Length;

        Console.WriteLine($"=== Manager Mode ===");
        Console.WriteLine($"에이전트: {agents.Length}개");
        Console.WriteLine($"클라이언트: {totalClients} ({perAgent}~{perAgent + 1}/에이전트)");
        Console.WriteLine($"대상: {target}\n");

        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };

        // POST /run to all agents
        for (int i = 0; i < agents.Length; i++)
        {
            int clients = perAgent + (i < remainder ? 1 : 0);
            var body = JsonSerializer.Serialize(new AgentRunRequest(target, clients, scenario, mode, speed),
                AgentRunRequestContext.Default.AgentRunRequest);
            try
            {
                var res = await http.PostAsync($"{agents[i].Url.TrimEnd('/')}/run", new StringContent(body, Encoding.UTF8, "application/json"));
                Console.WriteLine($"  [{i + 1}] {agents[i].Url} → {clients} clients ({res.StatusCode})");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  [{i + 1}] {agents[i].Url} → 연결 실패: {ex.Message}");
            }
        }

        Console.WriteLine("\n폴링 시작...\n");

        // Poll until all done
        var done = new bool[agents.Length];
        while (!done.All(d => d))
        {
            await Task.Delay(2000);
            for (int i = 0; i < agents.Length; i++)
            {
                if (done[i]) continue;
                try
                {
                    var json = await http.GetStringAsync($"{agents[i].Url.TrimEnd('/')}/status");
                    using var doc = JsonDocument.Parse(json);
                    var status = doc.RootElement.GetProperty("status").GetString();
                    var completed = doc.RootElement.TryGetProperty("completed", out var c) ? c.GetInt32() : 0;
                    var failed = doc.RootElement.TryGetProperty("failed", out var f) ? f.GetInt32() : 0;
                    var total = doc.RootElement.TryGetProperty("total", out var t) ? t.GetInt32() : 0;

                    Console.WriteLine($"  [{i + 1}] {status} ({completed + failed}/{total})");
                    if (status == "completed" || status == "failed") done[i] = true;
                }
                catch { /* 폴링 실패 무시, 재시도 */ }
            }
        }

        // Collect results
        Console.WriteLine("\n결과 수집...\n");
        var results = new List<LoadTestResult>();
        for (int i = 0; i < agents.Length; i++)
        {
            try
            {
                var json = await http.GetStringAsync($"{agents[i].Url.TrimEnd('/')}/result");
                var result = LoadTestResult.FromJson(json);
                results.Add(result);
                Console.WriteLine($"  [{i + 1}] 성공:{result.Success} 실패:{result.Failed}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  [{i + 1}] 결과 수집 실패: {ex.Message}");
            }
        }

        // Aggregate
        if (results.Count == 0) { Console.WriteLine("\n수집된 결과 없음."); return; }

        var totalSuccess = results.Sum(r => r.Success);
        var totalFailed = results.Sum(r => r.Failed);
        var totalSent = results.Sum(r => r.TotalSent);
        var totalRecv = results.Sum(r => r.TotalReceived);
        var allErrors = results.SelectMany(r => r.Errors).ToArray();

        var successResults = results.Where(r => r.Success > 0).ToArray();
        double avgDur = successResults.Length > 0 ? successResults.Average(r => r.AvgDurationSec) : 0;
        double minDur = successResults.Length > 0 ? successResults.Min(r => r.MinDurationSec) : 0;
        double maxDur = successResults.Length > 0 ? successResults.Max(r => r.MaxDurationSec) : 0;

        Console.WriteLine($"\n=== Aggregated Results ===");
        Console.WriteLine($"에이전트: {agents.Length} (응답: {results.Count})");
        Console.WriteLine($"클라이언트: {totalSuccess + totalFailed} (성공: {totalSuccess}, 실패: {totalFailed})");
        Console.WriteLine($"패킷: sent {totalSent}, received {totalRecv}");
        Console.WriteLine($"소요시간: avg {avgDur:F1}s, min {minDur:F1}s, max {maxDur:F1}s");

        if (allErrors.Length > 0)
        {
            Console.WriteLine($"\n에러 ({allErrors.Length}건):");
            foreach (var e in allErrors.Take(10))
                Console.WriteLine($"  ❌ {e}");
            if (allErrors.Length > 10)
                Console.WriteLine($"  ... 외 {allErrors.Length - 10}건");
        }
    }
}
