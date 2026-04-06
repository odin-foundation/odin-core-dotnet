#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using Odin.Core.Types;

using Odin.Core.Transform;

namespace Odin.Core.Transform.Verbs;

/// <summary>
/// Aggregation verbs: accumulate, set, sum, count, min, max, avg, first, last.
/// These verbs operate on arrays and accumulators for cross-record state.
/// </summary>
internal static class AggregationVerbs
{
    /// <summary>
    /// Registers all aggregation verbs into the provided dictionary.
    /// </summary>
    /// <param name="reg">The verb registration dictionary.</param>
    public static void Register(Dictionary<string, Func<DynValue[], VerbContext, DynValue>> reg)
    {
        reg["accumulate"] = Accumulate;
        reg["set"] = Set;
        reg["sum"] = Sum;
        reg["count"] = Count;
        reg["min"] = Min;
        reg["max"] = Max;
        reg["avg"] = Avg;
        reg["first"] = First;
        reg["last"] = Last;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private static double? ToDouble(DynValue v)
    {
        if (v.IsNull) return null;
        var d = v.AsDouble();
        if (d.HasValue) return d.Value;
        var s = v.AsString();
        if (s != null && double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
            return parsed;
        return null;
    }

    private static DynValue NumericResult(double v)
    {
        // ReSharper disable once CompareOfFloatsByEqualityOperator
        if (v == Math.Floor(v) && Math.Abs(v) < (double)long.MaxValue)
            return DynValue.Integer((long)v);
        return DynValue.Float(v);
    }

    private static List<DynValue>? ExtractItems(DynValue arg)
    {
        var arr = arg.AsArray();
        if (arr != null) return arr;
        return arg.ExtractArray();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Verb Implementations
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Adds a value to an accumulator. Two forms:
    /// 2 args: accumulate(name, value) - adds value to current accumulator
    /// 3 args: accumulate(name, verb, value) - applies verb operation
    /// </summary>
    private static DynValue Accumulate(DynValue[] args, VerbContext ctx)
    {
        if (args.Length < 2) return DynValue.Null();

        var accName = args[0].AsString();
        if (accName == null) return DynValue.Null();

        DynValue current;
        if (!ctx.Accumulators.TryGetValue(accName, out current!))
            current = DynValue.Null();

        // 2-arg form: accumulate(name, value) - add value to accumulator
        if (args.Length == 2)
        {
            var cv = ToDouble(current) ?? 0.0;
            var vv = ToDouble(args[1]) ?? 0.0;
            current = NumericResult(cv + vv);
            ctx.Accumulators[accName] = current;
            return current;
        }

        // 3-arg form: accumulate(name, verb, value)
        var verbName = args[1].AsString();
        if (verbName == null)
        {
            // If second arg isn't a string verb name, treat as 2-arg add
            var cv = ToDouble(current) ?? 0.0;
            var vv = ToDouble(args[1]) ?? 0.0;
            current = NumericResult(cv + vv);
            ctx.Accumulators[accName] = current;
            return current;
        }

        var value = args[2];

        switch (verbName)
        {
            case "sum":
            case "add":
            {
                var cv = ToDouble(current) ?? 0.0;
                var vv = ToDouble(value) ?? 0.0;
                current = NumericResult(cv + vv);
                break;
            }
            case "count":
            {
                var cv = ToDouble(current) ?? 0.0;
                current = NumericResult(cv + 1.0);
                break;
            }
            case "min":
            {
                var vv = ToDouble(value);
                if (vv.HasValue)
                {
                    var cv = ToDouble(current);
                    current = (!cv.HasValue || vv.Value < cv.Value)
                        ? NumericResult(vv.Value)
                        : current;
                }
                break;
            }
            case "max":
            {
                var vv = ToDouble(value);
                if (vv.HasValue)
                {
                    var cv = ToDouble(current);
                    current = (!cv.HasValue || vv.Value > cv.Value)
                        ? NumericResult(vv.Value)
                        : current;
                }
                break;
            }
            case "concat":
            {
                var cs = current.IsNull ? "" : (current.AsString() ?? current.ToString());
                var vs = value.IsNull ? "" : (value.AsString() ?? value.ToString());
                current = DynValue.String(cs + vs);
                break;
            }
            case "first":
            {
                if (current.IsNull)
                    current = value;
                break;
            }
            case "last":
            {
                current = value;
                break;
            }
            default:
            {
                // Unknown verb — just store the value
                current = value;
                break;
            }
        }

        ctx.Accumulators[accName] = current;
        return current;
    }

    /// <summary>
    /// Sets an accumulator to the given value. args[0]=name, args[1]=value.
    /// </summary>
    private static DynValue Set(DynValue[] args, VerbContext ctx)
    {
        if (args.Length < 2) return DynValue.Null();

        var accName = args[0].AsString();
        if (accName == null) return DynValue.Null();

        ctx.Accumulators[accName] = args[1];
        return args[1];
    }

    /// <summary>
    /// Returns the sum of all numeric values in an array argument.
    /// </summary>
    private static DynValue Sum(DynValue[] args, VerbContext ctx)
    {
        if (args.Length == 0) return DynValue.Integer(0);

        var items = ExtractItems(args[0]);
        if (items == null)
        {
            // Sum all arguments as individual values
            double total = 0;
            for (int i = 0; i < args.Length; i++)
            {
                var v = ToDouble(args[i]);
                if (v.HasValue) total += v.Value;
            }
            return NumericResult(total);
        }

        double sum = 0;
        for (int i = 0; i < items.Count; i++)
        {
            var v = ToDouble(items[i]);
            if (v.HasValue) sum += v.Value;
        }
        return NumericResult(sum);
    }

    /// <summary>
    /// Returns the count of elements in an array argument.
    /// </summary>
    private static DynValue Count(DynValue[] args, VerbContext ctx)
    {
        if (args.Length == 0) return DynValue.Integer(0);

        var items = ExtractItems(args[0]);
        if (items == null) return DynValue.Integer(args.Length);

        return DynValue.Integer(items.Count);
    }

    /// <summary>
    /// Returns the minimum numeric value in an array argument.
    /// </summary>
    private static DynValue Min(DynValue[] args, VerbContext ctx)
    {
        if (args.Length == 0) return DynValue.Null();

        var items = ExtractItems(args[0]);
        if (items == null)
        {
            // Treat all args as values
            items = new List<DynValue>(args);
        }

        double? min = null;
        for (int i = 0; i < items.Count; i++)
        {
            var v = ToDouble(items[i]);
            if (v.HasValue && (!min.HasValue || v.Value < min.Value))
                min = v.Value;
        }

        return min.HasValue ? NumericResult(min.Value) : DynValue.Null();
    }

    /// <summary>
    /// Returns the maximum numeric value in an array argument.
    /// </summary>
    private static DynValue Max(DynValue[] args, VerbContext ctx)
    {
        if (args.Length == 0) return DynValue.Null();

        var items = ExtractItems(args[0]);
        if (items == null)
        {
            items = new List<DynValue>(args);
        }

        double? max = null;
        for (int i = 0; i < items.Count; i++)
        {
            var v = ToDouble(items[i]);
            if (v.HasValue && (!max.HasValue || v.Value > max.Value))
                max = v.Value;
        }

        return max.HasValue ? NumericResult(max.Value) : DynValue.Null();
    }

    /// <summary>
    /// Returns the arithmetic mean of numeric values in an array argument.
    /// </summary>
    private static DynValue Avg(DynValue[] args, VerbContext ctx)
    {
        if (args.Length == 0) return DynValue.Null();

        var items = ExtractItems(args[0]);
        if (items == null)
        {
            items = new List<DynValue>(args);
        }

        double sum = 0;
        int count = 0;
        for (int i = 0; i < items.Count; i++)
        {
            var v = ToDouble(items[i]);
            if (v.HasValue)
            {
                sum += v.Value;
                count++;
            }
        }

        if (count == 0) return DynValue.Null();
        return DynValue.Float(sum / count);
    }

    /// <summary>
    /// Returns the first element of an array argument.
    /// </summary>
    private static DynValue First(DynValue[] args, VerbContext ctx)
    {
        if (args.Length == 0) return DynValue.Null();

        var items = ExtractItems(args[0]);
        if (items == null) return args[0];

        return items.Count > 0 ? items[0] : DynValue.Null();
    }

    /// <summary>
    /// Returns the last element of an array argument.
    /// </summary>
    private static DynValue Last(DynValue[] args, VerbContext ctx)
    {
        if (args.Length == 0) return DynValue.Null();

        var items = ExtractItems(args[0]);
        if (items == null) return args[args.Length - 1];

        return items.Count > 0 ? items[items.Count - 1] : DynValue.Null();
    }
}
