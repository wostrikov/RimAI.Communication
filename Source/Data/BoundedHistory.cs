using System.Collections.Generic;

namespace Ustas.RimAI.Communication.Data;

/// <summary>
/// A keyed log that keeps insertion order, holds at most a set number of entries - the oldest
/// goes first - and is safe to write from one thread while another reads it. Readers get a
/// copy, never the live collection. Host-free, so it can be tested without the game.
/// </summary>
public sealed class BoundedHistory<TKey, TValue>
{
    private readonly object _lock = new();
    private readonly Dictionary<TKey, LinkedListNode<KeyValuePair<TKey, TValue>>> _index = new();
    private readonly LinkedList<KeyValuePair<TKey, TValue>> _order = new();
    private readonly int _capacity;

    public BoundedHistory(int capacity)
    {
        _capacity = capacity > 0 ? capacity : 1;
    }

    public int Capacity => _capacity;

    public int Count
    {
        get { lock (_lock) return _order.Count; }
    }

    /// <summary>Adds an entry at the end, or replaces an existing one where it stands.</summary>
    public void Set(TKey key, TValue value)
    {
        lock (_lock)
        {
            var entry = new KeyValuePair<TKey, TValue>(key, value);
            if (_index.TryGetValue(key, out var node))
            {
                node.Value = entry;
                return;
            }

            _index[key] = _order.AddLast(entry);
            while (_order.Count > _capacity)
            {
                var oldest = _order.First;
                _order.RemoveFirst();
                _index.Remove(oldest.Value.Key);
            }
        }
    }

    public bool TryGetValue(TKey key, out TValue value)
    {
        lock (_lock)
        {
            if (_index.TryGetValue(key, out var node))
            {
                value = node.Value.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    /// <summary>The values, oldest first, as a copy.</summary>
    public List<TValue> Snapshot()
    {
        lock (_lock)
        {
            var values = new List<TValue>(_order.Count);
            foreach (var entry in _order)
                values.Add(entry.Value);
            return values;
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _order.Clear();
            _index.Clear();
        }
    }
}
