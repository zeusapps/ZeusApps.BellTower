# Independent Analysis 005 pump results

Command: `dotnet run --project artifacts/analysis-005-pump-probe/Probe.csproj --no-restore`.

Executed with the current Release application/protocol assemblies after QA confirmed its build was complete. Probe source is [retained here](analysis-005-pump-source.cs.txt). All data are disposable, fake runtime responses; the fixture endpoint is never contacted. No production configuration, installed tray, physical audio or real approval is used.

The normal named-wait cases pass on both causal and unordered paths (26 assertions):

- Original old-query/P1-resolves/P2-starts race: transient cache rejected; P2 stays provisional; zero announcements.
- Query A returns while A+B exists: inspected set/revision correctly captured; consumption rejects old result; transient cache remains invalid when B resolves; persisted generation remains advanced.
- A fresh query for A has a new observation ID and current revision, confirms successfully, and replay of that same observation is idempotent.
- Query scope does not follow a caller mutating the session object while the read is pending.
- Unrelated tool activity does not advance the permission revision or invalidate an unchanged-set query.

A separate candidate-group branch passes its two setup assertions (one per path), then emits these measured diagnostics:

```text
PASS candidate-set/False starts as ambiguous A+B
candidate-set/False after A resumes: candidates=B, revisionUnchanged=True
candidate-set/False after old response: confirmation=Confirmed, attention=True
PASS candidate-set/True starts as ambiguous A+B
candidate-set/True after A resumes: candidates=B, revisionUnchanged=True
candidate-set/True after old response: confirmation=Confirmed, attention=True
```

These diagnostic results are acceptance failures: the query began with candidates A+B and its old positive result could describe A. A resumes before the response arrives, leaving only B, but B is confirmed without fresh evidence. The program exits successfully because this diagnostic branch prints its result rather than asserting the desired rejection; its final completion message must not be read as acceptance passing. Both diagnostic failures are canonical ANALYSIS-002, not new finding IDs.

Reproduction:

1. In an isolated store, publish two ToolStart observations with distinct operation IDs A and B and the same fingerprint.
2. Publish a no-operation-ID PermissionRequest with that fingerprint. The real reducer creates one provisional wait whose Candidates are A and B.
3. Begin the real CodexPermissionPump query with a fake delayed client. Wait until its read starts.
4. Publish ToolStart A to signal resumption. The reducer narrows the same wait to candidate B; the permission Episode and ApprovalRevision remain unchanged.
5. Release the old positive response and drain the real StateStore. Candidate B becomes Confirmed and requires attention.

This is a simulated race using the real pump, reducer and persistence. It does not prove how often a native provider emits this supported missing-ID/parallel shape, nor does it replace native acceptance. The existing `AmbiguousParallelPermissionRequiresEveryCandidateToResumeOrComplete` regression establishes that candidate narrowing is an intentionally supported reducer behavior.
