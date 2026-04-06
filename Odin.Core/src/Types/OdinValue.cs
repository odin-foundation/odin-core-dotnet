using System;
using System.Collections.Generic;
using System.Globalization;

namespace Odin.Core.Types;

/// <summary>
/// The canonical ODIN value type — abstract base for all 16 value types.
/// Each subclass carries its payload plus optional modifiers and directives.
/// </summary>
public abstract class OdinValue
{
    /// <summary>Returns the type discriminator for this value.</summary>
    public abstract OdinValueType Type { get; }

    /// <summary>Modifiers applied to this value.</summary>
    public OdinModifiers? Modifiers { get; init; }

    /// <summary>Trailing directives.</summary>
    public IReadOnlyList<OdinDirective> Directives { get; init; } = Array.Empty<OdinDirective>();

    /// <summary>Returns true if this value has the required modifier.</summary>
    public bool IsRequired => Modifiers is { Required: true };

    /// <summary>Returns true if this value has the confidential modifier.</summary>
    public bool IsConfidential => Modifiers is { Confidential: true };

    /// <summary>Returns true if this value has the deprecated modifier.</summary>
    public bool IsDeprecated => Modifiers is { Deprecated: true };

    /// <summary>Returns true if this is a null value.</summary>
    public bool IsNull => Type == OdinValueType.Null;

    /// <summary>Returns true if this is a boolean value.</summary>
    public bool IsBoolean => Type == OdinValueType.Boolean;

    /// <summary>Returns true if this is a string value.</summary>
    public bool IsString => Type == OdinValueType.String;

    /// <summary>Returns true if this is an integer value.</summary>
    public bool IsInteger => Type == OdinValueType.Integer;

    /// <summary>Returns true if this is a number value.</summary>
    public bool IsNumber => Type == OdinValueType.Number;

    /// <summary>Returns true if this is a currency value.</summary>
    public bool IsCurrency => Type == OdinValueType.Currency;

    /// <summary>Returns true if this is a percent value.</summary>
    public bool IsPercent => Type == OdinValueType.Percent;

    /// <summary>Returns true if this is any numeric type.</summary>
    public bool IsNumeric => Type is OdinValueType.Integer or OdinValueType.Number or OdinValueType.Currency or OdinValueType.Percent;

    /// <summary>Returns true if this is any temporal type.</summary>
    public bool IsTemporal => Type is OdinValueType.Date or OdinValueType.Timestamp or OdinValueType.Time or OdinValueType.Duration;

    /// <summary>Returns true if this is a date value.</summary>
    public bool IsDate => Type == OdinValueType.Date;

    /// <summary>Returns true if this is a timestamp value.</summary>
    public bool IsTimestamp => Type == OdinValueType.Timestamp;

    /// <summary>Returns true if this is a time value.</summary>
    public bool IsTime => Type == OdinValueType.Time;

    /// <summary>Returns true if this is a duration value.</summary>
    public bool IsDuration => Type == OdinValueType.Duration;

    /// <summary>Returns true if this is a reference value.</summary>
    public bool IsReference => Type == OdinValueType.Reference;

    /// <summary>Returns true if this is a binary value.</summary>
    public bool IsBinary => Type == OdinValueType.Binary;

    /// <summary>Returns true if this is a verb expression.</summary>
    public bool IsVerb => Type == OdinValueType.Verb;

    /// <summary>Returns true if this is an array value.</summary>
    public bool IsArray => Type == OdinValueType.Array;

    /// <summary>Returns true if this is an object value.</summary>
    public bool IsObject => Type == OdinValueType.Object;

    /// <summary>Extract the boolean value, if this is a boolean.</summary>
    public virtual bool? AsBool() => null;

    /// <summary>Extract the string value, if this is a string.</summary>
    public virtual string? AsString() => null;

    /// <summary>Extract the integer value, if this is an integer.</summary>
    public virtual long? AsInt64() => null;

    /// <summary>Extract the numeric value as double (works for number, integer, currency, percent).</summary>
    public virtual double? AsDouble() => null;

