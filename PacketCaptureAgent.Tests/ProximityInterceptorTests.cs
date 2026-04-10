namespace PacketCaptureAgent.Tests;

public class ProximityInterceptorTests
{
    [Theory]
    [InlineData(0, 0, 3, 3, 3, 2)]   // 아래쪽이 가장 가까움
    [InlineData(5, 5, 5, 3, 5, 4)]   // 바로 위 → (5,4)
    [InlineData(0, 0, 2, 0, 1, 0)]   // 왼쪽이 가장 가까움
    [InlineData(10, 10, 10, 10, 10, 9)] // 같은 위치 → 위쪽 (0,-1) 먼저
    public void FindBestPos_ReturnsClosestAdjacentToNpc(
        int px, int py, int npcX, int npcY, int expectedX, int expectedY)
    {
        var (x, y) = ProximityInterceptor.FindBestPos(px, py, npcX, npcY);
        Assert.Equal(expectedX, x);
        Assert.Equal(expectedY, y);
    }

    [Fact]
    public void FindBestPos_SymmetricCase_PicksSmallestDistance()
    {
        // 플레이어 (0,5), NPC (5,5) → 인접 4칸 중 (4,5)가 가장 가까움
        var (x, y) = ProximityInterceptor.FindBestPos(0, 5, 5, 5);
        Assert.Equal(4, x);
        Assert.Equal(5, y);
    }
}
