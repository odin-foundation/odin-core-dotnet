namespace Odin.Core.Utils;

/// <summary>Security limits for ODIN processing.</summary>
public static class SecurityLimits
{
    /// <summary>Maximum document size in bytes (10 MB).</summary>
    public const int MaxDocumentSize = 10 * 1024 * 1024;

    /// <summary>Maximum nesting depth.</summary>
    public const int MaxNestingDepth = 64;

    /// <summary>Maximum array index.</summary>
    public const int MaxArrayIndex = 10_000;

    /// <summary>Maximum number of records to process in multi-record transform.</summary>
    public const int MaxRecords = 100_000;

    /// <summary>Maximum number of assignments in a single document.</summary>
    public const int MaxAssignments = 100_000;

    /// <summary>Maximum regex pattern length for ReDoS protection.</summary>
    public const int MaxRegexPatternLength = 500;

    /// <summary>Regex timeout for ReDoS protection.</summary>
    public static readonly System.TimeSpan RegexTimeout = System.TimeSpan.FromSeconds(1);
}
