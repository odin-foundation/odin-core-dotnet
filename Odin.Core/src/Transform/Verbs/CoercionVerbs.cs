#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using Odin.Core.Types;

using Odin.Core.Transform;

namespace Odin.Core.Transform.Verbs;

/// <summary>
/// Type coercion verbs: convert values between types (string, number, boolean, date, etc.).
/// </summary>
internal static class CoercionVerbs
{
    /// <summary>
    /// Registers all coercion verbs into the provided dictionary.
    /// </summary>
    /// <param name="reg">The verb registration dictionary.</param>
    public static void Register(Dictionary<string, Func<DynValue[], VerbContext, DynValue>> reg)
    {
        reg["coerceString"] = CoerceString;
        reg["coerceNumber"] = CoerceNumber;
        reg["coerceInteger"] = CoerceInteger;
        reg["coerceBoolean"] = CoerceBoolean;
        reg["coerceDate"] = CoerceDate;
        reg["coerceTimestamp"] = CoerceTimestamp;
        reg["tryCoerce"] = TryCoerce;
        reg["toArray"] = ToArray;
        reg["toObject"] = ToObject;
    }

    /// <summary>
    /// Converts any value to its string representation. Null passes through.
    /// </summary>
    private static DynValue CoerceString(DynValue[] args, VerbContext ctx)
    {
        if (args.Length == 0)
            throw new InvalidOperationException("coerceString: requires 1 argument");

        var val = args[0];
        if (val.IsNull) return DynValue.Null();

        return DynValue.String(VerbHelpers.CoerceStr(val));
    }

