using System;
using System.Globalization;

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

    /// <summary>Transform fuel budget; 0 = unbounded. Env: ODIN_MAX_TRANSFORM_FUEL.</summary>
    public static long MaxTransformFuel { get; set; } = EnvLong("ODIN_MAX_TRANSFORM_FUEL", 0);

    /// <summary>Transform wall-clock timeout in ms; 0 = unbounded. Env: ODIN_TRANSFORM_TIMEOUT_MS.</summary>
    public static long TransformTimeoutMs { get; set; } = EnvLong("ODIN_TRANSFORM_TIMEOUT_MS", 0);

    /// <summary>Maximum expression evaluation depth. Env: ODIN_MAX_EXPRESSION_DEPTH.</summary>
    public static int MaxExpressionDepth { get; set; } = (int)EnvLong("ODIN_MAX_EXPRESSION_DEPTH", 32);

    // Read a non-negative long from the environment, falling back to the default.
    private static long EnvLong(string name, long fallback)
    {
        var raw = Environment.GetEnvironmentVariable(name);
        if (raw != null && long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) && v >= 0)
            return v;
        return fallback;
    }
}
