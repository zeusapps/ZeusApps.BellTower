using System.Runtime.InteropServices;
using BellTower.Core;

namespace BellTower.IO;

/// <summary>
/// One Toolhelp snapshot of the process table, taken once per hook and shared by the ancestry walk, the agent's direct
/// neighbors and the ingress envelope. A table that could not be taken answers every query as unavailable.
/// </summary>
public sealed class ProcessTable
{
    /// <summary>The agent's direct same-image parent and children; <c>Complete</c> is false when children were not all listed.</summary>
    public sealed record NeighborSet(ProcessIdentity? Parent, IReadOnlyList<ProcessIdentity> Children, bool Complete);

    private readonly Dictionary<int, (int Parent, string Name)>? entries;

    private ProcessTable(Dictionary<int, (int Parent, string Name)>? entries) => this.entries = entries;

    public static ProcessTable Take() => new(Snapshot());

    public List<(int Pid, string Name)> Ancestors(int pid)
    {
        var result = new List<(int, string)>();
        if (entries is null)
        {
            return result;
        }

        var seen = new HashSet<int>();
        while (entries.TryGetValue(pid, out var entry) && seen.Add(pid) && result.Count < 32)
        {
            pid = entry.Parent;
            if (entries.TryGetValue(pid, out var parent))
            {
                result.Add((pid, parent.Name));
            }
        }

        return result;
    }

    /// <summary>
    /// The direct parent and, when <paramref name="children"/> is set, the direct children of an agent process whose image is
    /// <paramref name="image"/>. A relative counts only when its creation time is consistent with the relationship (a parent no
    /// younger and a child no older than the agent), which rejects a reused process identifier. Children beyond
    /// <paramref name="limit"/> are not listed and make the result incomplete. Returns null when the table is unavailable.
    /// </summary>
    public NeighborSet? Neighbors(ProcessIdentity agent, string image, bool children = true, int limit = 8)
    {
        if (entries is null)
        {
            return null;
        }
        ProcessIdentity? parent = null;
        if (entries.TryGetValue(agent.Pid, out var self) && self.Parent != agent.Pid && entries.TryGetValue(self.Parent, out var candidate) &&
            candidate.Name.Equals(image, StringComparison.OrdinalIgnoreCase) &&
            ProcessProbe.Identify(self.Parent) is { } identity && identity.StartedUtcTicks <= agent.StartedUtcTicks)
        {
            parent = identity;
        }
        var listed = new List<ProcessIdentity>();
        var complete = true;
        if (children)
        {
            // Filter before sorting: only the few matching entries are ordered, so the listed set is stable.
            var matching = entries.Where(item => item.Value.Parent == agent.Pid && item.Key != agent.Pid &&
                item.Value.Name.Equals(image, StringComparison.OrdinalIgnoreCase)).Select(item => item.Key).Order();
            foreach (var pid in matching)
            {
                if (ProcessProbe.Identify(pid) is not { } child || child.StartedUtcTicks < agent.StartedUtcTicks)
                {
                    continue;
                }
                if (listed.Count >= limit)
                {
                    complete = false;
                    break;
                }
                listed.Add(child);
            }
        }
        return new NeighborSet(parent, listed, complete);
    }

    private static Dictionary<int, (int Parent, string Name)>? Snapshot()
    {
        var parents = new Dictionary<int, (int Parent, string Name)>();
        var snapshot = CreateToolhelp32Snapshot(2, 0);
        if (snapshot == new IntPtr(-1))
        {
            return null;
        }

        try
        {
            var entry = new ProcessEntry
            {
                Size = (uint)Marshal.SizeOf<ProcessEntry>()
            };
            if (Process32First(snapshot, ref entry))
            {
                do
                {
                    parents[(int)entry.ProcessId] = ((int)entry.ParentProcessId, entry.ExeFile);
                }
                while (Process32Next(snapshot, ref entry));
            }
        }
        finally
        {
            CloseHandle(snapshot);
        }
        return parents;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ProcessEntry
    {
        public uint Size;
        public uint Usage;
        public uint ProcessId;
        public IntPtr Heap;
        public uint Module;
        public uint Threads;
        public uint ParentProcessId;
        public int Priority;
        public uint Flags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string ExeFile;
    }

    [DllImport("kernel32.dll")]
    private static extern IntPtr CreateToolhelp32Snapshot(uint flags, uint pid);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern bool Process32First(IntPtr handle, ref ProcessEntry entry);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern bool Process32Next(IntPtr handle, ref ProcessEntry entry);
    [DllImport("kernel32.dll")]
    private static extern bool CloseHandle(IntPtr handle);
}
