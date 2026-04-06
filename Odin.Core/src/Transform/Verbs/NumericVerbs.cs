#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using Odin.Core.Types;

using Odin.Core.Transform;

namespace Odin.Core.Transform.Verbs;

/// <summary>
/// Numeric verb implementations for the transform engine.
/// Provides 25 numeric verbs: formatNumber, formatInteger, formatCurrency, floor,
/// ceil, negate, switch, sign, trunc, random, minOf, maxOf, formatPercent,
/// isFinite, isNaN, parseInt, safeDivide, formatLocaleNumber, add, subtract,
/// multiply, divide, abs, round, mod.
/// </summary>
internal static class NumericVerbs
{
    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private static double? ToDouble(DynValue v)
    {
        switch (v.Type)
        {
            case DynValueType.Integer:
                return (double)(v.AsInt64() ?? 0);
            case DynValueType.Float:
            case DynValueType.Currency:
            case DynValueType.Percent:
                return v.AsDouble();
            case DynValueType.FloatRaw:
            case DynValueType.CurrencyRaw:
            {
                var s = v.AsString();
                if (s != null && double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed))
                    return parsed;
                return null;
            }
            case DynValueType.String:
            {
                var s = v.AsString();
                if (s != null && double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed))
                    return parsed;
                return null;
            }
            default:
                return null;
        }
    }

    /// <summary>
    /// Return an Integer DynValue if the value is whole, Float otherwise.
    /// </summary>
    private static DynValue NumericResult(double v)
    {
        if (v % 1.0 == 0.0 && Math.Abs(v) < (double)long.MaxValue)
            return DynValue.Integer((long)v);
        return DynValue.Float(v);
    }

    [ThreadStatic]
    private static Random? t_random;

    private static Random GetRandom()
    {
        if (t_random == null)
            t_random = new Random();
        return t_random;
    }

    /// <summary>
    /// DJB2 hash with seed starting at 0, matching TypeScript/Rust behavior.
    /// </summary>
    internal static uint StringToSeed(string s)
    {
        uint hash = 0;
        foreach (char c in s)
        {
            hash = unchecked((hash << 5) - hash + (uint)c);
        }
        return hash;
    }

    /// <summary>
    /// Mulberry32 PRNG. Returns a float in [0, 1). Mutates state in place.
    /// </summary>
    internal static double Mulberry32(ref uint state)
    {
        unchecked
        {
            state += 0x6D2B79F5;
            uint t = state;
            t = (t ^ (t >> 15)) * (t | 1);
            t = (t + ((t ^ (t >> 7)) * (t | 61))) ^ t;
            t = t ^ (t >> 14);
            return t / 4294967296.0;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Registration
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Registers all numeric verbs into the given dictionary.
    /// </summary>
    public static void Register(Dictionary<string, Func<DynValue[], VerbContext, DynValue>> reg)
    {
        reg["formatNumber"] = FormatNumber;
        reg["formatInteger"] = FormatInteger;
        reg["formatCurrency"] = FormatCurrency;
        reg["floor"] = Floor;
        reg["ceil"] = Ceil;
        reg["negate"] = Negate;
        reg["switch"] = Switch;
        reg["sign"] = Sign;
        reg["trunc"] = Trunc;
        reg["random"] = RandomVerb;
        reg["minOf"] = MinOf;
        reg["maxOf"] = MaxOf;
        reg["formatPercent"] = FormatPercent;
        reg["isFinite"] = IsFinite;
        reg["isNaN"] = IsNaN;
        reg["parseInt"] = ParseInt;
        reg["safeDivide"] = SafeDivide;
        reg["formatLocaleNumber"] = FormatLocaleNumber;
        reg["add"] = Add;
        reg["subtract"] = Subtract;
        reg["multiply"] = Multiply;
        reg["divide"] = Divide;
        reg["abs"] = Abs;
        reg["round"] = Round;
        reg["mod"] = Mod;
        reg["convertUnit"] = ConvertUnit;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Numeric Verbs (25)
    // ─────────────────────────────────────────────────────────────────────────

    private static DynValue FormatNumber(DynValue[] args, VerbContext ctx)
    {
        if (args.Length < 2) return DynValue.Null();
        var val = ToDouble(args[0]);
        if (!val.HasValue) return DynValue.Null();
        var places = ToDouble(args[1]);
        int decimals = places.HasValue ? (int)places.Value : 2;
        if (decimals < 0) decimals = 0;
        return DynValue.String(val.Value.ToString("F" + decimals, CultureInfo.InvariantCulture));
    }

    private static DynValue FormatInteger(DynValue[] args, VerbContext ctx)
    {
        if (args.Length < 1) return DynValue.Null();
        var val = ToDouble(args[0]);
        if (!val.HasValue) return DynValue.Null();
        long intVal = (long)val.Value;
        return DynValue.String(intVal.ToString(CultureInfo.InvariantCulture));
    }

    private static DynValue FormatCurrency(DynValue[] args, VerbContext ctx)
    {
        if (args.Length < 1) return DynValue.Null();
        var val = ToDouble(args[0]);
        if (!val.HasValue) return DynValue.Null();

        int decimals = 2;
        if (args.Length >= 2)
        {
            var d = ToDouble(args[1]);
            if (d.HasValue)
                decimals = (int)d.Value;
        }

        string formatted = val.Value.ToString("F" + decimals, CultureInfo.InvariantCulture);
        return DynValue.String(formatted);
    }

    private static DynValue Floor(DynValue[] args, VerbContext ctx)
    {
        if (args.Length < 1) return DynValue.Null();
        var val = ToDouble(args[0]);
        if (!val.HasValue) return DynValue.Null();
        return NumericResult(Math.Floor(val.Value));
    }

    private static DynValue Ceil(DynValue[] args, VerbContext ctx)
    {
        if (args.Length < 1) return DynValue.Null();
        var val = ToDouble(args[0]);
        if (!val.HasValue) return DynValue.Null();
        return NumericResult(Math.Ceiling(val.Value));
    }

    private static DynValue Negate(DynValue[] args, VerbContext ctx)
    {
        if (args.Length < 1) return DynValue.Null();
        var val = ToDouble(args[0]);
        if (!val.HasValue) return DynValue.Null();
        return NumericResult(-val.Value);
    }

    private static DynValue Switch(DynValue[] args, VerbContext ctx)
    {
        // args[0] = value to match
        // args[1..N] = pairs of (match, result)
        // If odd number of remaining args, last is default
        if (args.Length < 1) return DynValue.Null();
        DynValue value = args[0];

        int pairStart = 1;
        int remaining = args.Length - pairStart;
        bool hasDefault = remaining % 2 != 0;
        int pairCount = remaining / 2;

        for (int i = 0; i < pairCount; i++)
        {
            int matchIdx = pairStart + i * 2;
            int resultIdx = matchIdx + 1;
            if (ValuesEqual(value, args[matchIdx]))
                return args[resultIdx];
        }

        // Return default if present
        if (hasDefault)
            return args[args.Length - 1];

        return DynValue.Null();
    }

    private static bool ValuesEqual(DynValue a, DynValue b)
    {
        // Compare by coercing to common types
        var da = ToDouble(a);
        var db = ToDouble(b);
        if (da.HasValue && db.HasValue)
            return da.Value == db.Value;

        var sa = a.AsString() ?? a.ToString();
        var sb = b.AsString() ?? b.ToString();
        return string.Equals(sa, sb, StringComparison.Ordinal);
    }

    private static DynValue Sign(DynValue[] args, VerbContext ctx)
    {
        if (args.Length < 1) return DynValue.Null();
        var val = ToDouble(args[0]);
        if (!val.HasValue) return DynValue.Null();
        if (val.Value > 0) return DynValue.Integer(1);
        if (val.Value < 0) return DynValue.Integer(-1);
        return DynValue.Integer(0);
    }

    private static DynValue Trunc(DynValue[] args, VerbContext ctx)
    {
        if (args.Length < 1) return DynValue.Null();
        var val = ToDouble(args[0]);
        if (!val.HasValue) return DynValue.Null();
        return NumericResult(Math.Truncate(val.Value));
    }

    private static DynValue RandomVerb(DynValue[] args, VerbContext ctx)
    {
        // 1 string arg: seeded random float in [0, 1)
        if (args.Length == 1 && args[0].Type == DynValueType.String)
        {
            var seedStr = args[0].AsString();
            if (seedStr != null)
            {
                uint state = StringToSeed(seedStr);
                return DynValue.Float(Mulberry32(ref state));
            }
        }

        // 3 args (min, max, seed_str): seeded integer in [min, max]
        if (args.Length >= 3 && args[2].Type == DynValueType.String)
        {
            var min3 = ToDouble(args[0]);
            var max3 = ToDouble(args[1]);
            var seedStr = args[2].AsString();
            if (min3.HasValue && max3.HasValue && seedStr != null)
            {
                uint state = StringToSeed(seedStr);
                double r = Mulberry32(ref state);
                long lo = (long)min3.Value;
                long hi = (long)max3.Value;
                long result = lo + (long)Math.Floor(r * (hi - lo + 1));
                return DynValue.Integer(result);
            }
        }

        var rng = GetRandom();
        if (args.Length < 2)
        {
            // No args: random between 0 and 1
            return DynValue.Float(rng.NextDouble());
        }
        var minV = ToDouble(args[0]);
        var maxV = ToDouble(args[1]);
        if (!minV.HasValue || !maxV.HasValue) return DynValue.Null();
        double range = maxV.Value - minV.Value;
        double res = minV.Value + rng.NextDouble() * range;
        return DynValue.Float(res);
    }

    private static DynValue MinOf(DynValue[] args, VerbContext ctx)
    {
        if (args.Length < 1) return DynValue.Null();
        double? result = null;
        for (int i = 0; i < args.Length; i++)
        {
            // If argument is an array, iterate into it
            var arr = args[i].AsArray();
            if (arr != null)
            {
                for (int j = 0; j < arr.Count; j++)
                {
                    var val = ToDouble(arr[j]);
                    if (val.HasValue && (!result.HasValue || val.Value < result.Value))
                        result = val.Value;
                }
            }
            else
            {
                var val = ToDouble(args[i]);
                if (val.HasValue && (!result.HasValue || val.Value < result.Value))
                    result = val.Value;
            }
        }
        if (!result.HasValue) return DynValue.Null();
        return NumericResult(result.Value);
    }

    private static DynValue MaxOf(DynValue[] args, VerbContext ctx)
    {
        if (args.Length < 1) return DynValue.Null();
        double? result = null;
        for (int i = 0; i < args.Length; i++)
        {
            var arr = args[i].AsArray();
            if (arr != null)
            {
                for (int j = 0; j < arr.Count; j++)
                {
                    var val = ToDouble(arr[j]);
                    if (val.HasValue && (!result.HasValue || val.Value > result.Value))
                        result = val.Value;
                }
            }
            else
            {
                var val = ToDouble(args[i]);
                if (val.HasValue && (!result.HasValue || val.Value > result.Value))
                    result = val.Value;
            }
        }
        if (!result.HasValue) return DynValue.Null();
        return NumericResult(result.Value);
    }

    private static DynValue FormatPercent(DynValue[] args, VerbContext ctx)
    {
        if (args.Length < 1) return DynValue.Null();
        var val = ToDouble(args[0]);
        if (!val.HasValue) return DynValue.Null();
        int decimals = 0;
        if (args.Length >= 2)
        {
            var d = ToDouble(args[1]);
            if (d.HasValue) decimals = (int)d.Value;
        }
        double pct = val.Value * 100.0;
        // Use AwayFromZero rounding to match JavaScript's toFixed behavior
        double rounded = Math.Round(pct, Math.Max(0, decimals), MidpointRounding.AwayFromZero);
        return DynValue.String(rounded.ToString("F" + decimals, CultureInfo.InvariantCulture) + "%");
    }

    private static DynValue IsFinite(DynValue[] args, VerbContext ctx)
    {
        if (args.Length < 1) return DynValue.Bool(false);
        var val = ToDouble(args[0]);
        if (!val.HasValue) return DynValue.Bool(false);
        return DynValue.Bool(!double.IsInfinity(val.Value) && !double.IsNaN(val.Value));
    }

    private static DynValue IsNaN(DynValue[] args, VerbContext ctx)
    {
        if (args.Length < 1) return DynValue.Bool(false);
        var val = ToDouble(args[0]);
        if (!val.HasValue) return DynValue.Bool(true);
        return DynValue.Bool(double.IsNaN(val.Value));
    }

    private static DynValue ParseInt(DynValue[] args, VerbContext ctx)
    {
        if (args.Length < 1) return DynValue.Null();
        if (args[0].IsNull) return DynValue.Null();

        // If already integer, return it
        var intVal = args[0].AsInt64();
        if (intVal.HasValue) return DynValue.Integer(intVal.Value);

        // If float, truncate
        var dblVal = ToDouble(args[0]);
        if (dblVal.HasValue) return DynValue.Integer((long)dblVal.Value);

        // Try parsing string
        var s = args[0].AsString();
        if (s != null)
        {
            s = s.Trim();
            if (long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out long parsed))
                return DynValue.Integer(parsed);
            // Try parsing as float then truncating
            if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double dParsed))
                return DynValue.Integer((long)dParsed);
        }
        return DynValue.Null();
    }

    private static DynValue SafeDivide(DynValue[] args, VerbContext ctx)
    {
        if (args.Length < 2) return DynValue.Null();
        var numerator = ToDouble(args[0]);
        var denominator = ToDouble(args[1]);
        if (!numerator.HasValue) return DynValue.Null();
        if (!denominator.HasValue || denominator.Value == 0.0)
        {
            // Return fallback (args[2]) or null
            if (args.Length >= 3)
                return args[2];
            return DynValue.Null();
        }
        return NumericResult(numerator.Value / denominator.Value);
    }

    private static DynValue FormatLocaleNumber(DynValue[] args, VerbContext ctx)
    {
        if (args.Length < 1) return DynValue.Null();
        var val = ToDouble(args[0]);
        if (!val.HasValue) return DynValue.Null();

        // args[1] = locale string (e.g., "en-US", "de-DE"), default "en-US"
        // args[2] = optional decimal places
        string locale = "en-US";
        int decimals = -1; // -1 means use default formatting
        if (args.Length >= 2)
        {
            var locStr = args[1].AsString();
            if (locStr != null)
                locale = locStr;
        }
        if (args.Length >= 3)
        {
            var d = ToDouble(args[2]);
            if (d.HasValue) decimals = (int)d.Value;
        }

        try
        {
            var culture = new CultureInfo(locale);
            if (decimals >= 0)
                return DynValue.String(val.Value.ToString("N" + decimals, culture));
            return DynValue.String(val.Value.ToString("N", culture));
        }
        catch (Exception)
        {
            // Fallback to invariant culture
            if (decimals >= 0)
                return DynValue.String(val.Value.ToString("N" + decimals, CultureInfo.InvariantCulture));
            return DynValue.String(val.Value.ToString("N", CultureInfo.InvariantCulture));
        }
    }

    private static DynValue Add(DynValue[] args, VerbContext ctx)
    {
        if (args.Length < 2) return DynValue.Null();
        var a = ToDouble(args[0]);
        var b = ToDouble(args[1]);
        if (!a.HasValue || !b.HasValue) return DynValue.Null();
        return NumericResult(a.Value + b.Value);
    }

    private static DynValue Subtract(DynValue[] args, VerbContext ctx)
    {
        if (args.Length < 2) return DynValue.Null();
        var a = ToDouble(args[0]);
        var b = ToDouble(args[1]);
        if (!a.HasValue || !b.HasValue) return DynValue.Null();
        return NumericResult(a.Value - b.Value);
    }

    private static DynValue Multiply(DynValue[] args, VerbContext ctx)
    {
        if (args.Length < 2) return DynValue.Null();
        var a = ToDouble(args[0]);
        var b = ToDouble(args[1]);
        if (!a.HasValue || !b.HasValue) return DynValue.Null();
        return NumericResult(a.Value * b.Value);
    }

    private static DynValue Divide(DynValue[] args, VerbContext ctx)
    {
        if (args.Length < 2) return DynValue.Null();
        var a = ToDouble(args[0]);
        var b = ToDouble(args[1]);
        if (!a.HasValue || !b.HasValue) return DynValue.Null();
        if (b.Value == 0.0) return DynValue.Null();
        // Division always returns Float (number), not Integer, since division implies decimal result
        return DynValue.Float(a.Value / b.Value);
    }

    private static DynValue Abs(DynValue[] args, VerbContext ctx)
    {
        if (args.Length < 1) return DynValue.Null();
        var val = ToDouble(args[0]);
        if (!val.HasValue) return DynValue.Null();
        return NumericResult(Math.Abs(val.Value));
    }

    private static DynValue Round(DynValue[] args, VerbContext ctx)
    {
        if (args.Length < 1) return DynValue.Null();
        var val = ToDouble(args[0]);
        if (!val.HasValue) return DynValue.Null();
        int decimals = 0;
        if (args.Length >= 2)
        {
            var d = ToDouble(args[1]);
            if (d.HasValue) decimals = (int)d.Value;
        }
        if (decimals < 0) decimals = 0;
        double rounded = Math.Round(val.Value, decimals, MidpointRounding.AwayFromZero);
        return NumericResult(rounded);
    }

    private static DynValue Mod(DynValue[] args, VerbContext ctx)
    {
        if (args.Length < 2) return DynValue.Null();
        var a = ToDouble(args[0]);
        var b = ToDouble(args[1]);
        if (!a.HasValue || !b.HasValue) return DynValue.Null();
        if (b.Value == 0.0) return DynValue.Null();
        return NumericResult(a.Value % b.Value);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Unit Conversion
    // ─────────────────────────────────────────────────────────────────────────

    private static readonly HashSet<string> TempUnits = new HashSet<string> { "C", "F", "K" };

    private static readonly Dictionary<string, Dictionary<string, double>> UnitFamilies
        = new Dictionary<string, Dictionary<string, double>>
    {
        ["mass"] = new Dictionary<string, double>
            { ["kg"] = 1, ["g"] = 0.001, ["mg"] = 0.000001, ["lb"] = 0.453592, ["oz"] = 0.0283495, ["ton"] = 907.185, ["tonne"] = 1000 },
        ["length"] = new Dictionary<string, double>
            { ["m"] = 1, ["km"] = 1000, ["cm"] = 0.01, ["mm"] = 0.001, ["mi"] = 1609.344, ["ft"] = 0.3048, ["in"] = 0.0254, ["yd"] = 0.9144 },
        ["volume"] = new Dictionary<string, double>
            { ["L"] = 1, ["mL"] = 0.001, ["gal"] = 3.78541, ["qt"] = 0.946353, ["pt"] = 0.473176, ["cup"] = 0.236588, ["floz"] = 0.0295735 },
        ["speed"] = new Dictionary<string, double>
            { ["mps"] = 1, ["kph"] = 0.277778, ["mph"] = 0.44704 },
        ["area"] = new Dictionary<string, double>
            { ["sqm"] = 1, ["sqft"] = 0.092903, ["sqkm"] = 1000000, ["sqmi"] = 2589988.11, ["acre"] = 4046.8564, ["hectare"] = 10000 },
        ["data"] = new Dictionary<string, double>
            { ["B"] = 1, ["KB"] = 1024, ["MB"] = 1048576, ["GB"] = 1073741824, ["TB"] = 1099511627776 },
        ["time"] = new Dictionary<string, double>
            { ["ms"] = 0.001, ["s"] = 1, ["min"] = 60, ["hr"] = 3600, ["day"] = 86400 },
    };

    private static (string family, double factor)? FindUnitFamily(string unit)
    {
        foreach (var family in UnitFamilies)
        {
            if (family.Value.TryGetValue(unit, out var factor))
                return (family.Key, factor);
        }
        return null;
    }

    private static DynValue ConvertUnit(DynValue[] args, VerbContext ctx)
    {
        if (args.Length < 3) return DynValue.Null();

        var value = ToDouble(args[0]);
        if (!value.HasValue || double.IsNaN(value.Value) || double.IsInfinity(value.Value))
            return DynValue.Null();

        var fromUnit = args[1].AsString();
        var toUnit = args[2].AsString();
        if (fromUnit == null || toUnit == null) return DynValue.Null();

        // Handle temperature separately (formula-based)
        if (TempUnits.Contains(fromUnit) && TempUnits.Contains(toUnit))
        {
            if (fromUnit == toUnit) return NumericResult(value.Value);

            // Convert to Celsius first
            double celsius;
            switch (fromUnit)
            {
                case "C": celsius = value.Value; break;
                case "F": celsius = (value.Value - 32) * 5.0 / 9.0; break;
                case "K": celsius = value.Value - 273.15; break;
                default: return DynValue.Null();
            }

            // Convert from Celsius to target
            double result;
            switch (toUnit)
            {
                case "C": result = celsius; break;
                case "F": result = celsius * 9.0 / 5.0 + 32; break;
                case "K": result = celsius + 273.15; break;
                default: return DynValue.Null();
            }

            result = Math.Round(result * 1000000.0) / 1000000.0;
            return NumericResult(result);
        }

        // One is temp, other is not → incompatible
        if (TempUnits.Contains(fromUnit) || TempUnits.Contains(toUnit))
            return DynValue.Null();

        // Look up families
        var fromInfo = FindUnitFamily(fromUnit);
        var toInfo = FindUnitFamily(toUnit);

        if (!fromInfo.HasValue || !toInfo.HasValue) return DynValue.Null();
        if (fromInfo.Value.family != toInfo.Value.family) return DynValue.Null();

        if (fromUnit == toUnit) return NumericResult(value.Value);

        double converted = value.Value * fromInfo.Value.factor / toInfo.Value.factor;
        double rounded = Math.Round(converted * 1000000.0) / 1000000.0;
        return NumericResult(rounded);
    }
}
