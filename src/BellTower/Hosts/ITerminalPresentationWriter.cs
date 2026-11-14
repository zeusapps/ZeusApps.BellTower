namespace BellTower.Hosts;

public interface ITerminalPresentationWriter
{
    Task<bool> WriteAsync(DesiredTerminalPresentation desired, CancellationToken cancellation);

    /// <summary>
    /// Gives back a title Bell Tower recorded for an adopted native session that is no longer presented (it ended, or the
    /// native-presentation setting was turned off). True when no record remains to retry.
    /// </summary>
    Task<bool> RestoreAsync(string attachmentId, CancellationToken cancellation) => Task.FromResult(true);
}
