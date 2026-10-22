using BellTower.Core;

namespace BellTower.Updates;

public sealed record UpdatePlan(string Id, string Token, string Root, string DataDirectory, string Original, string Target,
    string ContractHash, DateTimeOffset Created, ProcessIdentity Owner, bool HadTray);