    /// <summary>Extract the decimal value, if this is a currency.</summary>
    public virtual decimal? AsDecimal() => null;

    /// <summary>Extract the reference path, if this is a reference.</summary>
    public virtual string? AsReference() => null;

    /// <summary>Extract the array items, if this is an array.</summary>
    public virtual IReadOnlyList<OdinArrayItem>? AsArray() => null;

    /// <summary>Create a new value with the given modifiers applied.</summary>
    public abstract OdinValue WithModifiers(OdinModifiers? modifiers);

    /// <summary>Create a new value with the given directives.</summary>
    public abstract OdinValue WithDirectives(IReadOnlyList<OdinDirective> directives);
}

// ─────────────────────────────────────────────────────────────────────────────
// 16 Sealed Subclasses
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>Null value (~).</summary>
public sealed class OdinNull : OdinValue
{
    /// <inheritdoc/>
    public override OdinValueType Type => OdinValueType.Null;

    /// <inheritdoc/>
    public override OdinValue WithModifiers(OdinModifiers? modifiers) =>
        new OdinNull { Modifiers = modifiers, Directives = Directives };

    /// <inheritdoc/>
    public override OdinValue WithDirectives(IReadOnlyList<OdinDirective> directives) =>
        new OdinNull { Modifiers = Modifiers, Directives = directives };

    /// <inheritdoc/>
    public override string ToString() => "~";
}

/// <summary>Boolean value (true/false).</summary>
public sealed class OdinBoolean : OdinValue
{
    /// <summary>The boolean payload.</summary>
    public bool Value { get; }

    /// <summary>Creates a boolean value.</summary>
    public OdinBoolean(bool value) { Value = value; }

    /// <inheritdoc/>
    public override OdinValueType Type => OdinValueType.Boolean;

    /// <inheritdoc/>
    public override bool? AsBool() => Value;

    /// <inheritdoc/>
    public override OdinValue WithModifiers(OdinModifiers? modifiers) =>
        new OdinBoolean(Value) { Modifiers = modifiers, Directives = Directives };

    /// <inheritdoc/>
    public override OdinValue WithDirectives(IReadOnlyList<OdinDirective> directives) =>
        new OdinBoolean(Value) { Modifiers = Modifiers, Directives = directives };

    /// <inheritdoc/>
    public override string ToString() => Value ? "true" : "false";
}

/// <summary>String value (quoted).</summary>
public sealed class OdinString : OdinValue
{
    /// <summary>The string payload.</summary>
    public string Value { get; }

    /// <summary>Creates a string value.</summary>
    public OdinString(string value) { Value = value; }

    /// <inheritdoc/>
    public override OdinValueType Type => OdinValueType.String;

    /// <inheritdoc/>
    public override string? AsString() => Value;

    /// <inheritdoc/>
    public override OdinValue WithModifiers(OdinModifiers? modifiers) =>
        new OdinString(Value) { Modifiers = modifiers, Directives = Directives };

    /// <inheritdoc/>
    public override OdinValue WithDirectives(IReadOnlyList<OdinDirective> directives) =>
        new OdinString(Value) { Modifiers = Modifiers, Directives = directives };

    /// <inheritdoc/>
    public override string ToString() => $"\"{Value}\"";
}

/// <summary>Integer value (##42).</summary>
public sealed class OdinInteger : OdinValue
{
    /// <summary>The integer payload.</summary>
    public long Value { get; }

    /// <summary>Original string for round-trip preservation of large values.</summary>
    public string? Raw { get; init; }

    /// <summary>Creates an integer value.</summary>
    public OdinInteger(long value) { Value = value; }

    /// <inheritdoc/>
    public override OdinValueType Type => OdinValueType.Integer;

    /// <inheritdoc/>
    public override long? AsInt64() => Value;

    /// <inheritdoc/>
    public override double? AsDouble() => Value;

