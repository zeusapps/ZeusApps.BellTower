namespace BellTower.Providers;

// A recognized literal OpenSpec CLI invocation. Inspection verbs (status, instructions) are
// recognized so they can be proven non-selecting; only "new" can become work evidence.
public sealed record OpenSpecOperation(string Verb, string Change, string? Store, string? Directory, bool Redirected)
{
    public bool Inspection => Verb is "status" or "instructions";
}
