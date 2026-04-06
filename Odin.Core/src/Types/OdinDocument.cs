using System;
using System.Collections.Generic;

namespace Odin.Core.Types;

/// <summary>An @import directive in an ODIN document.</summary>
public sealed class OdinImport
{
    /// <summary>Import path (relative, absolute, or URL).</summary>
    public string Path { get; }

    /// <summary>Optional namespace alias.</summary>
    public string? Alias { get; init; }

    /// <summary>Source line number (1-based).</summary>
    public int Line { get; init; }

    /// <summary>Creates an import directive.</summary>
    public OdinImport(string path) { Path = path; }
}

/// <summary>A @schema directive in an ODIN document.</summary>
public sealed class OdinSchemaRef
{
    /// <summary>Schema URL or path.</summary>
    public string Url { get; }

    /// <summary>Source line number (1-based).</summary>
    public int Line { get; init; }

    /// <summary>Creates a schema reference.</summary>
    public OdinSchemaRef(string url) { Url = url; }
}

/// <summary>A conditional directive in an ODIN document.</summary>
public sealed class OdinConditional
{
    /// <summary>Condition expression.</summary>
    public string Condition { get; }

    /// <summary>Source line number (1-based).</summary>
    public int Line { get; init; }

    /// <summary>Creates a conditional directive.</summary>
    public OdinConditional(string condition) { Condition = condition; }
}

/// <summary>A preserved comment from source ODIN text.</summary>
public sealed class OdinComment
{
    /// <summary>The comment text (without the leading ; or ; space).</summary>
    public string Text { get; }

    /// <summary>The path this comment is associated with (if any).</summary>
    public string? AssociatedPath { get; init; }

    /// <summary>Source line number (1-based).</summary>
    public int Line { get; init; }

    /// <summary>Creates a comment.</summary>
    public OdinComment(string text) { Text = text; }
}

/// <summary>Options for document flattening.</summary>
public sealed class FlattenOptions
{
    /// <summary>Include metadata ({$} section) in output.</summary>
    public bool IncludeMetadata { get; init; }

    /// <summary>Include null values in output.</summary>
    public bool IncludeNulls { get; init; }

    /// <summary>Sort output keys alphabetically (default: true).</summary>
    public bool Sort { get; init; } = true;
}

/// <summary>
/// An immutable ODIN document containing metadata, assignments, modifiers,
/// imports, schemas, conditionals, and comments.
/// </summary>
public sealed class OdinDocument
{
    /// <summary>Metadata assignments from the {$} header section.</summary>
    public OrderedMap<string, OdinValue> Metadata { get; }

    /// <summary>All field assignments (dot-separated paths as keys).</summary>
    public OrderedMap<string, OdinValue> Assignments { get; }

    /// <summary>Per-path modifiers.</summary>
    public OrderedMap<string, OdinModifiers> PathModifiers { get; }

    /// <summary>Import directives.</summary>
    public IReadOnlyList<OdinImport> Imports { get; }

    /// <summary>Schema directives.</summary>
    public IReadOnlyList<OdinSchemaRef> Schemas { get; }

    /// <summary>Conditional directives.</summary>
    public IReadOnlyList<OdinConditional> Conditionals { get; }

    /// <summary>Preserved comments.</summary>
    public IReadOnlyList<OdinComment> Comments { get; }

    /// <summary>Creates a document with all components.</summary>
    public OdinDocument(
        OrderedMap<string, OdinValue>? metadata = null,
        OrderedMap<string, OdinValue>? assignments = null,
        OrderedMap<string, OdinModifiers>? modifiers = null,
        IReadOnlyList<OdinImport>? imports = null,
        IReadOnlyList<OdinSchemaRef>? schemas = null,
        IReadOnlyList<OdinConditional>? conditionals = null,
        IReadOnlyList<OdinComment>? comments = null)
    {
        Metadata = metadata ?? new OrderedMap<string, OdinValue>();
        Assignments = assignments ?? new OrderedMap<string, OdinValue>();
        PathModifiers = modifiers ?? new OrderedMap<string, OdinModifiers>();
        Imports = imports ?? Array.Empty<OdinImport>();
        Schemas = schemas ?? Array.Empty<OdinSchemaRef>();
        Conditionals = conditionals ?? Array.Empty<OdinConditional>();
        Comments = comments ?? Array.Empty<OdinComment>();
    }

    /// <summary>Create an empty document.</summary>
    public static OdinDocument Empty() => new();

