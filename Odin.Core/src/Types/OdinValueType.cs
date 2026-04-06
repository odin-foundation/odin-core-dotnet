namespace Odin.Core.Types;

/// <summary>
/// All possible ODIN value types.
/// </summary>
public enum OdinValueType
{
    /// <summary>Null type (~).</summary>
    Null,
    /// <summary>Boolean type (true/false).</summary>
    Boolean,
    /// <summary>String type (quoted).</summary>
    String,
    /// <summary>Integer type (##).</summary>
    Integer,
    /// <summary>Decimal number type (#).</summary>
    Number,
    /// <summary>Currency type (#$).</summary>
    Currency,
    /// <summary>Percentage type (#%).</summary>
    Percent,
    /// <summary>Calendar date type.</summary>
    Date,
    /// <summary>Date-time timestamp type.</summary>
    Timestamp,
    /// <summary>Time-of-day type.</summary>
    Time,
    /// <summary>ISO 8601 duration type.</summary>
    Duration,
    /// <summary>Reference type (@).</summary>
    Reference,
    /// <summary>Binary/base64 type (^).</summary>
    Binary,
    /// <summary>Verb expression type (%).</summary>
    Verb,
    /// <summary>Array type.</summary>
    Array,
    /// <summary>Object type (nested key-value pairs).</summary>
    Object,
}