    /// <summary>
    /// Converts a value to a floating-point number. Parses strings, passes through numbers,
    /// converts booleans to 1.0/0.0. Null passes through.
    /// </summary>
    private static DynValue CoerceNumber(DynValue[] args, VerbContext ctx)
    {
        if (args.Length == 0)
            throw new InvalidOperationException("coerceNumber: requires 1 argument");

        var val = args[0];
        if (val.IsNull) return DynValue.Null();

        switch (val.Type)
        {
            case DynValueType.Integer:
                return DynValue.Float((double)val.AsInt64()!.Value);
            case DynValueType.Float:
                return val;
            case DynValueType.String:
                var s = val.AsString()!;
                if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var num))
                    return DynValue.Float(num);
                throw new InvalidOperationException($"coerceNumber: cannot parse '{s}' as number");
            case DynValueType.Bool:
                return DynValue.Float(val.AsBool()!.Value ? 1.0 : 0.0);
            default:
                throw new InvalidOperationException("coerceNumber: unsupported type");
        }
    }

    /// <summary>
    /// Converts a value to an integer. Truncates floats, parses strings.
    /// Null passes through.
    /// </summary>
    private static DynValue CoerceInteger(DynValue[] args, VerbContext ctx)
    {
        if (args.Length == 0)
            throw new InvalidOperationException("coerceInteger: requires 1 argument");

        var val = args[0];
        if (val.IsNull) return DynValue.Null();

        switch (val.Type)
        {
            case DynValueType.Integer:
                return val;
            case DynValueType.Float:
                return DynValue.Integer((long)val.AsDouble()!.Value);
            case DynValueType.String:
                var s = val.AsString()!;
                if (long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intVal))
                    return DynValue.Integer(intVal);
                if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var dblVal))
                    return DynValue.Integer((long)dblVal);
                throw new InvalidOperationException($"coerceInteger: cannot parse '{s}' as integer");
            case DynValueType.Bool:
                return DynValue.Integer(val.AsBool()!.Value ? 1L : 0L);
            default:
                throw new InvalidOperationException("coerceInteger: unsupported type");
        }
    }

    /// <summary>
    /// Converts a value to a boolean. Strings "false", "0", "no", "n", "off", and ""
    /// are considered false; all other non-null strings are true. Numbers are false when zero.
    /// Null becomes false.
    /// </summary>
    private static DynValue CoerceBoolean(DynValue[] args, VerbContext ctx)
    {
        if (args.Length == 0)
            throw new InvalidOperationException("coerceBoolean: requires 1 argument");

        var val = args[0];
        if (val.IsNull) return DynValue.Bool(false);

        switch (val.Type)
        {
            case DynValueType.Bool:
                return val;
            case DynValueType.String:
                var s = val.AsString()!.Trim().ToLowerInvariant();
                bool isFalsy = s == "" || s == "false" || s == "0" || s == "no" || s == "n" || s == "off";
                return DynValue.Bool(!isFalsy);
            case DynValueType.Integer:
                return DynValue.Bool(val.AsInt64()!.Value != 0);
            case DynValueType.Float:
                return DynValue.Bool(val.AsDouble()!.Value != 0.0);
            default:
                throw new InvalidOperationException("coerceBoolean: unsupported type");
        }
    }

    /// <summary>
    /// Parses a string to a date (YYYY-MM-DD). Also accepts unix timestamps as integers.
    /// Null passes through.
    /// </summary>
    private static DynValue CoerceDate(DynValue[] args, VerbContext ctx)
    {
        if (args.Length == 0)
            throw new InvalidOperationException("coerceDate: requires 1 argument");

        var val = args[0];
        if (val.IsNull) return DynValue.Null();

        switch (val.Type)
        {
            case DynValueType.String:
            case DynValueType.Date:
            case DynValueType.Timestamp:
                var s = val.Type == DynValueType.String ? val.AsString()! : val.AsString()!;
                if (s.Length >= 10 && IsValidDatePrefix(s))
                {
                    var datePart = s.Substring(0, 10);
                    if (int.TryParse(datePart.Substring(5, 2), out var month)
                        && int.TryParse(datePart.Substring(8, 2), out var day)
                        && month >= 1 && month <= 12 && day >= 1 && day <= 31)
                    {
                        return DynValue.Date(datePart);
                    }
                }
                throw new InvalidOperationException($"coerceDate: '{s}' is not a valid date");

            case DynValueType.Integer:
                var secs = val.AsInt64()!.Value;
                var dt = DateTimeOffset.FromUnixTimeSeconds(secs).UtcDateTime;
                return DynValue.Date(dt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));

            default:
                throw new InvalidOperationException("coerceDate: expected string argument");
        }
    }

    /// <summary>
    /// Parses a string to a timestamp (ISO 8601). Accepts YYYY-MM-DDThh:mm:ss forms.
    /// A bare date (YYYY-MM-DD) gets T00:00:00 appended. Null passes through.
    /// </summary>
    private static DynValue CoerceTimestamp(DynValue[] args, VerbContext ctx)
    {
        if (args.Length == 0)
            throw new InvalidOperationException("coerceTimestamp: requires 1 argument");

        var val = args[0];
        if (val.IsNull) return DynValue.Null();

        var s = val.AsString();
        if (s == null)
            throw new InvalidOperationException("coerceTimestamp: expected string argument");

        // Full timestamp: YYYY-MM-DDThh:mm:ss...
        if (s.Length >= 19 && IsValidDatePrefix(s))
        {
            char sep = s[10];
            if ((sep == 'T' || sep == ' ')
                && char.IsDigit(s[11]) && char.IsDigit(s[12])
                && s[13] == ':'
                && char.IsDigit(s[14]) && char.IsDigit(s[15])
                && s[16] == ':'
                && char.IsDigit(s[17]) && char.IsDigit(s[18]))
            {
                return DynValue.Timestamp(s);
            }
        }

        // Bare date: append T00:00:00
        if (s.Length == 10 && IsValidDatePrefix(s))
            return DynValue.Timestamp(s + "T00:00:00");

        throw new InvalidOperationException($"coerceTimestamp: '{s}' is not a valid timestamp");
    }

    /// <summary>
    /// Attempts to coerce a string value to the most appropriate type.
    /// Tries integer, float, boolean, date in order. If no coercion matches,
    /// returns the original value. Null passes through.
    /// </summary>
    private static DynValue TryCoerce(DynValue[] args, VerbContext ctx)
    {
        if (args.Length == 0)
            return DynValue.Null();

        var val = args[0];
        if (val.Type != DynValueType.String)
            return val;

        var s = val.AsString();
        if (s == null)
            return DynValue.Null();

        // Try integer
        if (long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intVal))
            return DynValue.Integer(intVal);

        // Try float
        if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var dblVal))
            return DynValue.Float(dblVal);

        // Try boolean
        if (s == "true") return DynValue.Bool(true);
        if (s == "false") return DynValue.Bool(false);

        // Try date (YYYY-MM-DD)
        if (s.Length == 10 && IsValidDatePrefix(s))
            return DynValue.Date(s);

        // Keep as string
        return val;
    }

    /// <summary>
    /// Wraps a value in an array. If the value is already an array, returns it unchanged.
    /// If no arguments are provided, returns an empty array.
    /// </summary>
    private static DynValue ToArray(DynValue[] args, VerbContext ctx)
    {
        if (args.Length == 0)
            return DynValue.Array(new List<DynValue>());

        var val = args[0];
        if (val.Type == DynValueType.Array)
            return val;

        return DynValue.Array(new List<DynValue> { val });
    }

    /// <summary>
    /// Converts a value to an object. If the value is already an object, returns it unchanged.
    /// If the value is an array of [key, value] pairs, converts them to an object.
    /// Null passes through.
    /// </summary>
    private static DynValue ToObject(DynValue[] args, VerbContext ctx)
    {
        if (args.Length == 0)
            throw new InvalidOperationException("toObject: requires 1 argument");

        var val = args[0];
        if (val.IsNull) return DynValue.Null();

        if (val.Type == DynValueType.Object)
            return val;

        if (val.Type == DynValueType.Array)
        {
            var arr = val.AsArray()!;
            var entries = new List<KeyValuePair<string, DynValue>>();
            for (int i = 0; i < arr.Count; i++)
            {
                var item = arr[i];
                var pair = item.AsArray();
                if (pair == null || pair.Count < 2)
                    throw new InvalidOperationException("toObject: array elements must be [key, value] pairs");

                string key;
                if (pair[0].Type == DynValueType.String)
                    key = pair[0].AsString()!;
                else if (pair[0].Type == DynValueType.Integer)
                    key = pair[0].AsInt64()!.Value.ToString(CultureInfo.InvariantCulture);
                else
                    key = VerbHelpers.CoerceStr(pair[0]);

                entries.Add(new KeyValuePair<string, DynValue>(key, pair[1]));
            }
            return DynValue.Object(entries);
        }

        throw new InvalidOperationException("toObject: expected array of [key, value] pairs");
    }

    /// <summary>
    /// Validates that a string begins with a valid YYYY-MM-DD date pattern.
    /// </summary>
    internal static bool IsValidDatePrefix(string s)
    {
        if (s.Length < 10) return false;
        return char.IsDigit(s[0]) && char.IsDigit(s[1]) && char.IsDigit(s[2]) && char.IsDigit(s[3])
            && s[4] == '-'
            && char.IsDigit(s[5]) && char.IsDigit(s[6])
            && s[7] == '-'
            && char.IsDigit(s[8]) && char.IsDigit(s[9]);
    }
}
