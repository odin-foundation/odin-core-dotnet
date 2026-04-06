using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;

namespace Odin.Core.Types;

/// <summary>
/// Type discriminator for <see cref="DynValue"/> variants.
/// </summary>
public enum DynValueType
{
    /// <summary>Null value.</summary>
    Null,
    /// <summary>Boolean value.</summary>
    Bool,
    /// <summary>Integer value (i64).</summary>
    Integer,
    /// <summary>Floating-point value.</summary>
    Float,
    /// <summary>Float with preserved raw string (for values exceeding double precision).</summary>
    FloatRaw,
    /// <summary>Currency value with decimal places and optional currency code.</summary>
    Currency,
    /// <summary>Currency with preserved raw string.</summary>
    CurrencyRaw,
    /// <summary>Percent value.</summary>
    Percent,
    /// <summary>Reference path (for ODIN @path output).</summary>
    Reference,
    /// <summary>Binary data as base64 string.</summary>
    Binary,
    /// <summary>Date string (YYYY-MM-DD).</summary>
    Date,
    /// <summary>Timestamp string (ISO 8601).</summary>
    Timestamp,
    /// <summary>Time string (HH:MM:SS).</summary>
    Time,
    /// <summary>Duration string (ISO 8601 P...).</summary>
    Duration,
    /// <summary>String value.</summary>
    String,
    /// <summary>Array of values.</summary>
    Array,
    /// <summary>Object with ordered key-value pairs.</summary>
    Object,
}

/// <summary>
/// A dynamic value used in transform I/O. This is a tagged union (type enum + typed accessors)
/// that mirrors the Rust DynValue enum. All format conversions route through this type
/// as part of the ODIN Canonical Data Model.
/// </summary>
public sealed class DynValue : IEquatable<DynValue>
{
    /// <summary>
    /// Gets the type discriminator for this value.
    /// </summary>
    public DynValueType Type { get; }

    // Payload fields — at most one is meaningful per variant.
    private readonly bool _boolValue;
    private readonly long _intValue;
    private readonly double _floatValue;
    private readonly byte _decimalPlaces;
    private readonly string? _stringValue;
    private readonly string? _currencyCode;
    private readonly List<DynValue>? _arrayValue;
    private readonly List<KeyValuePair<string, DynValue>>? _objectValue;

    private DynValue(DynValueType type)
    {
        Type = type;
    }

    private DynValue(DynValueType type, bool boolValue) : this(type)
    {
        _boolValue = boolValue;
    }

    private DynValue(DynValueType type, long intValue) : this(type)
    {
        _intValue = intValue;
    }

    private DynValue(DynValueType type, double floatValue) : this(type)
    {
        _floatValue = floatValue;
    }

    private DynValue(DynValueType type, string stringValue) : this(type)
    {
        _stringValue = stringValue ?? throw new ArgumentNullException(nameof(stringValue));
    }

    private DynValue(DynValueType type, double floatValue, byte decimalPlaces, string? currencyCode) : this(type)
    {
        _floatValue = floatValue;
        _decimalPlaces = decimalPlaces;
        _currencyCode = currencyCode;
    }

    private DynValue(DynValueType type, string stringValue, byte decimalPlaces, string? currencyCode) : this(type)
    {
        _stringValue = stringValue ?? throw new ArgumentNullException(nameof(stringValue));
        _decimalPlaces = decimalPlaces;
        _currencyCode = currencyCode;
    }

    private DynValue(DynValueType type, List<DynValue> arrayValue) : this(type)
    {
        _arrayValue = arrayValue ?? throw new ArgumentNullException(nameof(arrayValue));
    }

