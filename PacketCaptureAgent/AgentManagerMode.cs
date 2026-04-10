namespace PacketCaptureAgent;

static class AgentMode
{
    public static void Run(Program.CliOptions cli)
    {
        var protocol = Program.LoadProtocol(cli.ProtocolPath);
        if (protocol == null) return;
        new AgentServer(protocol, cli.ProtocolPath!).Run(cli.AgentPort);
    }
}

static class ManagerMode
{
    public static async Task RunAsync(Program.CliOptions cli)
    {
        if (string.IsNullOrEmpty(cli.Target)) { Console.WriteLine("대상 서버 필요: -t host:port"); return; }
        if (cli.ScenarioPath == null) { Console.WriteLine("시나리오 필요: -s scenario.json"); return; }
        await ManagerRunner.RunAsync(cli.ManagerPath!, cli.Target, cli.ScenarioPath, cli.Clients, cli.Mode, cli.Speed);
    }
}