    /// <inheritdoc/>
    public override OdinValue WithModifiers(OdinModifiers? modifiers) =>
        new OdinInteger(Value) { Raw = Raw, Modifiers = modifiers, Directives = Directives };

    /// <inheritdoc/>
    public override OdinValue WithDirectives(IReadOnlyList<OdinDirective> directives) =>
        new OdinInteger(Value) { Raw = Raw, Modifiers = Modifiers, Directives = directives };

    /// <inheritdoc/>
    public override string ToString() => Raw != null ? $"##{Raw}" : $"##{Value}";
}

/// <summary>Decimal number value (#3.14).</summary>
public sealed class OdinNumber : OdinValue
{
    /// <summary>The floating-point payload.</summary>
    public double Value { get; }

    /// <summary>Number of decimal places (for formatting).</summary>
    public byte? DecimalPlaces { get; init; }

    /// <summary>Original string for round-trip preservation.</summary>
    public string? Raw { get; init; }

    /// <summary>Creates a number value.</summary>
    public OdinNumber(double value) { Value = value; }

    /// <inheritdoc/>
    public override OdinValueType Type => OdinValueType.Number;

    /// <inheritdoc/>
    public override double? AsDouble() => Value;

    /// <inheritdoc/>
    public override OdinValue WithModifiers(OdinModifiers? modifiers) =>
        new OdinNumber(Value) { DecimalPlaces = DecimalPlaces, Raw = Raw, Modifiers = modifiers, Directives = Directives };

    /// <inheritdoc/>
    public override OdinValue WithDirectives(IReadOnlyList<OdinDirective> directives) =>
        new OdinNumber(Value) { DecimalPlaces = DecimalPlaces, Raw = Raw, Modifiers = Modifiers, Directives = directives };

    /// <inheritdoc/>
    public override string ToString() => Raw != null ? $"#{Raw}" : $"#{Value.ToString(CultureInfo.InvariantCulture)}";
}

/// <summary>Currency value (#$100.00).</summary>
public sealed class OdinCurrency : OdinValue
{
    /// <summary>The currency amount.</summary>
    public double Value { get; }

    /// <summary>Number of decimal places (default 2).</summary>
    public byte DecimalPlaces { get; init; } = 2;

    /// <summary>Optional currency code (e.g., "USD").</summary>
    public string? CurrencyCode { get; init; }

    /// <summary>Original string for round-trip preservation.</summary>
    public string? Raw { get; init; }

    /// <summary>Creates a currency value.</summary>
    public OdinCurrency(double value) { Value = value; }

    /// <inheritdoc/>
    public override OdinValueType Type => OdinValueType.Currency;

    /// <inheritdoc/>
    public override double? AsDouble() => Value;

    /// <inheritdoc/>
    public override decimal? AsDecimal() => (decimal)Value;

    /// <inheritdoc/>
    public override OdinValue WithModifiers(OdinModifiers? modifiers) =>
        new OdinCurrency(Value) { DecimalPlaces = DecimalPlaces, CurrencyCode = CurrencyCode, Raw = Raw, Modifiers = modifiers, Directives = Directives };

    /// <inheritdoc/>
    public override OdinValue WithDirectives(IReadOnlyList<OdinDirective> directives) =>
        new OdinCurrency(Value) { DecimalPlaces = DecimalPlaces, CurrencyCode = CurrencyCode, Raw = Raw, Modifiers = Modifiers, Directives = directives };

    /// <inheritdoc/>
    public override string ToString()
    {
        var val = Raw ?? Value.ToString(CultureInfo.InvariantCulture);
        return CurrencyCode != null ? $"#${val}:{CurrencyCode}" : $"#${val}";
    }
}

/// <summary>Percentage value (#%0.15 for 15%).</summary>
public sealed class OdinPercent : OdinValue
{
    /// <summary>The percentage as a decimal (0.15 = 15%).</summary>
    public double Value { get; }

    /// <summary>Original string for round-trip preservation.</summary>
    public string? Raw { get; init; }

    /// <summary>Creates a percent value.</summary>
    public OdinPercent(double value) { Value = value; }