    private DynValue(DynValueType type, List<KeyValuePair<string, DynValue>> objectValue) : this(type)
    {
        _objectValue = objectValue ?? throw new ArgumentNullException(nameof(objectValue));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Static Factory Methods
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Creates a null DynValue.</summary>
    public static DynValue Null() => new DynValue(DynValueType.Null);

    /// <summary>Creates a boolean DynValue.</summary>
    /// <param name="value">The boolean value.</param>
    public static DynValue Bool(bool value) => new DynValue(DynValueType.Bool, value);

    /// <summary>Creates an integer DynValue.</summary>
    /// <param name="value">The 64-bit integer value.</param>
    public static DynValue Integer(long value) => new DynValue(DynValueType.Integer, value);

    /// <summary>Creates a floating-point DynValue.</summary>
    /// <param name="value">The double-precision floating-point value.</param>
    public static DynValue Float(double value) => new DynValue(DynValueType.Float, value);

    /// <summary>Creates a float DynValue with preserved raw string for values exceeding double precision.</summary>
    /// <param name="raw">The raw string representation of the number.</param>
    public static DynValue FloatRaw(string raw) => new DynValue(DynValueType.FloatRaw, raw);

    /// <summary>Creates a currency DynValue.</summary>
    /// <param name="value">The currency amount as a double.</param>
    /// <param name="decimalPlaces">Number of decimal places (default 2).</param>
    /// <param name="currencyCode">Optional ISO currency code (e.g., "USD").</param>
    public static DynValue Currency(double value, byte decimalPlaces = 2, string? currencyCode = null)
        => new DynValue(DynValueType.Currency, value, decimalPlaces, currencyCode);

    /// <summary>Creates a currency DynValue with preserved raw string for values exceeding double precision.</summary>
    /// <param name="raw">The raw string representation of the currency amount.</param>
    /// <param name="decimalPlaces">Number of decimal places (default 2).</param>
    /// <param name="currencyCode">Optional ISO currency code (e.g., "USD").</param>
    public static DynValue CurrencyRaw(string raw, byte decimalPlaces = 2, string? currencyCode = null)
        => new DynValue(DynValueType.CurrencyRaw, raw, decimalPlaces, currencyCode);

    /// <summary>Creates a percent DynValue.</summary>
    /// <param name="value">The percentage as a decimal (0.15 = 15%).</param>
    public static DynValue Percent(double value) => new DynValue(DynValueType.Percent, value);

    /// <summary>Creates a reference DynValue for ODIN @path expressions.</summary>
    /// <param name="path">The reference path (without @ prefix).</param>
    public static DynValue Reference(string path) => new DynValue(DynValueType.Reference, path);

    /// <summary>Creates a binary DynValue from a base64 string.</summary>
    /// <param name="base64">The base64-encoded binary data.</param>
    public static DynValue Binary(string base64) => new DynValue(DynValueType.Binary, base64);

    /// <summary>Creates a date DynValue.</summary>
    /// <param name="date">The date string in YYYY-MM-DD format.</param>
    public static DynValue Date(string date) => new DynValue(DynValueType.Date, date);

    /// <summary>Creates a timestamp DynValue.</summary>
    /// <param name="timestamp">The ISO 8601 timestamp string.</param>
    public static DynValue Timestamp(string timestamp) => new DynValue(DynValueType.Timestamp, timestamp);

    /// <summary>Creates a time DynValue.</summary>
    /// <param name="time">The time string in HH:MM:SS format.</param>
    public static DynValue Time(string time) => new DynValue(DynValueType.Time, time);

    /// <summary>Creates a duration DynValue.</summary>
    /// <param name="duration">The ISO 8601 duration string (e.g., "P1Y6M", "PT30M").</param>
    public static DynValue Duration(string duration) => new DynValue(DynValueType.Duration, duration);

    /// <summary>Creates a string DynValue.</summary>
    /// <param name="value">The string value.</param>
    public static DynValue String(string value) => new DynValue(DynValueType.String, value);

    /// <summary>Creates an array DynValue.</summary>
    /// <param name="items">The list of array elements.</param>
    public static DynValue Array(List<DynValue> items) => new DynValue(DynValueType.Array, items);

    /// <summary>Creates an object DynValue with ordered key-value pairs.</summary>
    /// <param name="entries">The ordered list of key-value pairs.</param>
    public static DynValue Object(List<KeyValuePair<string, DynValue>> entries)
        => new DynValue(DynValueType.Object, entries);

    // ─────────────────────────────────────────────────────────────────────────
    // Type Checks
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Returns true if this value is null.</summary>
    public bool IsNull => Type == DynValueType.Null;

    // ─────────────────────────────────────────────────────────────────────────
    // Typed Accessors
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Tries to extract a string value. Returns the string payload for String, Reference,
    /// Binary, Date, Timestamp, Time, Duration, FloatRaw, and CurrencyRaw variants.
    /// Returns null for other types.
    /// </summary>
    public string? AsString()
    {
        switch (Type)
        {
            case DynValueType.String:
            case DynValueType.Reference:
            case DynValueType.Binary:
            case DynValueType.Date:
            case DynValueType.Timestamp:
            case DynValueType.Time:
            case DynValueType.Duration:
            case DynValueType.FloatRaw:
            case DynValueType.CurrencyRaw:
                return _stringValue;
            default:
                return null;
        }
    }

    /// <summary>
    /// Tries to extract a 64-bit integer value. Returns the value for Integer variants only.
    /// </summary>
    public long? AsInt64()
    {
        if (Type == DynValueType.Integer)
            return _intValue;
        return null;
    }

    /// <summary>
    /// Tries to extract a double value. Returns the value for Float, Currency, Percent,
    /// and Integer variants. For FloatRaw and CurrencyRaw, attempts to parse the raw string.
    /// Returns null for other types.
    /// </summary>
    public double? AsDouble()
    {
        switch (Type)
        {
            case DynValueType.Float:
            case DynValueType.Currency:
            case DynValueType.Percent:
                return _floatValue;
            case DynValueType.Integer:
                return (double)_intValue;
            case DynValueType.FloatRaw:
            case DynValueType.CurrencyRaw:
                if (_stringValue != null && double.TryParse(_stringValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
                    return parsed;
                return null;
            default:
                return null;
        }
    }

    /// <summary>
    /// Tries to extract a boolean value. Returns the value for Bool variants only.
    /// </summary>
    public bool? AsBool()
    {
        if (Type == DynValueType.Bool)
            return _boolValue;
        return null;
    }

    /// <summary>
    /// Gets the currency code for Currency/CurrencyRaw variants. Returns null otherwise.
    /// </summary>
    public string? GetCurrencyCode()
    {
        if (Type == DynValueType.Currency || Type == DynValueType.CurrencyRaw)
            return _currencyCode;
        return null;
    }

    /// <summary>
    /// Gets the decimal places for Currency/CurrencyRaw variants. Returns 2 by default.
    /// </summary>
    public byte GetDecimalPlaces()
    {
        if (Type == DynValueType.Currency || Type == DynValueType.CurrencyRaw)
            return _decimalPlaces;
        return 2;
    }

    /// <summary>
    /// Tries to get the array items. Returns the list for Array variants only.
    /// </summary>
    public List<DynValue>? AsArray()
    {
        if (Type == DynValueType.Array)
            return _arrayValue;
        return null;
    }

    /// <summary>
    /// Tries to get the object entries. Returns the ordered key-value list for Object variants only.
    /// </summary>
    public List<KeyValuePair<string, DynValue>>? AsObject()
    {
        if (Type == DynValueType.Object)
            return _objectValue;
        return null;
    }

    /// <summary>
    /// Gets a field from an object by key. Returns null if this is not an object
    /// or the key is not found.
    /// </summary>
    /// <param name="key">The field name to look up.</param>
    public DynValue? Get(string key)
    {
        if (_objectValue == null)
            return null;
        for (int i = 0; i < _objectValue.Count; i++)
        {
            if (_objectValue[i].Key == key)
                return _objectValue[i].Value;
        }
        return null;
    }

    /// <summary>
    /// Gets an element from an array by index. Returns null if this is not an array
    /// or the index is out of range.
    /// </summary>
    /// <param name="index">The zero-based index into the array.</param>
    public DynValue? GetIndex(int index)
    {
        if (_arrayValue == null || index < 0 || index >= _arrayValue.Count)
            return null;
        return _arrayValue[index];
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Extract Methods (parse string-encoded arrays/objects)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Extracts an array from this value. If this is an Array, returns the items.
    /// If this is a String starting with '[', attempts to parse it as a JSON/ODIN array.
    /// Returns null otherwise.
    /// </summary>
    public List<DynValue>? ExtractArray()
    {
        if (Type == DynValueType.Array)
            return _arrayValue != null ? new List<DynValue>(_arrayValue) : null;
        if (Type == DynValueType.String && _stringValue != null)
        {
            var trimmed = _stringValue.Trim();
            if (trimmed.Length >= 2 && trimmed[0] == '[' && trimmed[trimmed.Length - 1] == ']')
                return ParseArrayString(trimmed);
        }
        return null;
    }

    /// <summary>
    /// Extracts an object from this value. If this is an Object, returns the entries.
    /// If this is a String starting with '{', attempts to parse it as a JSON/ODIN object.
    /// Returns null otherwise.
    /// </summary>
    public List<KeyValuePair<string, DynValue>>? ExtractObject()
    {
        if (Type == DynValueType.Object)
            return _objectValue != null ? new List<KeyValuePair<string, DynValue>>(_objectValue) : null;
        if (Type == DynValueType.String && _stringValue != null)
        {
            var trimmed = _stringValue.Trim();
            if (trimmed.Length >= 2 && trimmed[0] == '{' && trimmed[trimmed.Length - 1] == '}')
                return ParseObjectString(trimmed);
        }
        return null;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // System.Text.Json Integration
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a DynValue from a <see cref="JsonElement"/>. Maps JSON types to the
    /// closest DynValue variant (numbers become Integer or Float).
    /// </summary>
    /// <param name="element">The JSON element to convert.</param>
    public static DynValue FromJsonElement(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Null:
            case JsonValueKind.Undefined:
                return Null();

            case JsonValueKind.True:
                return Bool(true);

            case JsonValueKind.False:
                return Bool(false);

            case JsonValueKind.Number:
                if (element.TryGetInt64(out long intVal))
                    return Integer(intVal);
                if (element.TryGetDouble(out double dblVal))
                    return Float(dblVal);
                // Fallback for very large numbers
                return FloatRaw(element.GetRawText());

            case JsonValueKind.String:
                return String(element.GetString() ?? "");

            case JsonValueKind.Array:
                var items = new List<DynValue>();
                foreach (var item in element.EnumerateArray())
                    items.Add(FromJsonElement(item));
                return Array(items);

            case JsonValueKind.Object:
                var entries = new List<KeyValuePair<string, DynValue>>();
                foreach (var prop in element.EnumerateObject())
                    entries.Add(new KeyValuePair<string, DynValue>(prop.Name, FromJsonElement(prop.Value)));
                return Object(entries);

            default:
                return Null();
        }
    }

    /// <summary>
    /// Converts this DynValue to a <see cref="JsonElement"/> via an intermediate JSON document.
    /// </summary>
    public JsonElement ToJsonElement()
    {
        using var buffer = new System.IO.MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            WriteJsonValue(writer);
        }
        buffer.Position = 0;
        using var doc = JsonDocument.Parse(buffer);
        return doc.RootElement.Clone();
    }

    /// <summary>
    /// Writes this DynValue to a <see cref="Utf8JsonWriter"/>.
    /// </summary>
    /// <param name="writer">The JSON writer to write to.</param>
    internal void WriteJsonValue(Utf8JsonWriter writer)
    {
        switch (Type)
        {
            case DynValueType.Null:
                writer.WriteNullValue();
                break;

            case DynValueType.Bool:
                writer.WriteBooleanValue(_boolValue);
                break;

            case DynValueType.Integer:
                writer.WriteNumberValue(_intValue);
                break;

            case DynValueType.Float:
            case DynValueType.Currency:
            case DynValueType.Percent:
                writer.WriteNumberValue(_floatValue);
                break;

            case DynValueType.FloatRaw:
            case DynValueType.CurrencyRaw:
                if (_stringValue != null && double.TryParse(_stringValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var f))
                    writer.WriteNumberValue(f);
                else
                    writer.WriteStringValue(_stringValue);
                break;

            case DynValueType.String:
            case DynValueType.Reference:
            case DynValueType.Binary:
            case DynValueType.Date:
            case DynValueType.Timestamp:
            case DynValueType.Time:
            case DynValueType.Duration:
                writer.WriteStringValue(_stringValue);
                break;

            case DynValueType.Array:
                writer.WriteStartArray();
                if (_arrayValue != null)
                {
                    for (int i = 0; i < _arrayValue.Count; i++)
                        _arrayValue[i].WriteJsonValue(writer);
                }
                writer.WriteEndArray();
                break;

            case DynValueType.Object:
                writer.WriteStartObject();
                if (_objectValue != null)
                {
                    for (int i = 0; i < _objectValue.Count; i++)
                    {
                        writer.WritePropertyName(_objectValue[i].Key);
                        _objectValue[i].Value.WriteJsonValue(writer);
                    }
                }
                writer.WriteEndObject();
                break;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Equality
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Determines whether the specified DynValue is equal to this instance.
    /// </summary>
    /// <param name="other">The DynValue to compare with.</param>
    public bool Equals(DynValue? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        if (Type != other.Type) return false;

        switch (Type)
        {
            case DynValueType.Null:
                return true;
            case DynValueType.Bool:
                return _boolValue == other._boolValue;
            case DynValueType.Integer:
                return _intValue == other._intValue;
            case DynValueType.Float:
            case DynValueType.Percent:
                // ReSharper disable once CompareOfFloatsByEqualityOperator
                return _floatValue == other._floatValue;
            case DynValueType.Currency:
                // ReSharper disable once CompareOfFloatsByEqualityOperator
                return _floatValue == other._floatValue
                    && _decimalPlaces == other._decimalPlaces
                    && _currencyCode == other._currencyCode;
            case DynValueType.FloatRaw:
            case DynValueType.String:
            case DynValueType.Reference:
            case DynValueType.Binary:
            case DynValueType.Date:
            case DynValueType.Timestamp:
            case DynValueType.Time:
            case DynValueType.Duration:
                return _stringValue == other._stringValue;
            case DynValueType.CurrencyRaw:
                return _stringValue == other._stringValue
                    && _decimalPlaces == other._decimalPlaces
                    && _currencyCode == other._currencyCode;
            case DynValueType.Array:
                return ArrayEquals(_arrayValue, other._arrayValue);
            case DynValueType.Object:
                return ObjectEquals(_objectValue, other._objectValue);
            default:
                return false;
        }
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is DynValue other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        switch (Type)
        {
            case DynValueType.Null:
                return Type.GetHashCode();
            case DynValueType.Bool:
                return HashCode.Combine(Type, _boolValue);
            case DynValueType.Integer:
                return HashCode.Combine(Type, _intValue);
            case DynValueType.Float:
            case DynValueType.Percent:
                return HashCode.Combine(Type, _floatValue);
            case DynValueType.Currency:
                return HashCode.Combine(Type, _floatValue, _decimalPlaces, _currencyCode);
            case DynValueType.CurrencyRaw:
                return HashCode.Combine(Type, _stringValue, _decimalPlaces, _currencyCode);
            default:
                return HashCode.Combine(Type, _stringValue);
        }
    }

    /// <summary>
    /// Determines whether two DynValue instances are equal.
    /// </summary>
    public static bool operator ==(DynValue? left, DynValue? right)
    {
        if (left is null) return right is null;
        return left.Equals(right);
    }

    /// <summary>
    /// Determines whether two DynValue instances are not equal.
    /// </summary>
    public static bool operator !=(DynValue? left, DynValue? right) => !(left == right);

    /// <inheritdoc/>
    public override string ToString()
    {
        switch (Type)
        {
            case DynValueType.Null: return "null";
            case DynValueType.Bool: return _boolValue ? "true" : "false";
            case DynValueType.Integer: return _intValue.ToString(CultureInfo.InvariantCulture);
            case DynValueType.Float: return _floatValue.ToString(CultureInfo.InvariantCulture);
            case DynValueType.FloatRaw: return _stringValue ?? "";
            case DynValueType.Currency:
                var cval = _floatValue.ToString(CultureInfo.InvariantCulture);
                return _currencyCode != null ? $"#${cval}:{_currencyCode}" : $"#${cval}";
            case DynValueType.CurrencyRaw:
                return _currencyCode != null ? $"#${_stringValue}:{_currencyCode}" : $"#${_stringValue}";
            case DynValueType.Percent:
                return $"#%{_floatValue.ToString(CultureInfo.InvariantCulture)}";
            case DynValueType.Reference: return $"@{_stringValue}";
            case DynValueType.Binary: return $"^{_stringValue}";
            case DynValueType.String: return $"\"{_stringValue}\"";
            case DynValueType.Date:
            case DynValueType.Timestamp:
            case DynValueType.Time:
            case DynValueType.Duration:
                return _stringValue ?? "";
            case DynValueType.Array:
                return $"[{_arrayValue?.Count ?? 0} items]";
            case DynValueType.Object:
                return $"{{{_objectValue?.Count ?? 0} fields}}";
            default:
                return Type.ToString();
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Internal: String-encoded array/object parsing
    // ─────────────────────────────────────────────────────────────────────────

    private static List<DynValue>? ParseArrayString(string s)
    {
        var trimmed = s.Trim();
        if (trimmed.Length < 2 || trimmed[0] != '[' || trimmed[trimmed.Length - 1] != ']')
            return null;
        var inner = trimmed.Substring(1, trimmed.Length - 2);
        var items = SplitTopLevel(inner);
        var result = new List<DynValue>();
        for (int i = 0; i < items.Count; i++)
        {
            var item = items[i].Trim();
            if (item.Length == 0) continue;
            result.Add(ParseElement(item));
        }
        return result;
    }

    private static List<KeyValuePair<string, DynValue>>? ParseObjectString(string s)
    {
        var trimmed = s.Trim();
        if (trimmed.Length < 2 || trimmed[0] != '{' || trimmed[trimmed.Length - 1] != '}')
            return null;
        var inner = trimmed.Substring(1, trimmed.Length - 2).Trim();
        if (inner.Length == 0)
            return new List<KeyValuePair<string, DynValue>>();
        var pairs = SplitTopLevel(inner);
        var result = new List<KeyValuePair<string, DynValue>>();
        for (int i = 0; i < pairs.Count; i++)
        {
            var pair = pairs[i].Trim();
            if (pair.Length == 0) continue;
            int colonPos = FindColonSeparator(pair);
            if (colonPos < 0) continue;
            var keyStr = pair.Substring(0, colonPos).Trim();
            var valStr = pair.Substring(colonPos + 1).Trim();
            string key;
            if (keyStr.Length >= 2 && keyStr[0] == '"' && keyStr[keyStr.Length - 1] == '"')
                key = UnescapeString(keyStr.Substring(1, keyStr.Length - 2));
            else
                key = keyStr;
            result.Add(new KeyValuePair<string, DynValue>(key, ParseElement(valStr)));
        }
        return result;
    }

    private static List<string> SplitTopLevel(string s)
    {
        var items = new List<string>();
        int depth = 0;
        bool inString = false;
        bool escape = false;
        int start = 0;
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (escape) { escape = false; continue; }
            if (c == '\\' && inString) { escape = true; continue; }
            if (c == '"') { inString = !inString; continue; }
            if (!inString)
            {
                if (c == '[' || c == '{') depth++;
                else if (c == ']' || c == '}') depth--;
                else if (c == ',' && depth == 0)
                {
                    items.Add(s.Substring(start, i - start));
                    start = i + 1;
                }
            }
        }
        if (start < s.Length)
            items.Add(s.Substring(start));
        return items;
    }

    private static DynValue ParseElement(string s)
    {
        s = s.Trim();
        if (s == "~" || s == "null") return Null();
        if (s == "?true" || s == "true") return Bool(true);
        if (s == "?false" || s == "false") return Bool(false);

        // ODIN integer: ##N
        if (s.Length > 2 && s[0] == '#' && s[1] == '#')
        {
            var rest = s.Substring(2);
            if (long.TryParse(rest, NumberStyles.Integer, CultureInfo.InvariantCulture, out long intVal))
                return Integer(intVal);
        }

        // ODIN currency: #$N
        if (s.Length > 2 && s[0] == '#' && s[1] == '$')
        {
            var rest = s.Substring(2);
            if (double.TryParse(rest, NumberStyles.Float, CultureInfo.InvariantCulture, out double curVal))
                return Float(curVal);
        }

        // ODIN number: #N
        if (s.Length > 1 && s[0] == '#')
        {
            var rest = s.Substring(1);
            if (double.TryParse(rest, NumberStyles.Float, CultureInfo.InvariantCulture, out double numVal))
                return Float(numVal);
        }

        // Quoted string
        if (s.Length >= 2 && s[0] == '"' && s[s.Length - 1] == '"')
            return String(UnescapeString(s.Substring(1, s.Length - 2)));

        // Nested array
        if (s.Length >= 2 && s[0] == '[' && s[s.Length - 1] == ']')
        {
            var arr = ParseArrayString(s);
            if (arr != null) return Array(arr);
        }

        // Plain integer
        if (long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out long plainInt))
            return Integer(plainInt);

        // Plain float
        if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double plainFloat))
            return Float(plainFloat);

        // Nested object
        if (s.Length >= 2 && s[0] == '{' && s[s.Length - 1] == '}')
        {
            var obj = ParseObjectString(s);
            if (obj != null) return Object(obj);
        }

        // Fallback: bare string
        return String(s);
    }

    private static string UnescapeString(string s)
    {
        return s.Replace("\\\"", "\"")
                .Replace("\\\\", "\\")
                .Replace("\\n", "\n")
                .Replace("\\t", "\t")
                .Replace("\\r", "\r");
    }

    private static int FindColonSeparator(string s)
    {
        bool inString = false;
        bool escape = false;
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (escape) { escape = false; continue; }
            if (c == '\\' && inString) { escape = true; continue; }
            if (c == '"') { inString = !inString; continue; }
            if (c == ':' && !inString) return i;
        }
        return -1;
    }

    private static bool ArrayEquals(List<DynValue>? a, List<DynValue>? b)
    {
        if (a is null && b is null) return true;
        if (a is null || b is null) return false;
        if (a.Count != b.Count) return false;
        for (int i = 0; i < a.Count; i++)
        {
            if (!a[i].Equals(b[i])) return false;
        }
        return true;
    }

    private static bool ObjectEquals(List<KeyValuePair<string, DynValue>>? a, List<KeyValuePair<string, DynValue>>? b)
    {
        if (a is null && b is null) return true;
        if (a is null || b is null) return false;
        if (a.Count != b.Count) return false;
        for (int i = 0; i < a.Count; i++)
        {
            if (a[i].Key != b[i].Key || !a[i].Value.Equals(b[i].Value)) return false;
        }
        return true;
    }
}
