using System;
using System.Collections.Generic;
using Odin.Core.Types;

namespace Odin.Core.Diff;

/// <summary>
/// Compares two <see cref="OdinDocument"/> instances and produces an <see cref="OdinDiff"/>
/// describing the additions, removals, changes, and moves between them.
/// </summary>
public static class Differ
{
    /// <summary>
    /// Compare two documents and produce a diff.
    /// </summary>
    /// <param name="a">The original (base) document.</param>
    /// <param name="b">The modified (target) document.</param>
    /// <returns>An <see cref="OdinDiff"/> describing all differences.</returns>
    public static OdinDiff ComputeDiff(OdinDocument a, OdinDocument b)
    {
        var added = new List<DiffEntry>();
        var removed = new List<DiffEntry>();
        var changed = new List<DiffChange>();
        var moved = new List<DiffMove>();

        // Find removed and changed entries
        foreach (var entry in a.Assignments)
        {
            if (b.Assignments.TryGetValue(entry.Key, out var valueB))
            {
                if (!ValuesEqual(entry.Value, valueB))
                {
                    changed.Add(new DiffChange(entry.Key, entry.Value, valueB));
                }
            }
            else
            {
                removed.Add(new DiffEntry(entry.Key, entry.Value));
            }
        }

        // Find added entries
        foreach (var entry in b.Assignments)
        {
            if (!a.Assignments.ContainsKey(entry.Key))
            {
                added.Add(new DiffEntry(entry.Key, entry.Value));
            }
        }

        // Detect moves: a removed value that appears as an added value
        var moveRemovedIndices = new List<int>();
        var moveAddedSet = new HashSet<int>();

        for (int ri = 0; ri < removed.Count; ri++)
        {
            for (int ai = 0; ai < added.Count; ai++)
            {
                if (moveAddedSet.Contains(ai))
                    continue;

                if (ValuesEqual(removed[ri].Value, added[ai].Value))
                {
                    moved.Add(new DiffMove(removed[ri].Path, added[ai].Path, removed[ri].Value));
                    moveRemovedIndices.Add(ri);
                    moveAddedSet.Add(ai);
                    break;
                }
            }
        }

        // Remove matched items from added/removed (in reverse order to preserve indices)
        moveRemovedIndices.Sort((x, y) => y.CompareTo(x));
        foreach (int i in moveRemovedIndices)
        {
            SwapRemove(removed, i);
        }

        var moveAddedIndices = new List<int>(moveAddedSet);
        moveAddedIndices.Sort((x, y) => y.CompareTo(x));
        foreach (int i in moveAddedIndices)
        {
            SwapRemove(added, i);
        }

        return new OdinDiff
        {
            Added = added,
            Removed = removed,
            Changed = changed,
            Moved = moved,
        };
    }

    /// <summary>
    /// Compare two ODIN values for semantic equality.
    /// Uses the serialized string representation for comparison.
    /// </summary>
    private static bool ValuesEqual(OdinValue a, OdinValue b)
    {
        if (ReferenceEquals(a, b))
            return true;
        if (a.Type != b.Type)
            return false;

        // Compare modifiers — different modifiers mean different values
        var modsA = a.Modifiers;
        var modsB = b.Modifiers;
        if (!ReferenceEquals(modsA, modsB))
        {
            var ma = modsA ?? OdinModifiers.Empty;
            var mb = modsB ?? OdinModifiers.Empty;
            if (ma.Required != mb.Required || ma.Confidential != mb.Confidential ||
                ma.Deprecated != mb.Deprecated || ma.Attr != mb.Attr)
                return false;
        }

        // Compare by type
        switch (a)
        {
            case OdinNull:
                return true;
            case OdinBoolean ba:
                return ba.Value == ((OdinBoolean)b).Value;
            case OdinString sa:
                return sa.Value == ((OdinString)b).Value;
            case OdinInteger ia:
                return ia.Value == ((OdinInteger)b).Value;
            case OdinNumber na:
                // Compare with tolerance for floating-point
                return na.Value.Equals(((OdinNumber)b).Value);
            case OdinCurrency ca:
                return ca.Value == ((OdinCurrency)b).Value
                    && ca.CurrencyCode == ((OdinCurrency)b).CurrencyCode;
            case OdinPercent pa:
                return pa.Value.Equals(((OdinPercent)b).Value);
            case OdinDate da:
                return da.Raw == ((OdinDate)b).Raw;
            case OdinTimestamp tsa:
                return tsa.Raw == ((OdinTimestamp)b).Raw;
            case OdinTime ta:
                return ta.Value == ((OdinTime)b).Value;
            case OdinDuration dura:
                return dura.Value == ((OdinDuration)b).Value;
            case OdinReference ra:
                return ra.Path == ((OdinReference)b).Path;
            case OdinBinary bina:
                var binb = (OdinBinary)b;
                return bina.Algorithm == binb.Algorithm
                    && ByteArrayEquals(bina.Data, binb.Data);
            default:
                // For complex types (verb, array, object), fall back to string comparison
                return a.ToString() == b.ToString();
        }
    }

    /// <summary>
    /// Compare two byte arrays for equality.
    /// </summary>
    private static bool ByteArrayEquals(byte[] a, byte[] b)
    {
        if (a.Length != b.Length)
            return false;
        for (int i = 0; i < a.Length; i++)
        {
            if (a[i] != b[i])
                return false;
        }
        return true;
    }

    /// <summary>
    /// Swap-remove: replace element at index with the last element, then remove the last.
    /// This matches Rust's Vec::swap_remove behavior.
    /// </summary>
    private static void SwapRemove<T>(List<T> list, int index)
    {
        int last = list.Count - 1;
        if (index != last)
        {
            list[index] = list[last];
        }
        list.RemoveAt(last);
    }
}
