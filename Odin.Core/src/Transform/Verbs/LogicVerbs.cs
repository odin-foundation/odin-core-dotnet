#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using Odin.Core.Types;

using Odin.Core.Transform;

namespace Odin.Core.Transform.Verbs;

/// <summary>
/// Logic and comparison verbs: boolean operators, comparisons, type checks, and conditionals.
/// </summary>
internal static class LogicVerbs
{
    /// <summary>
    /// Registers all logic verbs into the provided dictionary.
    /// </summary>
    /// <param name="reg">The verb registration dictionary.</param>
    public static void Register(Dictionary<string, Func<DynValue[], VerbContext, DynValue>> reg)
    {
        reg["and"] = And;
        reg["or"] = Or;
        reg["not"] = Not;
        reg["xor"] = Xor;
        reg["eq"] = Eq;
        reg["ne"] = Ne;
        reg["lt"] = Lt;
        reg["lte"] = Lte;
        reg["gt"] = Gt;
        reg["gte"] = Gte;
        reg["between"] = Between;
        reg["isNull"] = IsNull;
        reg["isString"] = IsString;
        reg["isNumber"] = IsNumber;
        reg["isBoolean"] = IsBoolean;
        reg["isArray"] = IsArray;
        reg["isObject"] = IsObject;
        reg["isDate"] = IsDate;
        reg["typeOf"] = TypeOf;
        reg["cond"] = Cond;
        reg["assert"] = Assert;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Boolean Logic
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Logical AND of two boolean arguments.
    /// </summary>
    private static DynValue And(DynValue[] args, VerbContext ctx)
    {
        if (args.Length < 2)
            throw new InvalidOperationException("and: requires 2 arguments");

        var a = args[0].AsBool();
        var b = args[1].AsBool();
        if (a == null || b == null)
            throw new InvalidOperationException("and: expected boolean arguments");

        return DynValue.Bool(a.Value && b.Value);
    }

    /// <summary>
    /// Logical OR of two boolean arguments.
    /// </summary>
    private static DynValue Or(DynValue[] args, VerbContext ctx)
    {
        if (args.Length < 2)
            throw new InvalidOperationException("or: requires 2 arguments");

        var a = args[0].AsBool();
        var b = args[1].AsBool();
        if (a == null || b == null)
            throw new InvalidOperationException("or: expected boolean arguments");

        return DynValue.Bool(a.Value || b.Value);
    }

    /// <summary>
    /// Logical NOT. Negates booleans; null becomes true; zero becomes true;
    /// empty strings and "false" become true.
    /// </summary>
    private static DynValue Not(DynValue[] args, VerbContext ctx)
    {
        if (args.Length == 0)
            return DynValue.Bool(false);

        var val = args[0];
        switch (val.Type)
        {
            case DynValueType.Bool:
                return DynValue.Bool(!val.AsBool()!.Value);
            case DynValueType.Null:
                return DynValue.Bool(true);
            case DynValueType.Integer:
                return DynValue.Bool(val.AsInt64()!.Value == 0);
            case DynValueType.Float:
                return DynValue.Bool(val.AsDouble()!.Value == 0.0);
            case DynValueType.String:
                var s = val.AsString()!;
                return DynValue.Bool(s.Length == 0 || s == "false");
            case DynValueType.Array:
                return DynValue.Bool(val.AsArray()!.Count == 0);
            default:
                return DynValue.Bool(false);
        }
    }

    /// <summary>
    /// Logical XOR of two boolean arguments.
    /// </summary>
    private static DynValue Xor(DynValue[] args, VerbContext ctx)
    {
        if (args.Length < 2)
            throw new InvalidOperationException("xor: requires 2 arguments");

        var a = args[0].AsBool();
        var b = args[1].AsBool();
        if (a == null || b == null)
            throw new InvalidOperationException("xor: expected boolean arguments");

        return DynValue.Bool(a.Value ^ b.Value);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Equality
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Deep equality comparison of two values. Supports cross-type numeric
    /// and string-number comparisons.
    /// </summary>
    private static DynValue Eq(DynValue[] args, VerbContext ctx)
    {
        if (args.Length < 2)
            throw new InvalidOperationException("eq: requires 2 arguments");

        return DynValue.Bool(VerbHelpers.DynValuesEqual(args[0], args[1]));
    }

    /// <summary>
    /// Deep inequality comparison of two values.
    /// </summary>
    private static DynValue Ne(DynValue[] args, VerbContext ctx)
    {
        if (args.Length < 2)
            throw new InvalidOperationException("ne: requires 2 arguments");

        return DynValue.Bool(!VerbHelpers.DynValuesEqual(args[0], args[1]));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Ordering Comparisons
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Less-than comparison. Supports numeric and string comparison.
    /// </summary>
    private static DynValue Lt(DynValue[] args, VerbContext ctx)
    {
        if (args.Length < 2)
            throw new InvalidOperationException("lt: requires 2 arguments");

        var (a, b) = (ToF64ForCmp(args[0]), ToF64ForCmp(args[1]));
        if (a.HasValue && b.HasValue)
            return DynValue.Bool(a.Value < b.Value);

        var (sa, sb) = (args[0].AsString(), args[1].AsString());
        if (sa != null && sb != null)
            return DynValue.Bool(string.Compare(sa, sb, StringComparison.Ordinal) < 0);

        throw new InvalidOperationException("lt: expected numeric or string arguments");
    }

    /// <summary>
    /// Less-than-or-equal comparison. Supports numeric and string comparison.
    /// </summary>
    private static DynValue Lte(DynValue[] args, VerbContext ctx)
    {
        if (args.Length < 2)
            throw new InvalidOperationException("lte: requires 2 arguments");

        var (a, b) = (ToF64ForCmp(args[0]), ToF64ForCmp(args[1]));
        if (a.HasValue && b.HasValue)
            return DynValue.Bool(a.Value <= b.Value);

        var (sa, sb) = (args[0].AsString(), args[1].AsString());
        if (sa != null && sb != null)
            return DynValue.Bool(string.Compare(sa, sb, StringComparison.Ordinal) <= 0);

        throw new InvalidOperationException("lte: expected numeric or string arguments");
    }

    /// <summary>
    /// Greater-than comparison. Supports numeric and string comparison.
    /// </summary>
    private static DynValue Gt(DynValue[] args, VerbContext ctx)
    {
        if (args.Length < 2)
            throw new InvalidOperationException("gt: requires 2 arguments");

        var (a, b) = (ToF64ForCmp(args[0]), ToF64ForCmp(args[1]));
        if (a.HasValue && b.HasValue)
            return DynValue.Bool(a.Value > b.Value);

        var (sa, sb) = (args[0].AsString(), args[1].AsString());
        if (sa != null && sb != null)
            return DynValue.Bool(string.Compare(sa, sb, StringComparison.Ordinal) > 0);

        throw new InvalidOperationException("gt: expected numeric or string arguments");
    }

    /// <summary>
    /// Greater-than-or-equal comparison. Supports numeric and string comparison.
    /// </summary>
    private static DynValue Gte(DynValue[] args, VerbContext ctx)
    {
        if (args.Length < 2)
            throw new InvalidOperationException("gte: requires 2 arguments");

        var (a, b) = (ToF64ForCmp(args[0]), ToF64ForCmp(args[1]));
        if (a.HasValue && b.HasValue)
            return DynValue.Bool(a.Value >= b.Value);

        var (sa, sb) = (args[0].AsString(), args[1].AsString());
        if (sa != null && sb != null)
            return DynValue.Bool(string.Compare(sa, sb, StringComparison.Ordinal) >= 0);

        throw new InvalidOperationException("gte: expected numeric or string arguments");
    }

    /// <summary>
    /// Checks if value is between min and max (inclusive). Supports numeric and string comparison.
    /// </summary>
    private static DynValue Between(DynValue[] args, VerbContext ctx)
    {
        if (args.Length < 3)
            throw new InvalidOperationException("between: requires 3 arguments (value, min, max)");

        var (val, min, max) = (ToF64ForCmp(args[0]), ToF64ForCmp(args[1]), ToF64ForCmp(args[2]));
        if (val.HasValue && min.HasValue && max.HasValue)
            return DynValue.Bool(val.Value >= min.Value && val.Value <= max.Value);

        var (sVal, sMin, sMax) = (args[0].AsString(), args[1].AsString(), args[2].AsString());
        if (sVal != null && sMin != null && sMax != null)
        {
            return DynValue.Bool(
                string.Compare(sVal, sMin, StringComparison.Ordinal) >= 0
                && string.Compare(sVal, sMax, StringComparison.Ordinal) <= 0);
        }

        throw new InvalidOperationException("between: expected numeric or string arguments");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Type Checks
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns true if the argument is null.
    /// </summary>
    private static DynValue IsNull(DynValue[] args, VerbContext ctx)
    {
        if (args.Length == 0) return DynValue.Bool(true);
        return DynValue.Bool(args[0].IsNull);
    }

    /// <summary>
    /// Returns true if the argument is a string.
    /// </summary>
    private static DynValue IsString(DynValue[] args, VerbContext ctx)
    {
        if (args.Length == 0) return DynValue.Bool(false);
        return DynValue.Bool(args[0].Type == DynValueType.String);
    }

    /// <summary>
    /// Returns true if the argument is a number (integer or float).
    /// </summary>
    private static DynValue IsNumber(DynValue[] args, VerbContext ctx)
    {
        if (args.Length == 0) return DynValue.Bool(false);
        return DynValue.Bool(args[0].Type == DynValueType.Integer || args[0].Type == DynValueType.Float);
    }

    /// <summary>
    /// Returns true if the argument is a boolean.
    /// </summary>
    private static DynValue IsBoolean(DynValue[] args, VerbContext ctx)
    {
        if (args.Length == 0) return DynValue.Bool(false);
        return DynValue.Bool(args[0].Type == DynValueType.Bool);
    }

    /// <summary>
    /// Returns true if the argument is an array, or a string that looks like a JSON array.
    /// </summary>
    private static DynValue IsArray(DynValue[] args, VerbContext ctx)
    {
        if (args.Length == 0) return DynValue.Bool(false);

        if (args[0].Type == DynValueType.Array)
            return DynValue.Bool(true);

        if (args[0].Type == DynValueType.String)
        {
            var s = args[0].AsString()!.Trim();
            return DynValue.Bool(s.Length >= 2 && s[0] == '[' && s[s.Length - 1] == ']');
        }

        return DynValue.Bool(false);
    }

    /// <summary>
    /// Returns true if the argument is an object, or a string that looks like a JSON object.
    /// </summary>
    private static DynValue IsObject(DynValue[] args, VerbContext ctx)
    {
        if (args.Length == 0) return DynValue.Bool(false);

        if (args[0].Type == DynValueType.Object)
            return DynValue.Bool(true);

        if (args[0].Type == DynValueType.String)
        {
            var s = args[0].AsString()!.Trim();
            return DynValue.Bool(s.Length >= 2 && s[0] == '{' && s[s.Length - 1] == '}');
        }

        return DynValue.Bool(false);
    }

    /// <summary>
    /// Returns true if the argument is a date or a string matching the YYYY-MM-DD pattern.
    /// </summary>
    private static DynValue IsDate(DynValue[] args, VerbContext ctx)
    {
        if (args.Length == 0) return DynValue.Bool(false);

        if (args[0].Type == DynValueType.Date)
            return DynValue.Bool(true);

        if (args[0].Type == DynValueType.String)
        {
            var s = args[0].AsString()!;
            return DynValue.Bool(s.Length >= 10 && CoercionVerbs.IsValidDatePrefix(s));
        }

        return DynValue.Bool(false);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Type Inspection
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the type name of the argument as a string.
    /// </summary>
    private static DynValue TypeOf(DynValue[] args, VerbContext ctx)
    {
        if (args.Length == 0) return DynValue.String("null");

        string typeName = args[0].Type switch
        {
            DynValueType.Null => "null",
            DynValueType.Bool => "boolean",
            DynValueType.String => "string",
            DynValueType.Integer => "integer",
            DynValueType.Float or DynValueType.FloatRaw => "number",
            DynValueType.Currency or DynValueType.CurrencyRaw => "currency",
            DynValueType.Percent => "percent",
            DynValueType.Reference => "reference",
            DynValueType.Binary => "binary",
            DynValueType.Date => "date",
            DynValueType.Timestamp => "timestamp",
            DynValueType.Time => "time",
            DynValueType.Duration => "duration",
            DynValueType.Array => "array",
            DynValueType.Object => "object",
            _ => "null",
        };
        return DynValue.String(typeName);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Conditional
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Multi-branch conditional. Processes condition/value pairs; if the argument count
    /// is odd, the last argument is the default/else value.
    /// </summary>
    private static DynValue Cond(DynValue[] args, VerbContext ctx)
    {
        int i = 0;
        while (i + 1 < args.Length)
        {
            if (VerbHelpers.IsTruthy(args[i]))
                return args[i + 1];
            i += 2;
        }
        // If there's a remaining arg, it's the default
        if (i < args.Length)
            return args[i];

        return DynValue.Null();
    }

    /// <summary>
    /// Asserts that the first argument is truthy. If falsy, throws an error with the
    /// optional message from the second argument.
    /// </summary>
    private static DynValue Assert(DynValue[] args, VerbContext ctx)
    {
        if (args.Length == 0)
            throw new InvalidOperationException("assert: requires at least 1 argument");

        if (VerbHelpers.IsTruthy(args[0]))
            return args[0];

        var message = args.Length >= 2 ? args[1].AsString() ?? "assertion failed" : "assertion failed";
        throw new InvalidOperationException($"assert: {message}");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Extracts a double from a DynValue for numeric comparison purposes.
    /// Returns null for non-numeric types.
    /// </summary>
    private static double? ToF64ForCmp(DynValue val)
    {
        switch (val.Type)
        {
            case DynValueType.Integer:
                return (double)val.AsInt64()!.Value;
            case DynValueType.Float:
                return val.AsDouble()!.Value;
            default:
                return null;
        }
    }
}