    /// <summary>Get a value at the given path. Supports $.key prefix for metadata.</summary>
    public OdinValue? Get(string path)
    {
        if (path.StartsWith("$.", StringComparison.Ordinal))
        {
            var metaKey = path.Substring(2);
            return Metadata.TryGetValue(metaKey, out var val) ? val : null;
        }
        return Assignments.TryGetValue(path, out var value) ? value : null;
    }

    /// <summary>Get a string value at the given path.</summary>
    public string? GetString(string path) => Get(path)?.AsString();

    /// <summary>Get an integer value at the given path.</summary>
    public long? GetInteger(string path) => Get(path)?.AsInt64();

    /// <summary>Get a numeric value at the given path.</summary>
    public double? GetNumber(string path) => Get(path)?.AsDouble();

    /// <summary>Get a boolean value at the given path.</summary>
    public bool? GetBoolean(string path) => Get(path)?.AsBool();

    /// <summary>Returns true if the given path has a value assigned.</summary>
    public bool Has(string path) => Get(path) != null;

    /// <summary>
    /// Resolve a value at the given path, following @reference chains.
    /// Returns null if the path doesn't exist. Throws on circular/unresolved references.
    /// </summary>
    public OdinValue? Resolve(string path)
    {
        var value = Get(path);
        if (value == null) return null;

        if (value is OdinReference refVal)
        {
            var seen = new HashSet<string> { path };
            var currentPath = refVal.Path;

            while (true)
            {
                if (seen.Contains(currentPath))
                    throw new InvalidOperationException($"Circular reference detected: {path}");
                seen.Add(currentPath);

                var current = Get(currentPath);
                if (current == null)
                    throw new InvalidOperationException($"Unresolved reference: {currentPath}");
                if (current is OdinReference nextRef)
                    currentPath = nextRef.Path;
                else
                    return current;
            }
        }

        return value;
    }

    /// <summary>Returns all assignment paths in insertion order.</summary>
    public IReadOnlyList<string> Paths() => Assignments.Keys;

    /// <summary>Create a new document with the given path set to the given value.</summary>
    public OdinDocument With(string path, OdinValue value)
    {
        var newAssignments = Assignments.Clone();
        newAssignments.Set(path, value);
        return new OdinDocument(
            metadata: Metadata,
            assignments: newAssignments,
            modifiers: PathModifiers,
            imports: Imports,
            schemas: Schemas,
            conditionals: Conditionals,
            comments: Comments);
    }

    /// <summary>Create a new document with the given path removed.</summary>
    public OdinDocument Without(string path)
    {
        var newAssignments = Assignments.Clone();
        newAssignments.Remove(path);
        return new OdinDocument(
            metadata: Metadata,
            assignments: newAssignments,
            modifiers: PathModifiers,
            imports: Imports,
            schemas: Schemas,
            conditionals: Conditionals,
            comments: Comments);
    }

    /// <summary>Flatten the document to a map of string key-value pairs.</summary>
    public OrderedMap<string, string> Flatten(FlattenOptions? options = null)
    {
        var opts = options ?? new FlattenOptions();
        var result = new OrderedMap<string, string>();

        if (opts.IncludeMetadata)
        {
            foreach (var entry in Metadata)
                result.Set($"$.{entry.Key}", FormatValueForFlatten(entry.Value));
        }

        foreach (var entry in Assignments)
        {
            if (!opts.IncludeNulls && entry.Value.IsNull)
                continue;
            result.Set(entry.Key, FormatValueForFlatten(entry.Value));
        }

        if (opts.Sort)
        {
            var entries = new List<KeyValuePair<string, string>>(result.Entries);
            entries.Sort((a, b) => string.Compare(a.Key, b.Key, StringComparison.Ordinal));
            return new OrderedMap<string, string>(entries);
        }

        return result;
    }

    private static string FormatValueForFlatten(OdinValue value)
    {
        switch (value)
        {
            case OdinNull: return "~";
            case OdinBoolean b: return b.Value ? "true" : "false";
            case OdinString s: return s.Value;
            case OdinInteger i: return i.Raw ?? i.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            case OdinNumber n: return n.Raw ?? n.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            case OdinCurrency c: return c.Raw ?? c.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            case OdinPercent p: return p.Raw ?? p.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            case OdinDate d: return d.Raw;
            case OdinTimestamp ts: return ts.Raw;
            case OdinTime t: return t.Value;
            case OdinDuration d: return d.Value;
            case OdinReference r: return $"@{r.Path}";
            case OdinBinary: return "<binary>";
            default: return value.ToString() ?? "";
        }
    }
}