    /// <inheritdoc/>
    public override OdinValueType Type => OdinValueType.Percent;

    /// <inheritdoc/>
    public override double? AsDouble() => Value;

    /// <inheritdoc/>
    public override OdinValue WithModifiers(OdinModifiers? modifiers) =>
        new OdinPercent(Value) { Raw = Raw, Modifiers = modifiers, Directives = Directives };

    /// <inheritdoc/>
    public override OdinValue WithDirectives(IReadOnlyList<OdinDirective> directives) =>
        new OdinPercent(Value) { Raw = Raw, Modifiers = Modifiers, Directives = directives };

    /// <inheritdoc/>
    public override string ToString() => Raw != null ? $"#%{Raw}" : $"#%{Value.ToString(CultureInfo.InvariantCulture)}";
}

/// <summary>Date value (2024-06-15).</summary>
public sealed class OdinDate : OdinValue
{
    /// <summary>Calendar year.</summary>
    public int Year { get; }

    /// <summary>Month of the year (1-12).</summary>
    public byte Month { get; }

    /// <summary>Day of the month (1-31).</summary>
    public byte Day { get; }

    /// <summary>Original string representation (required for round-trip).</summary>
    public string Raw { get; }

    /// <summary>Creates a date value from components.</summary>
    public OdinDate(int year, byte month, byte day, string raw)
    {
        Year = year;
        Month = month;
        Day = day;
        Raw = raw;
    }

    /// <summary>Creates a date value from components (generates raw string).</summary>
    public OdinDate(int year, byte month, byte day)
        : this(year, month, day, $"{year:D4}-{month:D2}-{day:D2}") { }

    /// <inheritdoc/>
    public override OdinValueType Type => OdinValueType.Date;

    /// <inheritdoc/>
    public override OdinValue WithModifiers(OdinModifiers? modifiers) =>
        new OdinDate(Year, Month, Day, Raw) { Modifiers = modifiers, Directives = Directives };

    /// <inheritdoc/>
    public override OdinValue WithDirectives(IReadOnlyList<OdinDirective> directives) =>
        new OdinDate(Year, Month, Day, Raw) { Modifiers = Modifiers, Directives = directives };

    /// <inheritdoc/>
    public override string ToString() => Raw;
}

/// <summary>Timestamp value (2024-06-15T14:30:00Z).</summary>
public sealed class OdinTimestamp : OdinValue
{
    /// <summary>Milliseconds since Unix epoch.</summary>
    public long EpochMs { get; }

    /// <summary>Original string representation (required for round-trip).</summary>
    public string Raw { get; }

    /// <summary>Creates a timestamp value.</summary>
    public OdinTimestamp(long epochMs, string raw)
    {
        EpochMs = epochMs;
        Raw = raw;
    }

    /// <inheritdoc/>
    public override OdinValueType Type => OdinValueType.Timestamp;

    /// <inheritdoc/>
    public override OdinValue WithModifiers(OdinModifiers? modifiers) =>
        new OdinTimestamp(EpochMs, Raw) { Modifiers = modifiers, Directives = Directives };

    /// <inheritdoc/>
    public override OdinValue WithDirectives(IReadOnlyList<OdinDirective> directives) =>
        new OdinTimestamp(EpochMs, Raw) { Modifiers = Modifiers, Directives = directives };

    /// <inheritdoc/>
    public override string ToString() => Raw;
}

/// <summary>Time value (T14:30:00).</summary>
public sealed class OdinTime : OdinValue
{
    /// <summary>Time string with T prefix.</summary>
    public string Value { get; }

    /// <summary>Creates a time value.</summary>
    public OdinTime(string value) { Value = value; }

    /// <inheritdoc/>
    public override OdinValueType Type => OdinValueType.Time;

    /// <inheritdoc/>
    public override OdinValue WithModifiers(OdinModifiers? modifiers) =>
        new OdinTime(Value) { Modifiers = modifiers, Directives = Directives };

