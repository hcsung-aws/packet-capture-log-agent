namespace PacketCaptureAgent.Tests;

public class PacketObserverTests
{
    private static ActionCatalog MakeCatalog(params (string id, string sendPacket)[] actions) => new()
    {
        Actions = actions.Select(a => new CatalogAction
        {
            Id = a.id,
            Packets = new() { new ActionPacket { Direction = "SEND", Name = a.sendPacket } }
        }).ToList()
    };

    [Fact]
    public void OnSendPacket_UpdatesFsmState()
    {
        var catalog = MakeCatalog(("login", "CS_LOGIN"), ("move", "CS_MOVE"), ("attack", "CS_ATTACK"));
        var observer = new PacketObserver(catalog);

        observer.OnSendPacket("CS_LOGIN");
        Assert.Equal("login", observer.CurrentFsmState);

        observer.OnSendPacket("CS_MOVE");
        Assert.Equal("move", observer.CurrentFsmState);

        observer.OnSendPacket("CS_ATTACK");
        Assert.Equal("attack", observer.CurrentFsmState);
    }

    [Fact]
    public void OnSendPacket_TracksObservedActions()
    {
        var catalog = MakeCatalog(("login", "CS_LOGIN"), ("move", "CS_MOVE"));
        var observer = new PacketObserver(catalog);

        observer.OnSendPacket("CS_LOGIN");
        observer.OnSendPacket("CS_MOVE");
        observer.OnSendPacket("CS_MOVE"); // 중복

        Assert.Contains("login", observer.ObservedActions);
        Assert.Contains("move", observer.ObservedActions);
        Assert.Equal(2, observer.ObservedActions.Count);
    }

    [Fact]
    public void OnSendPacket_UnknownPacket_NoChange()
    {
        var catalog = MakeCatalog(("login", "CS_LOGIN"));
        var observer = new PacketObserver(catalog);

        observer.OnSendPacket("CS_UNKNOWN");

        Assert.Null(observer.CurrentFsmState);
        Assert.Empty(observer.ObservedActions);
    }

    [Fact]
    public void InitialState_IsNull()
    {
        var observer = new PacketObserver(MakeCatalog(("login", "CS_LOGIN")));
        Assert.Null(observer.CurrentFsmState);
    }

    [Fact]
    public void CaseInsensitive_PacketName()
    {
        var catalog = MakeCatalog(("login", "CS_LOGIN"));
        var observer = new PacketObserver(catalog);

        observer.OnSendPacket("cs_login");
        Assert.Equal("login", observer.CurrentFsmState);
    }
}
