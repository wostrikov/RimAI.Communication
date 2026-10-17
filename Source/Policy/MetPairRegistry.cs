using System.Collections.Generic;

namespace Ustas.RimAI.Communication.Policy;

/// <summary>
/// Pairs of pawns that have already talked, by thing id, so a visitor is introduced once and
/// not at every talk while no thought about the other is left to remember it by. Capped: the
/// oldest pair goes first. Host-free, so it can be tested without the game.
/// </summary>
public sealed class MetPairRegistry
{
    public const int MaxPairs = 2000;

    private readonly object _lock = new();
    private readonly HashSet<long> _pairs = new();
    private readonly Queue<long> _order = new();
    private readonly int _capacity;

    public MetPairRegistry(int capacity = MaxPairs)
    {
        _capacity = capacity > 0 ? capacity : MaxPairs;
    }

    /// <summary>One key for the pair, whichever of the two comes first.</summary>
    public static long Key(int a, int b)
    {
        uint x = (uint)a;
        uint y = (uint)b;
        return x < y ? (long)(((ulong)x << 32) | y) : (long)(((ulong)y << 32) | x);
    }

    public int Count
    {
        get { lock (_lock) return _pairs.Count; }
    }

    public bool HasMet(int a, int b)
    {
        if (a == b) return false;
        lock (_lock) return _pairs.Contains(Key(a, b));
    }

    /// <returns>Whether the pair is new.</returns>
    public bool Record(int a, int b)
    {
        if (a == b) return false;
        lock (_lock) return AddLocked(Key(a, b));
    }

    /// <summary>The pairs, oldest first, for saving.</summary>
    public List<long> Snapshot()
    {
        lock (_lock) return new List<long>(_order);
    }

    /// <summary>Replaces the pairs with saved ones; a save without any leaves it empty.</summary>
    public void Load(IEnumerable<long> keys)
    {
        lock (_lock)
        {
            _pairs.Clear();
            _order.Clear();
            if (keys == null) return;
            foreach (long key in keys)
                AddLocked(key);
        }
    }

    private bool AddLocked(long key)
    {
        if (!_pairs.Add(key)) return false;
        _order.Enqueue(key);
        while (_order.Count > _capacity)
            _pairs.Remove(_order.Dequeue());
        return true;
    }
}
