using System.Text;
using Xunit;

namespace BellTower.Tests;

public sealed class ProgramConsoleTests
{
    private const int PipeNotConnected = unchecked((int)0x800700E9);

    [Fact]
    public void Output_setter_io_failure_is_tolerated_and_input_is_still_set()
    {
        Encoding? input = null;
        Program.UseUtf8Console(_ => throw new IOException("No process is on the other end of the pipe.", PipeNotConnected), e => input = e);
        Assert.NotNull(input);
    }

    [Fact]
    public void Input_setter_io_failure_is_tolerated()
    {
        Encoding? output = null;
        Program.UseUtf8Console(e => output = e, _ => throw new IOException("closed", PipeNotConnected));
        Assert.NotNull(output);
    }

    [Fact]
    public void Healthy_console_receives_bomless_utf8_on_both()
    {
        Encoding? output = null, input = null;
        Program.UseUtf8Console(e => output = e, e => input = e);
        foreach (var encoding in new[] { output, input })
        {
            Assert.NotNull(encoding);
            Assert.Equal(65001, encoding.CodePage);
            Assert.Empty(encoding.GetPreamble());
        }
    }

    [Fact]
    public void Non_io_exception_propagates()
    {
        Assert.Throws<InvalidOperationException>(() => Program.UseUtf8Console(_ => throw new InvalidOperationException(), _ => { }));
        Assert.Throws<InvalidOperationException>(() => Program.UseUtf8Console(_ => { }, _ => throw new InvalidOperationException()));
    }
}
