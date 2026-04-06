using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Odin.Core.Types;

/// <summary>
/// Insertion-order preserving map. Maintains both a list for order and a dictionary for O(1) lookup.
/// </summary>
public sealed class OrderedMap<TKey, TValue> : IEnumerable<KeyValuePair<TKey, TValue>>
    where TKey : notnull
{
    private readonly List<KeyValuePair<TKey, TValue>> _entries;
    private readonly Dictionary<TKey, int> _index;

    /// <summary>Creates an empty ordered map.</summary>
    public OrderedMap()
    {
        _entries = new List<KeyValuePair<TKey, TValue>>();
        _index = new Dictionary<TKey, int>();
    }

    /// <summary>Creates an ordered map with specified capacity.</summary>
    public OrderedMap(int capacity)
    {
        _entries = new List<KeyValuePair<TKey, TValue>>(capacity);
        _index = new Dictionary<TKey, int>(capacity);
    }

    /// <summary>Creates an ordered map from existing entries.</summary>
    public OrderedMap(IEnumerable<KeyValuePair<TKey, TValue>> entries)
    {
        _entries = new List<KeyValuePair<TKey, TValue>>();
        _index = new Dictionary<TKey, int>();
        foreach (var entry in entries)
        {
            Set(entry.Key, entry.Value);
        }
    }

    /// <summary>Gets the number of entries.</summary>
    public int Count => _entries.Count;

    /// <summary>Gets all keys in insertion order.</summary>
    public IReadOnlyList<TKey> Keys
    {
        get
        {
            var keys = new List<TKey>(_entries.Count);
            for (int i = 0; i < _entries.Count; i++)
                keys.Add(_entries[i].Key);
            return keys;
        }
    }

    /// <summary>Gets all values in insertion order.</summary>
    public IReadOnlyList<TValue> Values
    {
        get
        {
            var values = new List<TValue>(_entries.Count);
            for (int i = 0; i < _entries.Count; i++)
                values.Add(_entries[i].Value);
            return values;
        }
    }

    /// <summary>Gets all entries in insertion order.</summary>
    public IReadOnlyList<KeyValuePair<TKey, TValue>> Entries => _entries;

    /// <summary>Gets or sets a value by key.</summary>
    public TValue this[TKey key]
    {
        get
        {
            if (!_index.TryGetValue(key, out var idx))
                throw new KeyNotFoundException($"Key not found: {key}");
            return _entries[idx].Value;
        }
        set => Set(key, value);
    }

    /// <summary>Sets a value, updating in-place if key exists, appending if new.</summary>
    public void Set(TKey key, TValue value)
    {
        if (_index.TryGetValue(key, out var idx))
        {
            _entries[idx] = new KeyValuePair<TKey, TValue>(key, value);
        }
        else
        {
            _index[key] = _entries.Count;
            _entries.Add(new KeyValuePair<TKey, TValue>(key, value));
        }
    }

    /// <summary>Tries to get a value by key.</summary>
    public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value)
    {
        if (_index.TryGetValue(key, out var idx))
        {
            value = _entries[idx].Value;
            return true;
        }
        value = default;
        return false;
    }

    /// <summary>Returns true if the map contains the key.</summary>
    public bool ContainsKey(TKey key) => _index.ContainsKey(key);

    /// <summary>Removes a key and its value. Returns true if found.</summary>
    public bool Remove(TKey key)
    {
        if (!_index.TryGetValue(key, out var idx))
            return false;

        _entries.RemoveAt(idx);
        _index.Remove(key);

        // Rebuild index for entries after the removed one
        for (int i = idx; i < _entries.Count; i++)
        {
            _index[_entries[i].Key] = i;
        }
        return true;
    }

    /// <summary>Removes all entries.</summary>
    public void Clear()
    {
        _entries.Clear();
        _index.Clear();
    }

    /// <summary>Gets the entry at the specified index position.</summary>
    public KeyValuePair<TKey, TValue> GetAt(int index) => _entries[index];

    /// <summary>Creates a shallow copy of this map.</summary>
    public OrderedMap<TKey, TValue> Clone() => new(_entries);

    /// <inheritdoc/>
    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() => _entries.GetEnumerator();

    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