    /// <inheritdoc/>
    public override OdinValue WithDirectives(IReadOnlyList<OdinDirective> directives) =>
        new OdinTime(Value) { Modifiers = Modifiers, Directives = directives };

    /// <inheritdoc/>
    public override string ToString() => Value;
}

/// <summary>Duration value (P1Y6M, PT30M).</summary>
public sealed class OdinDuration : OdinValue
{
    /// <summary>Duration string with P prefix.</summary>
    public string Value { get; }

    /// <summary>Creates a duration value.</summary>
    public OdinDuration(string value) { Value = value; }

    /// <inheritdoc/>
    public override OdinValueType Type => OdinValueType.Duration;

    /// <inheritdoc/>
    public override OdinValue WithModifiers(OdinModifiers? modifiers) =>
        new OdinDuration(Value) { Modifiers = modifiers, Directives = Directives };

    /// <inheritdoc/>
    public override OdinValue WithDirectives(IReadOnlyList<OdinDirective> directives) =>
        new OdinDuration(Value) { Modifiers = Modifiers, Directives = directives };

    /// <inheritdoc/>
    public override string ToString() => Value;
}

/// <summary>Reference to another path (@policy.id).</summary>
public sealed class OdinReference : OdinValue
{
    /// <summary>Target path (without @ prefix).</summary>
    public string Path { get; }

    /// <summary>Creates a reference value.</summary>
    public OdinReference(string path) { Path = path; }

    /// <inheritdoc/>
    public override OdinValueType Type => OdinValueType.Reference;

    /// <inheritdoc/>
    public override string? AsReference() => Path;

    /// <inheritdoc/>
    public override OdinValue WithModifiers(OdinModifiers? modifiers) =>
        new OdinReference(Path) { Modifiers = modifiers, Directives = Directives };

    /// <inheritdoc/>
    public override OdinValue WithDirectives(IReadOnlyList<OdinDirective> directives) =>
        new OdinReference(Path) { Modifiers = Modifiers, Directives = directives };

    /// <inheritdoc/>
    public override string ToString() => $"@{Path}";
}

/// <summary>Binary data (^SGVsbG8=, ^sha256:abc123...).</summary>
public sealed class OdinBinary : OdinValue
{
    /// <summary>Decoded binary data.</summary>
    public byte[] Data { get; }

    /// <summary>Algorithm if specified (e.g., "sha256").</summary>
    public string? Algorithm { get; init; }

    /// <summary>Creates a binary value.</summary>
    public OdinBinary(byte[] data) { Data = data; }

    /// <inheritdoc/>
    public override OdinValueType Type => OdinValueType.Binary;

    /// <inheritdoc/>
    public override OdinValue WithModifiers(OdinModifiers? modifiers) =>
        new OdinBinary(Data) { Algorithm = Algorithm, Modifiers = modifiers, Directives = Directives };

    /// <inheritdoc/>
    public override OdinValue WithDirectives(IReadOnlyList<OdinDirective> directives) =>
        new OdinBinary(Data) { Algorithm = Algorithm, Modifiers = Modifiers, Directives = directives };

    /// <inheritdoc/>
    public override string ToString() => Algorithm != null ? $"^{Algorithm}:<data>" : "^<data>";
}

/// <summary>Verb expression (%upper @name).</summary>
public sealed class OdinVerb : OdinValue
{
    /// <summary>Verb name (e.g., "upper", "concat").</summary>
    public string Name { get; }

    /// <summary>Whether this is a custom verb (%&amp;namespace.verb).</summary>
    public bool IsCustom { get; init; }

    /// <summary>Parsed arguments (can include nested verb expressions).</summary>
    public IReadOnlyList<OdinValue> Args { get; }

    /// <summary>Creates a verb expression value.</summary>
    public OdinVerb(string name, IReadOnlyList<OdinValue> args)
    {
        Name = name;
        Args = args;
    }

    /// <inheritdoc/>
    public override OdinValueType Type => OdinValueType.Verb;

