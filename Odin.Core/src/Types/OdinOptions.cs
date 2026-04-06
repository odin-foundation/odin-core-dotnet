namespace Odin.Core.Types;

/// <summary>Options for ODIN text parsing.</summary>
public sealed class ParseOptions
{
    /// <summary>Maximum nesting depth (default: 64).</summary>
    public int MaxDepth { get; init; } = 64;

    /// <summary>Maximum document size in bytes (default: 10MB).</summary>
    public int MaxDocumentSize { get; init; } = 10 * 1024 * 1024;

    /// <summary>Maximum array index (default: 10000).</summary>
    public int MaxArrayIndex { get; init; } = 10000;

    /// <summary>Preserve comments (default: false).</summary>
    public bool PreserveComments { get; init; }

    /// <summary>Allow empty documents without raising P014 (default: false).</summary>
    public bool AllowEmpty { get; init; }

    /// <summary>Allow duplicate path assignments (default: false). When false, P007 is raised on duplicates.</summary>
    public bool AllowDuplicates { get; init; }

    /// <summary>Default parse options.</summary>
    public static readonly ParseOptions Default = new();
}

/// <summary>Options for ODIN text serialization.</summary>
public sealed class StringifyOptions
{
    /// <summary>Include metadata section (default: true).</summary>
    public bool IncludeMetadata { get; init; } = true;

    /// <summary>Preserve insertion order (default: true).</summary>
    public bool PreserveOrder { get; init; } = true;

    /// <summary>Indent string (default: empty for compact).</summary>
    public string Indent { get; init; } = "";

    /// <summary>Canonical mode: metadata as $.key, strip trailing zeros, numeric array sort.</summary>
    public bool Canonical { get; init; }

    /// <summary>Default stringify options.</summary>
    public static readonly StringifyOptions Default = new();
}

/// <summary>Options for schema validation.</summary>
public sealed class ValidateOptions
{
    /// <summary>Enable strict mode (reject unknown fields, V011).</summary>
    public bool Strict { get; init; }

    /// <summary>Validate references (V012/V013).</summary>
    public bool ValidateReferences { get; init; } = true;

    /// <summary>Stop validation at the first error (default: false).</summary>
    public bool FailFast { get; init; }

    /// <summary>Default validation options.</summary>
    public static readonly ValidateOptions Default = new();
}
