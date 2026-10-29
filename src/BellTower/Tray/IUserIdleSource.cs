namespace BellTower.Tray;

public interface IUserIdleSource
{
    UserIdleSnapshot GetSnapshot();

    // Sources with an asynchronous native poll should override this with a bounded fresh read.
    UserIdleSnapshot GetFreshSnapshot() => GetSnapshot();
}