    /// <inheritdoc/>
    public override OdinValue WithModifiers(OdinModifiers? modifiers) =>
        new OdinVerb(Name, Args) { IsCustom = IsCustom, Modifiers = modifiers, Directives = Directives };

    /// <inheritdoc/>
    public override OdinValue WithDirectives(IReadOnlyList<OdinDirective> directives) =>
        new OdinVerb(Name, Args) { IsCustom = IsCustom, Modifiers = Modifiers, Directives = directives };

    /// <inheritdoc/>
    public override string ToString() => $"%{Name}";
}

/// <summary>Array of values.</summary>
public sealed class OdinArray : OdinValue
{
    /// <summary>Ordered array elements.</summary>
    public IReadOnlyList<OdinArrayItem> Items { get; }

    /// <summary>Creates an array value.</summary>
    public OdinArray(IReadOnlyList<OdinArrayItem> items) { Items = items; }

    /// <inheritdoc/>
    public override OdinValueType Type => OdinValueType.Array;

    /// <inheritdoc/>
    public override IReadOnlyList<OdinArrayItem>? AsArray() => Items;

    /// <inheritdoc/>
    public override OdinValue WithModifiers(OdinModifiers? modifiers) =>
        new OdinArray(Items) { Modifiers = modifiers, Directives = Directives };

    /// <inheritdoc/>
    public override OdinValue WithDirectives(IReadOnlyList<OdinDirective> directives) =>
        new OdinArray(Items) { Modifiers = Modifiers, Directives = directives };

    /// <inheritdoc/>
    public override string ToString() => $"[{Items.Count} items]";
}

/// <summary>Object value (nested key-value pairs).</summary>
public sealed class OdinObject : OdinValue
{
    /// <summary>Ordered key-value pairs.</summary>
    public IReadOnlyList<KeyValuePair<string, OdinValue>> Fields { get; }

    /// <summary>Creates an object value.</summary>
    public OdinObject(IReadOnlyList<KeyValuePair<string, OdinValue>> fields) { Fields = fields; }

    /// <inheritdoc/>
    public override OdinValueType Type => OdinValueType.Object;

    /// <inheritdoc/>
    public override OdinValue WithModifiers(OdinModifiers? modifiers) =>
        new OdinObject(Fields) { Modifiers = modifiers, Directives = Directives };

    /// <inheritdoc/>
    public override OdinValue WithDirectives(IReadOnlyList<OdinDirective> directives) =>
        new OdinObject(Fields) { Modifiers = Modifiers, Directives = directives };

    /// <inheritdoc/>
    public override string ToString() => $"{{{Fields.Count} fields}}";
}

// ─────────────────────────────────────────────────────────────────────────────
// Array Item (Record vs Value)
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Array item type — either a record (map of fields) or a direct value.
/// </summary>
public abstract class OdinArrayItem
{
    /// <summary>Creates a record array item.</summary>
    public static OdinArrayItem Record(IReadOnlyList<KeyValuePair<string, OdinValue>> fields) =>
        new OdinArrayRecord(fields);

    /// <summary>Creates a value array item.</summary>
    public static OdinArrayItem FromValue(OdinValue value) =>
        new OdinArrayValue(value);

    /// <summary>Gets the record fields if this is a record item.</summary>
    public virtual IReadOnlyList<KeyValuePair<string, OdinValue>>? AsRecord() => null;

    /// <summary>Gets the value if this is a value item.</summary>
    public virtual OdinValue? AsValue() => null;
}

/// <summary>A record array item with named fields.</summary>
public sealed class OdinArrayRecord : OdinArrayItem
{
    /// <summary>The record fields.</summary>
    public IReadOnlyList<KeyValuePair<string, OdinValue>> Fields { get; }

    /// <summary>Creates a record array item.</summary>
    public OdinArrayRecord(IReadOnlyList<KeyValuePair<string, OdinValue>> fields) { Fields = fields; }

    /// <inheritdoc/>
    public override IReadOnlyList<KeyValuePair<string, OdinValue>>? AsRecord() => Fields;
}

