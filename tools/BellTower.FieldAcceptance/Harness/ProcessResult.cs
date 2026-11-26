namespace BellTower.FieldAcceptance.Harness;

/// <param name="AgentMilliseconds">For a hook run through <see cref="AgentSession"/>: the milliseconds the fake provider process measured from starting the hook shell to its exit (in-agent time); 0 otherwise.</param>
public sealed record ProcessResult(int ExitCode, string Output, string Error, TimeSpan Elapsed, bool TimedOut, double AgentMilliseconds = 0)
{
    public bool Succeeded => ExitCode == 0 && !TimedOut;
}
