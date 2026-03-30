using System.Net;
using System.Text;
using System.Text.Json;

namespace PacketCaptureAgent;

/// <summary>BT 웹 에디터. HttpListener 기반 REST API + 정적 HTML 서빙.</summary>
public class BehaviorTreeWebEditor
{
    private readonly string _btPath;
    private readonly string _htmlDir;
    private BehaviorTreeDefinition _tree;

    public BehaviorTreeWebEditor(string btPath)
    {
        _btPath = btPath;
        _tree = BehaviorTreeDefinition.Load(btPath);
        _htmlDir = Path.Combine(AppContext.BaseDirectory, "wwwroot");
        // fallback: source tree의 wwwroot
        if (!Directory.Exists(_htmlDir))
            _htmlDir = Path.Combine(Path.GetDirectoryName(typeof(BehaviorTreeWebEditor).Assembly.Location) ?? ".", "wwwroot");
    }

    public void Run(int port = 8080)
    {
        var listener = new HttpListener();
        listener.Prefixes.Add($"http://localhost:{port}/");
        listener.Start();
        Console.WriteLine($"BT Web Editor: http://localhost:{port}/");
        Console.WriteLine($"BT 파일: {_btPath}");
        Console.WriteLine("Ctrl+C로 종료\n");

        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo($"http://localhost:{port}/") { UseShellExecute = true }); }
        catch { /* 브라우저 자동 오픈 실패 무시 */ }

        while (listener.IsListening)
        {
            try
            {
                var ctx = listener.GetContext();
                HandleRequest(ctx);
            }
            catch (HttpListenerException) { break; }
        }
    }

    private void HandleRequest(HttpListenerContext ctx)
    {
        var req = ctx.Request;
        var res = ctx.Response;
        res.Headers.Add("Access-Control-Allow-Origin", "*");
        res.Headers.Add("Access-Control-Allow-Methods", "GET,PUT,POST,DELETE,OPTIONS");
        res.Headers.Add("Access-Control-Allow-Headers", "Content-Type");

        if (req.HttpMethod == "OPTIONS") { res.StatusCode = 204; res.Close(); return; }

        try
        {
            var path = req.Url?.AbsolutePath ?? "/";
            if (path == "/" || path == "/index.html")
                ServeFile(res, "editor.html", "text/html");
            else if (path == "/api/tree" && req.HttpMethod == "GET")
                Json(res, _tree);
            else if (path.StartsWith("/api/node/") && req.HttpMethod == "PUT")
                HandleNodeUpdate(req, res, path);
            else if (path.StartsWith("/api/node/") && req.HttpMethod == "DELETE")
                HandleNodeDelete(res, path);
            else if (path == "/api/save" && req.HttpMethod == "POST")
                HandleSave(res);
            else
                { res.StatusCode = 404; res.Close(); }
        }
        catch (Exception ex)
        {
            res.StatusCode = 500;
            var bytes = Encoding.UTF8.GetBytes(ex.Message);
            res.OutputStream.Write(bytes);
            res.Close();
        }
    }

    private void HandleNodeUpdate(HttpListenerRequest req, HttpListenerResponse res, string path)
    {
        // /api/node/{idx}/{field}
        var parts = path.Split('/');
        if (parts.Length < 5 || !int.TryParse(parts[3], out var idx))
            { res.StatusCode = 400; res.Close(); return; }

        var field = parts[4];
        var body = new StreamReader(req.InputStream).ReadToEnd();
        var json = JsonDocument.Parse(body).RootElement;
        var nodes = IndexNodes(_tree.Root);

        if (idx < 0 || idx >= nodes.Count) { res.StatusCode = 404; res.Close(); return; }

        switch (field)
        {
            case "condition":
                var cond = json.GetProperty("value").GetString();
                nodes[idx].Condition = string.IsNullOrWhiteSpace(cond) ? null : cond;
                break;
            case "weight":
                nodes[idx].Weight = (float)json.GetProperty("value").GetDouble();
                break;
            default:
                res.StatusCode = 400; res.Close(); return;
        }

        Json(res, new { ok = true });
    }

    private void HandleNodeDelete(HttpListenerResponse res, string path)
    {
        var parts = path.Split('/');
        if (parts.Length < 4 || !int.TryParse(parts[3], out var idx))
            { res.StatusCode = 400; res.Close(); return; }

        var nodes = IndexNodes(_tree.Root);
        if (idx < 0 || idx >= nodes.Count) { res.StatusCode = 404; res.Close(); return; }

        _tree.Root = RemoveFromTree(_tree.Root, nodes[idx]) ?? new BtSequence();
        Json(res, new { ok = true });
    }

    private void HandleSave(HttpListenerResponse res)
    {
        _tree.Save(_btPath);
        Console.WriteLine($"저장 완료: {_btPath}");
        Json(res, new { ok = true, path = _btPath });
    }

    private List<BtNode> IndexNodes(BtNode node)
    {
        var list = new List<BtNode>();
        Collect(node, list);
        return list;
    }

    private void Collect(BtNode node, List<BtNode> list)
    {
        list.Add(node);
        switch (node)
        {
            case BtSequence s: foreach (var c in s.Children) Collect(c, list); break;
            case BtSelector s: foreach (var c in s.Children) Collect(c, list); break;
            case BtRepeat r: Collect(r.Child, list); break;
        }
    }

    private static BtNode? RemoveFromTree(BtNode node, BtNode target)
    {
        if (node == target) return null;
        switch (node)
        {
            case BtSequence s:
                s.Children = s.Children.Select(c => RemoveFromTree(c, target)).Where(c => c != null).ToList()!;
                return s.Children.Count == 0 ? null : s;
            case BtSelector s:
                s.Children = s.Children.Select(c => RemoveFromTree(c, target)).Where(c => c != null).ToList()!;
                return s.Children.Count == 0 ? null : s;
            case BtRepeat r:
                var child = RemoveFromTree(r.Child, target);
                return child == null ? null : (r.Child = child, r).r;
            default: return node;
        }
    }

    private void ServeFile(HttpListenerResponse res, string filename, string contentType)
    {
        var filePath = Path.Combine(_htmlDir, filename);
        if (!File.Exists(filePath)) { res.StatusCode = 404; res.Close(); return; }
        res.ContentType = $"{contentType}; charset=utf-8";
        var bytes = File.ReadAllBytes(filePath);
        res.OutputStream.Write(bytes);
        res.Close();
    }

    private static void Json(HttpListenerResponse res, object data)
    {
        res.ContentType = "application/json; charset=utf-8";
        var bytes = JsonSerializer.SerializeToUtf8Bytes(data, BehaviorTreeDefinition.JsonOpts);
        res.OutputStream.Write(bytes);
        res.Close();
    }
}
