namespace PacketCaptureAgent.Tests;

public class CoverageReportTests
{
    [Fact]
    public void PacketCoverage_CalculatesCorrectly()
    {
        var tracker = new CoverageTracker();
        tracker.OnSend("CS_LOGIN");
        tracker.OnReceive("SC_LOGIN_OK");
        tracker.OnSend("CS_MOVE");

        var protocol = new ProtocolDefinition
        {
            Packets = new List<PacketDefinition>
            {
                new() { Name = "CS_LOGIN" },
                new() { Name = "SC_LOGIN_OK" },
                new() { Name = "CS_MOVE" },
                new() { Name = "CS_ATTACK" },
                new() { Name = "SC_DAMAGE" },
            }
        };

        var report = CoverageReport.Generate(tracker, protocol);

        Assert.Single(report.Sections);
        var pkt = report.Sections[0];
        Assert.Equal("Packet Types", pkt.Name);
        Assert.Equal(5, pkt.Total);
        Assert.Equal(3, pkt.Covered);
        Assert.Equal(2, pkt.Missing.Count);
        Assert.Contains("CS_ATTACK", pkt.Missing);
        Assert.Contains("SC_DAMAGE", pkt.Missing);
    }

    [Fact]
    public void FsmCoverage_StatesAndTransitions()
    {
        var tracker = new CoverageTracker();
        tracker.OnFsmTransition("connect", "login");
        tracker.OnFsmTransition("login", "move");

        var fsm = new FsmDefinition
        {
            Transitions = new Dictionary<string, Dictionary<string, float>>
            {
                ["connect"] = new() { ["login"] = 1.0f },
                ["login"] = new() { ["move"] = 0.7f, ["shop"] = 0.3f },
                ["move"] = new() { ["attack"] = 0.5f, ["move"] = 0.5f },
            }
        };

        var report = CoverageReport.Generate(tracker, new ProtocolDefinition(), fsm);

        var states = report.Sections.First(s => s.Name == "FSM States");
        Assert.Equal(3, states.Total);
        Assert.Equal(3, states.Covered); // connect, login, move all visited
        Assert.Empty(states.Missing);

        var trans = report.Sections.First(s => s.Name == "FSM Transitions");
        Assert.Equal(5, trans.Total);
        Assert.Equal(2, trans.Covered);
        Assert.Equal(3, trans.Missing.Count);
    }

    [Fact]
    public void BtCoverage_ActionNodes()
    {
        var tracker = new CoverageTracker();
        tracker.OnBtNode("login");
        tracker.OnBtNode("select_char");

        var bt = new BehaviorTreeDefinition
        {
            Name = "test",
            Root = new BtSequence
            {
                Children = new List<BtNode>
                {
                    new BtAction { Id = "login" },
                    new BtAction { Id = "select_char" },
                    new BtAction { Id = "enter_world" },
                }
            }
        };

        var report = CoverageReport.Generate(tracker, new ProtocolDefinition(), bt: bt);

        var nodes = report.Sections.First(s => s.Name == "BT Nodes");
        Assert.Equal(3, nodes.Total);
        Assert.Equal(2, nodes.Covered);
        Assert.Single(nodes.Missing);
        Assert.Contains("enter_world", nodes.Missing);
    }

    [Fact]
    public void PrintToConsole_FormatsCorrectly()
    {
        var tracker = new CoverageTracker();
        tracker.OnSend("CS_LOGIN");

        var protocol = new ProtocolDefinition
        {
            Packets = new List<PacketDefinition>
            {
                new() { Name = "CS_LOGIN" },
                new() { Name = "CS_MOVE" },
            }
        };

        var report = CoverageReport.Generate(tracker, protocol);
        var sw = new StringWriter();
        report.PrintToConsole(sw);
        var output = sw.ToString();

        Assert.Contains("Coverage Report", output);
        Assert.Contains("1/2", output);
        Assert.Contains("50.0%", output);
        Assert.Contains("CS_MOVE", output);
    }

    [Fact]
    public void EmptyTracker_ZeroCoverage()
    {
        var tracker = new CoverageTracker();
        var protocol = new ProtocolDefinition
        {
            Packets = new List<PacketDefinition>
            {
                new() { Name = "CS_LOGIN" },
            }
        };

        var report = CoverageReport.Generate(tracker, protocol);
        Assert.Equal(0, report.Sections[0].Covered);
        Assert.Equal(1, report.Sections[0].Total);
    }
}
