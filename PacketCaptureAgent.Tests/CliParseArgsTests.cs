namespace PacketCaptureAgent.Tests;

/// <summary>
/// Program.ParseArgs tests — CLI 인자 파싱 검증.
/// </summary>
public class CliParseArgsTests
{
    [Fact]
    public void ParseArgs_Empty_ReturnsDefaults()
    {
        var opts = Program.ParseArgs([]);

        Assert.Null(opts.ProtocolPath);
        Assert.Null(opts.ReplayLog);
        Assert.Null(opts.Target);
        Assert.Equal("hybrid", opts.Mode);
        Assert.Equal(5000, opts.Timeout);
        Assert.Equal(1.0, opts.Speed);
        Assert.Null(opts.Port);
        Assert.False(opts.ShowHelp);
    }

    [Fact]
    public void ParseArgs_Protocol_ShortFlag()
    {
        var opts = Program.ParseArgs(["-p", "proto.json"]);
        Assert.Equal("proto.json", opts.ProtocolPath);
    }

    [Fact]
    public void ParseArgs_Protocol_LongFlag()
    {
        var opts = Program.ParseArgs(["--protocol", "proto.json"]);
        Assert.Equal("proto.json", opts.ProtocolPath);
    }

    [Fact]
    public void ParseArgs_ReplayMode_AllOptions()
    {
        var opts = Program.ParseArgs([
            "-p", "proto.json",
            "-r", "capture.log",
            "-t", "host:1234",
            "--mode", "timing",
            "--timeout", "3000",
            "--speed", "2.0"
        ]);

        Assert.Equal("proto.json", opts.ProtocolPath);
        Assert.Equal("capture.log", opts.ReplayLog);
        Assert.Equal("host:1234", opts.Target);
        Assert.Equal("timing", opts.Mode);
        Assert.Equal(3000, opts.Timeout);
        Assert.Equal(2.0, opts.Speed);
    }

    [Fact]
    public void ParseArgs_Help()
    {
        Assert.True(Program.ParseArgs(["-h"]).ShowHelp);
        Assert.True(Program.ParseArgs(["--help"]).ShowHelp);
    }

    [Fact]
    public void ParseArgs_Port()
    {
        var opts = Program.ParseArgs(["--port", "9000"]);
        Assert.Equal(9000, opts.Port);
    }

    [Fact]
    public void ParseArgs_Port_WithProtocol()
    {
        var opts = Program.ParseArgs(["-p", "proto.json", "--port", "8080"]);
        Assert.Equal("proto.json", opts.ProtocolPath);
        Assert.Equal(8080, opts.Port);
    }

    [Fact]
    public void ParseArgs_Port_InvalidNumber_Ignored()
    {
        var opts = Program.ParseArgs(["--port", "abc"]);
        Assert.Null(opts.Port);
    }

    [Fact]
    public void ParseArgs_MissingValue_Ignored()
    {
        // --port가 마지막 인자이고 값이 없으면 무시
        var opts = Program.ParseArgs(["--port"]);
        Assert.Null(opts.Port);
    }

    [Fact]
    public void ParseArgs_UnknownArgs_Ignored()
    {
        var opts = Program.ParseArgs(["--unknown", "value", "-p", "proto.json"]);
        Assert.Equal("proto.json", opts.ProtocolPath);
    }

    [Fact]
    public void ParseArgs_Coverage_Flag()
    {
        var opts = Program.ParseArgs(["--coverage"]);
        Assert.True(opts.Coverage);
        Assert.Null(opts.CoverageOutput);
    }

    [Fact]
    public void ParseArgs_CoverageOutput_WithPath()
    {
        var opts = Program.ParseArgs(["--coverage", "--coverage-output", "report.json"]);
        Assert.True(opts.Coverage);
        Assert.Equal("report.json", opts.CoverageOutput);
    }

    [Fact]
    public void ParseArgs_CoverageDefaults()
    {
        var opts = Program.ParseArgs([]);
        Assert.False(opts.Coverage);
        Assert.Null(opts.CoverageOutput);
    }
}
