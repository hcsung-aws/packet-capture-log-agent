using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace PacketCaptureAgent;

/// <summary>JSON Lines 파일 로거. CloudWatch Logs 호환 포맷, 날짜별 롤링.
/// TextWriter를 상속하여 기존 출력 경로에 그대로 연결 가능.</summary>
public class ReplayLogger : TextWriter
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };
    private readonly TextWriter? _console;
    private readonly string _logDir;
    private readonly int? _clientId;
    private StreamWriter? _file;
    private string _currentDate = "";
    private readonly object _lock = new();

    public override Encoding Encoding => Encoding.UTF8;

    public ReplayLogger(string logDir, int? clientId = null, TextWriter? console = null)
    {
        _logDir = logDir;
        _clientId = clientId;
        _console = console;
        Directory.CreateDirectory(logDir);
    }

    public override void WriteLine(string? value)
    {
        _console?.WriteLine(value);
        if (string.IsNullOrEmpty(value)) return;

        var now = DateTime.Now;
        var entry = new LogEntry { ts = now.ToString("o"), msg = value.TrimStart() };
        if (_clientId.HasValue) entry.client = _clientId.Value;
        var json = JsonSerializer.Serialize(entry, JsonOpts);

        lock (_lock)
        {
            EnsureFile(now);
            _file!.WriteLine(json);
            _file.Flush();
        }
    }

    private void EnsureFile(DateTime now)
    {
        var date = now.ToString("yyyy-MM-dd");
        if (date == _currentDate && _file != null) return;
        _file?.Dispose();
        _currentDate = date;
        var path = Path.Combine(_logDir, $"replay_{date}.jsonl");
        var fs = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
        _file = new StreamWriter(fs, Encoding.UTF8);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { lock (_lock) _file?.Dispose(); }
        base.Dispose(disposing);
    }

    private class LogEntry
    {
        public string ts { get; set; } = "";
        public int? client { get; set; }
        public string msg { get; set; } = "";
    }
}
