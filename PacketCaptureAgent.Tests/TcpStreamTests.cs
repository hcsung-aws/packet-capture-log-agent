using System.Net;

namespace PacketCaptureAgent.Tests;

/// <summary>
/// TcpStream characterization tests — 현재 동작을 캡처하여 변경 시 사이드이펙트 감지.
/// </summary>
public class TcpStreamTests
{
    private static readonly ConnectionKey DummyKey = new(IPAddress.Loopback, 1000, IPAddress.Loopback, 2000);

    [Fact]
    public void Append_TryRead_BasicFlow()
    {
        var stream = new TcpStream(DummyKey);
        stream.Append(new byte[] { 1, 2, 3, 4 });

        Assert.Equal(4, stream.Available);

        var buf = new byte[4];
        Assert.True(stream.TryRead(buf));
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, buf);
        Assert.Equal(0, stream.Available);
    }

    [Fact]
    public void TryPeek_DoesNotConsume()
    {
        var stream = new TcpStream(DummyKey);
        stream.Append(new byte[] { 10, 20, 30 });

        var buf = new byte[2];
        Assert.True(stream.TryPeek(buf));
        Assert.Equal(new byte[] { 10, 20 }, buf);
        Assert.Equal(3, stream.Available); // 소비되지 않음
    }

    [Fact]
    public void TryRead_InsufficientData_ReturnsFalse()
    {
        var stream = new TcpStream(DummyKey);
        stream.Append(new byte[] { 1, 2 });

        var buf = new byte[4];
        Assert.False(stream.TryRead(buf));
        Assert.Equal(2, stream.Available); // 데이터 유지
    }

    [Fact]
    public void TryPeek_InsufficientData_ReturnsFalse()
    {
        var stream = new TcpStream(DummyKey);
        stream.Append(new byte[] { 1 });

        var buf = new byte[3];
        Assert.False(stream.TryPeek(buf));
    }

    [Fact]
    public void TryRead_ResetsPositions_WhenFullyConsumed()
    {
        var stream = new TcpStream(DummyKey);
        stream.Append(new byte[] { 1, 2, 3 });

        var buf = new byte[3];
        stream.TryRead(buf);
        Assert.Equal(0, stream.Available);

        // 완전 소비 후 다시 쓰기 가능
        stream.Append(new byte[] { 4, 5 });
        Assert.Equal(2, stream.Available);

        var buf2 = new byte[2];
        Assert.True(stream.TryRead(buf2));
        Assert.Equal(new byte[] { 4, 5 }, buf2);
    }

    [Fact]
    public void Compact_FreesReadSpace()
    {
        var stream = new TcpStream(DummyKey);

        // 버퍼를 상당량 채운 뒤 일부 읽기 → Compact로 공간 확보
        var chunk = new byte[30000];
        Array.Fill<byte>(chunk, 0xAA);
        stream.Append(chunk);

        var readBuf = new byte[20000];
        stream.TryRead(readBuf); // 20000 읽음 → readPos=20000

        // 남은 10000 + 새 40000 = 50000 < 65536 → Compact 후 들어감
        var chunk2 = new byte[40000];
        Array.Fill<byte>(chunk2, 0xBB);
        stream.Append(chunk2);

        Assert.Equal(50000, stream.Available);
    }

    [Fact]
    public void MultipleAppend_ThenRead()
    {
        var stream = new TcpStream(DummyKey);
        stream.Append(new byte[] { 1, 2 });
        stream.Append(new byte[] { 3, 4 });

        Assert.Equal(4, stream.Available);

        var buf = new byte[4];
        Assert.True(stream.TryRead(buf));
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, buf);
    }

    [Fact]
    public void Append_ExceedsBuffer_GrowsAutomatically()
    {
        // Bug #1 수정 후: Compact 후에도 공간 부족 시 버퍼 확장
        var stream = new TcpStream(DummyKey);

        var bigChunk = new byte[60000];
        Array.Fill<byte>(bigChunk, 0xAA);
        stream.Append(bigChunk);

        var overflow = new byte[10000];
        Array.Fill<byte>(overflow, 0xBB);
        stream.Append(overflow); // 예외 없이 확장

        Assert.Equal(70000, stream.Available);

        // 데이터 무결성 확인
        var readBuf = new byte[70000];
        Assert.True(stream.TryRead(readBuf));
        Assert.Equal(0xAA, readBuf[0]);
        Assert.Equal(0xAA, readBuf[59999]);
        Assert.Equal(0xBB, readBuf[60000]);
        Assert.Equal(0xBB, readBuf[69999]);
    }
}
