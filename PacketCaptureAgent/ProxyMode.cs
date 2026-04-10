namespace PacketCaptureAgent;

static class ProxyMode
{
    public static async Task RunAsync(Program.CliOptions cli)
    {
        var protocol = Program.LoadProtocol(cli.ProtocolPath);
        if (protocol == null) return;

        var catalog = ActionCatalogBuilder.LoadCatalog(Program.CatalogPath(cli.ProtocolPath!));
        if (catalog == null) { Console.WriteLine("Action Catalog 없음. 먼저 --analyze로 분석하세요."); return; }

        var ep = Program.ParseTarget(cli.Target);
        if (ep == null) return;
        var (host, port) = ep.Value;

        int listenPort = cli.Port ?? 9000;

        var proxy = new ProxyServer(protocol, catalog);
        await proxy.RunAsync(listenPort, host, port, cli.FsmPath, cli.BehaviorPath, cli.Duration);
    }
}