/// <summary>A direct value array item.</summary>
public sealed class OdinArrayValue : OdinArrayItem
{
    /// <summary>The value.</summary>
    public OdinValue Value { get; }

    /// <summary>Creates a value array item.</summary>
    public OdinArrayValue(OdinValue value) { Value = value; }

    /// <inheritdoc/>
    public override OdinValue? AsValue() => Value;
}

// ─────────────────────────────────────────────────────────────────────────────
// Factory (OdinValues)
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Factory methods for creating ODIN values with sensible defaults.
/// </summary>
public static class OdinValues
{
    /// <summary>Create a null value.</summary>
    public static OdinNull Null() => new();

    /// <summary>Create a boolean value.</summary>
    public static OdinBoolean Boolean(bool value) => new(value);

    /// <summary>Create a string value.</summary>
    public static OdinString String(string value) => new(value);

    /// <summary>Create an integer value.</summary>
    public static OdinInteger Integer(long value) => new(value);

    /// <summary>Create an integer from raw string (preserves original for large values).</summary>
    public static OdinInteger IntegerFromStr(string raw)
    {
        long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value);
        return new OdinInteger(value) { Raw = raw };
    }

    /// <summary>Create a number value.</summary>
    public static OdinNumber Number(double value) => new(value);

    /// <summary>Create a number with decimal places.</summary>
    public static OdinNumber NumberWithPlaces(double value, byte decimalPlaces) =>
        new(value) { DecimalPlaces = decimalPlaces };

    /// <summary>Create a currency value.</summary>
    public static OdinCurrency Currency(double value, byte decimalPlaces = 2) =>
        new(value) { DecimalPlaces = decimalPlaces };

    /// <summary>Create a currency value with currency code.</summary>
    public static OdinCurrency CurrencyWithCode(double value, byte decimalPlaces, string code) =>
        new(value) { DecimalPlaces = decimalPlaces, CurrencyCode = code };

    /// <summary>Create a percent value (0-1 range, 0.15 = 15%).</summary>
    public static OdinPercent Percent(double value) => new(value);

    /// <summary>Create a date value from components.</summary>
    public static OdinDate Date(int year, byte month, byte day) => new(year, month, day);

    /// <summary>Create a date value from a raw string.</summary>
    public static OdinDate? DateFromStr(string raw)
    {
        var parts = raw.Split('-');
        if (parts.Length != 3) return null;
        if (!int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var year)) return null;
        if (!byte.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var month)) return null;
        if (!byte.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var day)) return null;
        return new OdinDate(year, month, day, raw);
    }

    /// <summary>Create a timestamp value.</summary>
    public static OdinTimestamp Timestamp(long epochMs, string raw) => new(epochMs, raw);

    /// <summary>Create a time value.</summary>
    public static OdinTime Time(string value) => new(value);

    /// <summary>Create a duration value.</summary>
    public static OdinDuration Duration(string value) => new(value);

    /// <summary>Create a reference value (path without @ prefix).</summary>
    public static OdinReference Reference(string path) => new(path);

    /// <summary>Create a binary value.</summary>
    public static OdinBinary Binary(byte[] data) => new(data);

    /// <summary>Create a binary value with algorithm tag.</summary>
    public static OdinBinary BinaryWithAlgorithm(byte[] data, string algorithm) =>
        new(data) { Algorithm = algorithm };

    /// <summary>Create a verb expression.</summary>
    public static OdinVerb Verb(string name, IReadOnlyList<OdinValue> args) => new(name, args);

    /// <summary>Create a custom verb expression.</summary>
    public static OdinVerb CustomVerb(string name, IReadOnlyList<OdinValue> args) =>
        new(name, args) { IsCustom = true };

    /// <summary>Create an array value.</summary>
    public static OdinArray Array(IReadOnlyList<OdinArrayItem> items) => new(items);

    /// <summary>Create an object value.</summary>
    public static OdinObject Object(IReadOnlyList<KeyValuePair<string, OdinValue>> fields) => new(fields);
}
