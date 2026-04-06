using System.Collections.Generic;

namespace Odin.Core.Types;

/// <summary>
/// Represents the difference between two ODIN documents.
/// </summary>
public sealed class OdinDiff
{
    /// <summary>Paths added in the second document.</summary>
    public IReadOnlyList<DiffEntry> Added { get; init; } = System.Array.Empty<DiffEntry>();

    /// <summary>Paths removed from the first document.</summary>
    public IReadOnlyList<DiffEntry> Removed { get; init; } = System.Array.Empty<DiffEntry>();

    /// <summary>Paths with changed values.</summary>
    public IReadOnlyList<DiffChange> Changed { get; init; } = System.Array.Empty<DiffChange>();

    /// <summary>Paths that were moved (renamed).</summary>
    public IReadOnlyList<DiffMove> Moved { get; init; } = System.Array.Empty<DiffMove>();

    /// <summary>Returns true if there are no differences.</summary>
    public bool IsEmpty => Added.Count == 0 && Removed.Count == 0 && Changed.Count == 0 && Moved.Count == 0;
}

/// <summary>A diff entry (added or removed path).</summary>
public sealed class DiffEntry
{
    /// <summary>The path that was added or removed.</summary>
    public string Path { get; }

    /// <summary>The value at this path.</summary>
    public OdinValue Value { get; }

    /// <summary>Creates a diff entry.</summary>
    public DiffEntry(string path, OdinValue value)
    {
        Path = path;
        Value = value;
    }
}

/// <summary>A changed value in a diff.</summary>
public sealed class DiffChange
{
    /// <summary>The path that changed.</summary>
    public string Path { get; }

    /// <summary>The old value.</summary>
    public OdinValue OldValue { get; }

    /// <summary>The new value.</summary>
    public OdinValue NewValue { get; }

    /// <summary>Creates a diff change.</summary>
    public DiffChange(string path, OdinValue oldValue, OdinValue newValue)
    {
        Path = path;
        OldValue = oldValue;
        NewValue = newValue;
    }
}

/// <summary>A moved path in a diff.</summary>
public sealed class DiffMove
{
    /// <summary>The original path.</summary>
    public string FromPath { get; }

    /// <summary>The new path.</summary>
    public string ToPath { get; }

    /// <summary>The value that was moved.</summary>
    public OdinValue Value { get; }

    /// <summary>Creates a diff move.</summary>
    public DiffMove(string fromPath, string toPath, OdinValue value)
    {
        FromPath = fromPath;
        ToPath = toPath;
        Value = value;
    }
}
