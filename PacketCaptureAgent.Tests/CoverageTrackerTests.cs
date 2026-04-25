namespace PacketCaptureAgent.Tests;

public class CoverageTrackerTests
{
    [Fact]
    public void OnSend_TracksUniquePacketNames()
    {
        var tracker = new CoverageTracker();
        tracker.OnSend("CS_LOGIN");
        tracker.OnSend("CS_MOVE");
        tracker.OnSend("CS_LOGIN"); // duplicate

        Assert.Equal(2, tracker.SentPackets.Count);
        Assert.Contains("CS_LOGIN", tracker.SentPackets);
        Assert.Contains("CS_MOVE", tracker.SentPackets);
    }

    [Fact]
    public void OnReceive_TracksUniquePacketNames()
    {
        var tracker = new CoverageTracker();
        tracker.OnReceive("SC_LOGIN_OK");
        tracker.OnReceive("SC_CHAR_INFO");
        tracker.OnReceive("SC_LOGIN_OK");

        Assert.Equal(2, tracker.ReceivedPackets.Count);
    }

    [Fact]
    public void OnFsmTransition_TracksStatesAndTransitions()
    {
        var tracker = new CoverageTracker();
        tracker.OnFsmTransition("connect", "login");
        tracker.OnFsmTransition("login", "move");
        tracker.OnFsmTransition("move", "attack");
        tracker.OnFsmTransition("move", "attack"); // duplicate

        Assert.Equal(4, tracker.FsmStatesVisited.Count); // connect, login, move, attack
        Assert.Equal(3, tracker.FsmTransitions.Count);    // 3 unique transitions
    }

    [Fact]
    public void OnBtNode_TracksUniqueNodes()
    {
        var tracker = new CoverageTracker();
        tracker.OnBtNode("login");
        tracker.OnBtNode("select_char");
        tracker.OnBtNode("login"); // duplicate

        Assert.Equal(2, tracker.BtNodesExecuted.Count);
    }

    [Fact]
    public void EmptyTracker_AllCollectionsEmpty()
    {
        var tracker = new CoverageTracker();

        Assert.Empty(tracker.SentPackets);
        Assert.Empty(tracker.ReceivedPackets);
        Assert.Empty(tracker.FsmStatesVisited);
        Assert.Empty(tracker.FsmTransitions);
        Assert.Empty(tracker.BtNodesExecuted);
    }
}
