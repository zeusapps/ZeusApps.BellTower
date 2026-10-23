namespace BellTower.Hosts;

public interface ITerminalPresentationWriter
{
    Task<bool> WriteAsync(DesiredTerminalPresentation desired, CancellationToken cancellation);
}
