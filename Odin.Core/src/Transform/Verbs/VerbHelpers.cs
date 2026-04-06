#nullable enable

using System.Globalization;
using Odin.Core.Types;

using Odin.Core.Transform;

namespace Odin.Core.Transform.Verbs;

/// <summary>
/// Shared helper methods used across verb implementations for type coercion,
/// truthiness checks, and equality comparison.
/// </summary>
internal static class VerbHelpers
{
    /// <summary>
    /// Coerces a DynValue to a double. Handles integers, floats, currency, percent,
    /// raw float/currency strings, plain strings, and booleans. Returns null for
    /// types that cannot be converted.
    /// </summary>
    /// <param name="val">The value to coerce.</param>
    /// <returns>The coerced double value, or null if conversion fails.</returns>
    public static double? CoerceNum(DynValue val)
    {
        switch (val.Type)
        {
            case DynValueType.Integer:
                return (double)val.AsInt64()!.Value;

            case DynValueType.Float:
            case DynValueType.Currency:
            case DynValueType.Percent:
                return val.AsDouble()!.Value;

            case DynValueType.FloatRaw:
            case DynValueType.CurrencyRaw:
                var raw = val.AsString();
                if (raw != null && double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
                    return parsed;
                return null;

            case DynValueType.String:
                var s = val.AsString();
                if (s != null)
                {
                    if (long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
                    {
                        if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
                            return d;
                    }
                    if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var f))
                        return f;
                }
                return null;

            case DynValueType.Bool:
                return val.AsBool()!.Value ? 1.0 : 0.0;

            default:
                return null;
        }
    }

    /// <summary>
    /// Coerces a DynValue to its string representation. Mirrors the Rust coerce_str function.
    /// </summary>
    /// <param name="val">The value to coerce.</param>
    /// <returns>The string representation of the value.</returns>
    public static string CoerceStr(DynValue val)
    {
        switch (val.Type)
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
                return val.AsString() ?? "";

            case DynValueType.Integer:
                return val.AsInt64()!.Value.ToString(CultureInfo.InvariantCulture);

            case DynValueType.Float:
            case DynValueType.Currency:
            case DynValueType.Percent:
                return val.AsDouble()!.Value.ToString(CultureInfo.InvariantCulture);

            case DynValueType.Bool:
                return val.AsBool()!.Value ? "true" : "false";

            case DynValueType.Null:
                return "";

            case DynValueType.Array:
                var arr = val.AsArray();
                return $"[{arr?.Count ?? 0} items]";

            case DynValueType.Object:
                return "[object]";

            default:
                return "";
        }
    }

    /// <summary>
    /// Determines if a DynValue is "truthy". Truthy means non-null, non-false,
    /// non-empty-string, non-zero. Arrays and objects are always truthy.
    /// </summary>
    /// <param name="val">The value to check.</param>
    /// <returns>True if the value is truthy.</returns>
    public static bool IsTruthy(DynValue val)
    {
        switch (val.Type)
        {
            case DynValueType.Null:
                return false;
            case DynValueType.Bool:
                return val.AsBool()!.Value;
            case DynValueType.Integer:
                return val.AsInt64()!.Value != 0;
            case DynValueType.Float:
            case DynValueType.Currency:
            case DynValueType.Percent:
                return val.AsDouble()!.Value != 0.0;
            case DynValueType.String:
            case DynValueType.Reference:
            case DynValueType.Binary:
            case DynValueType.Date:
            case DynValueType.Timestamp:
            case DynValueType.Time:
            case DynValueType.Duration:
                var s = val.AsString();
                return s != null && s.Length > 0;
            case DynValueType.FloatRaw:
            case DynValueType.CurrencyRaw:
                var raw = val.AsString();
                return raw != null && raw.Length > 0 && raw != "0";
            case DynValueType.Array:
            case DynValueType.Object:
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// Deep equality comparison of two DynValues with cross-type numeric and
    /// string-number coercion, matching the Rust dyn_values_equal behavior.
    /// </summary>
    /// <param name="a">First value.</param>
    /// <param name="b">Second value.</param>
    /// <returns>True if the values are considered equal.</returns>
    public static bool DynValuesEqual(DynValue a, DynValue b)
    {
        // Direct structural equality first
        if (a.Equals(b)) return true;

        // Cross-type numeric comparison (Integer vs Float)
        if ((a.Type == DynValueType.Integer && b.Type == DynValueType.Float)
            || (a.Type == DynValueType.Float && b.Type == DynValueType.Integer))
        {
            var da = a.AsDouble();
            var db = b.AsDouble();
            if (da.HasValue && db.HasValue)
                return da.Value == db.Value;
        }

        // String-number coercion
        if (a.Type == DynValueType.String && (b.Type == DynValueType.Integer || b.Type == DynValueType.Float))
        {
            return StringMatchesNumber(a.AsString(), b);
        }
        if (b.Type == DynValueType.String && (a.Type == DynValueType.Integer || a.Type == DynValueType.Float))
        {
            return StringMatchesNumber(b.AsString(), a);
        }

        return false;
    }

    private static bool StringMatchesNumber(string? s, DynValue num)
    {
        if (s == null) return false;

        if (num.Type == DynValueType.Integer)
        {
            if (long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
                return parsed == num.AsInt64()!.Value;
        }

        if (num.Type == DynValueType.Float)
        {
            if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
                return parsed == num.AsDouble()!.Value;
        }

        return false;
    }
}
