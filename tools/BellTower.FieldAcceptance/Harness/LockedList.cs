using System.Collections;

namespace BellTower.FieldAcceptance.Harness;

/// <summary>A list that is safe to add to from one thread while another reads it: every read works on a snapshot.</summary>
public sealed class LockedList<T> : IReadOnlyList<T>
{
    private readonly List<T> items = [];
    private readonly object gate = new();

    public void Add(T item)
    {
        lock (gate)
        {
            items.Add(item);
        }
    }

    public int Count
    {
        get
        {
            lock (gate)
            {
                return items.Count;
            }
        }
    }

    public T this[int index]
    {
        get
        {
            lock (gate)
            {
                return items[index];
            }
        }
    }

    public IEnumerator<T> GetEnumerator()
    {
        T[] snapshot;
        lock (gate)
        {
            snapshot = [.. items];
        }
        return ((IEnumerable<T>)snapshot).GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
