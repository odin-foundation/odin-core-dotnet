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
        if (args.Length == 0) return DynValue.Null();
        return VerbHelpers.NumericResult(VerbHelpers.ToNumber(args[0]));
    }

    /// <summary>
    /// Converts a value to an integer. Truncates floats, parses strings.
    /// Null passes through.
    /// </summary>
    private static DynValue CoerceInteger(DynValue[] args, VerbContext ctx)
    {
        if (args.Length == 0) return DynValue.Null();
        return DynValue.Integer((long)Math.Floor(VerbHelpers.ToNumber(args[0])));
    }

    /// <summary>
    /// Converts a value to a boolean. Strings "false", "0", "no", "n", "off", and ""
    /// are considered false; all other non-null strings are true. Numbers are false when zero.
    /// Null becomes false.
    /// </summary>
    private static DynValue CoerceBoolean(DynValue[] args, VerbContext ctx)
    {
        if (args.Length == 0) return DynValue.Null();
        return DynValue.Bool(VerbHelpers.ToBoolean(args[0]));
    }

    /// <summary>
    /// Parses a string to a date (YYYY-MM-DD). Also accepts unix timestamps as integers.
    /// Null passes through.
    /// </summary>
    private static DynValue CoerceDate(DynValue[] args, VerbContext ctx)
    {
        if (args.Length == 0) return DynValue.Null();

        var val = args[0];
        if (val.IsNull) return DynValue.Null();

        if (val.Type == DynValueType.Date) return val;
        if (val.Type == DynValueType.Timestamp)
        {
            var ts = val.AsString() ?? "";
            int ti = ts.IndexOf('T');
            if (ti >= 0) ts = ts.Substring(0, ti);
            return DynValue.Date(ts);
        }

        var s = VerbHelpers.CoerceStr(val);
        if (s.Length == 0) return DynValue.Null();

        // ISO yyyy-MM-dd prefix
        if (IsValidDatePrefix(s))
            return DynValue.Date(s.Substring(0, 10));

        // Compact YYYYMMDD
        var compact = System.Text.RegularExpressions.Regex.Match(s, @"^(\d{4})(\d{2})(\d{2})$");
        if (compact.Success)
        {
            int y = int.Parse(compact.Groups[1].Value, CultureInfo.InvariantCulture);
            int mo = int.Parse(compact.Groups[2].Value, CultureInfo.InvariantCulture);
            int d = int.Parse(compact.Groups[3].Value, CultureInfo.InvariantCulture);
            return ValidYmd(y, mo, d) ? DynValue.Date($"{y:D4}-{mo:D2}-{d:D2}") : DynValue.Null();
        }

        // Slash MM/DD/YYYY (US) or DD/MM/YYYY when first > 12
        var slash = System.Text.RegularExpressions.Regex.Match(s, @"^(\d{1,2})/(\d{1,2})/(\d{4})$");
        if (slash.Success)
        {
            int first = int.Parse(slash.Groups[1].Value, CultureInfo.InvariantCulture);
            int second = int.Parse(slash.Groups[2].Value, CultureInfo.InvariantCulture);
            int y = int.Parse(slash.Groups[3].Value, CultureInfo.InvariantCulture);
            int mo, d;
            if (first > 12) { d = first; mo = second; }
            else { mo = first; d = second; }
            return ValidYmd(y, mo, d) ? DynValue.Date($"{y:D4}-{mo:D2}-{d:D2}") : DynValue.Null();
        }

        // Epoch seconds / millis
        if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var epoch))
        {
            try
            {
                const double threshold = 100_000_000_000d;
                long ms = Math.Abs(epoch) < threshold ? (long)(epoch * 1000) : (long)epoch;
                var dt = DateTimeOffset.FromUnixTimeMilliseconds(ms).UtcDateTime;
                return DynValue.Date(dt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            }
            catch { return DynValue.Null(); }
        }

        return DynValue.Null();
    }

    private static bool ValidYmd(int y, int mo, int d)
    {
        if (mo < 1 || mo > 12 || d < 1) return false;
        if (y < 1 || y > 9999) return false;
        return d <= DateTime.DaysInMonth(y, mo);
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
        if (args.Length == 0) return DynValue.Null();

        var val = args[0];
        if (val.IsNull) return DynValue.Null();
        if (val.Type == DynValueType.Object) return val;
        if (val.Type != DynValueType.Array) return DynValue.Null();

        var arr = val.AsArray()!;
        var entries = new List<KeyValuePair<string, DynValue>>();
        var index = new Dictionary<string, int>();
        foreach (var item in arr)
        {
            if (item.Type == DynValueType.Array)
            {
                var pair = item.AsArray()!;
                if (pair.Count < 2) continue;
                var key = VerbHelpers.CoerceStr(pair[0]);
                if (index.TryGetValue(key, out var at))
                    entries[at] = new KeyValuePair<string, DynValue>(key, pair[1]);
                else { index[key] = entries.Count; entries.Add(new KeyValuePair<string, DynValue>(key, pair[1])); }
            }
            else if (item.Type == DynValueType.Object)
            {
                var keyV = item.Get("key");
                if (keyV == null) continue;
                var key = VerbHelpers.CoerceStr(keyV);
                var valV = item.Get("value") ?? DynValue.Null();
                if (index.TryGetValue(key, out var at))
                    entries[at] = new KeyValuePair<string, DynValue>(key, valV);
                else { index[key] = entries.Count; entries.Add(new KeyValuePair<string, DynValue>(key, valV)); }
            }
        }
        if (entries.Count == 0) return DynValue.Null();
        return DynValue.Object(entries);
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
