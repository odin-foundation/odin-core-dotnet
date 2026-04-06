#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using Odin.Core.Types;

using Odin.Core.Transform;

namespace Odin.Core.Transform.Verbs;

/// <summary>
/// Collection, array, and time-series transformation verbs. Provides 38 verbs for filtering,
/// sorting, grouping, slicing, and time-series operations on arrays within <see cref="DynValue"/>.
/// </summary>
internal static class CollectionVerbs
{
    /// <summary>
    /// Registers all collection/array/time-series verbs into the provided dictionary.
    /// </summary>
    /// <param name="reg">The verb registration dictionary.</param>
    public static void Register(Dictionary<string, Func<DynValue[], VerbContext, DynValue>> reg)
    {
        reg["filter"] = Filter;
        reg["flatten"] = Flatten;
        reg["distinct"] = Distinct;
        reg["unique"] = Unique;
        reg["sort"] = Sort;
        reg["sortDesc"] = SortDesc;
        reg["sortBy"] = SortBy;
        reg["map"] = Map;
        reg["indexOf"] = IndexOf;
        reg["at"] = At;
        reg["slice"] = Slice;
        reg["reverse"] = Reverse;
        reg["every"] = Every;
        reg["some"] = Some;
        reg["find"] = Find;
        reg["findIndex"] = FindIndex;
        reg["includes"] = Includes;
        reg["concatArrays"] = ConcatArrays;
        reg["zip"] = Zip;
        reg["groupBy"] = GroupBy;
        reg["partition"] = Partition;
        reg["take"] = Take;
        reg["drop"] = Drop;
        reg["chunk"] = Chunk;
        reg["range"] = Range;
        reg["compact"] = Compact;
        reg["pluck"] = Pluck;
        reg["rowNumber"] = RowNumber;
        reg["sample"] = Sample;
        reg["limit"] = Limit;
        reg["dedupe"] = Dedupe;
        reg["cumsum"] = Cumsum;
        reg["cumprod"] = Cumprod;
        reg["diff"] = Diff;
        reg["pctChange"] = PctChange;
        reg["shift"] = Shift;
        reg["lag"] = Lag;
        reg["lead"] = Lead;
        reg["rank"] = Rank;
        reg["fillMissing"] = FillMissing;
        reg["reduce"] = Reduce;
        reg["pivot"] = Pivot;
        reg["unpivot"] = Unpivot;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Extract array from a DynValue, returning empty list if not an array.</summary>
    private static List<DynValue> ExtractArray(DynValue v)
    {
        var arr = v.AsArray();
        if (arr != null) return arr;

        // Try extracting from string-encoded array
        var extracted = v.ExtractArray();
        if (extracted != null) return extracted;

        return new List<DynValue>();
    }

    /// <summary>Check if a DynValue is truthy (non-null, non-false, non-empty-string, non-zero).</summary>
    private static bool IsTruthy(DynValue v)
    {
        if (v.IsNull) return false;
        if (v.Type == DynValueType.Bool) return v.AsBool() == true;
        if (v.Type == DynValueType.String) return !string.IsNullOrEmpty(v.AsString());
        if (v.Type == DynValueType.Integer) return v.AsInt64() != 0;
        if (v.Type == DynValueType.Float) return v.AsDouble() != 0.0;
        return true;
    }

    /// <summary>Extract a double from a DynValue for numeric operations.</summary>
    private static double? ToDouble(DynValue v)
    {
        return v.AsDouble();
    }

    /// <summary>Extract an int from a DynValue.</summary>
    private static int? ToInt(DynValue v)
    {
        var l = v.AsInt64();
        if (l.HasValue) return (int)l.Value;
        var d = v.AsDouble();
        if (d.HasValue) return (int)d.Value;
        var s = v.AsString();
        if (s != null && int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            return parsed;
        return null;
    }

    /// <summary>Compare two DynValues for ordering. Returns -1, 0, or 1.</summary>
    private static int CompareDynValues(DynValue a, DynValue b)
    {
        // Numeric comparison
        var na = ToDouble(a);
        var nb = ToDouble(b);
        if (na.HasValue && nb.HasValue)
            return na.Value.CompareTo(nb.Value);

        // String comparison
        var sa = a.AsString() ?? a.ToString();
        var sb = b.AsString() ?? b.ToString();
        return string.Compare(sa, sb, StringComparison.Ordinal);
    }

    /// <summary>Check if two DynValues are equal using structural comparison.</summary>
    private static bool AreEqual(DynValue a, DynValue b)
    {
        if (a.Equals(b)) return true;

        // Cross-type numeric equality
        var na = ToDouble(a);
        var nb = ToDouble(b);
        // ReSharper disable once CompareOfFloatsByEqualityOperator
        if (na.HasValue && nb.HasValue) return na.Value == nb.Value;

        // Cross-type string equality
        var sa = a.AsString();
        var sb = b.AsString();
        if (sa != null && sb != null) return sa == sb;

        return false;
    }

    /// <summary>Get a field value from an object DynValue by key.</summary>
    private static DynValue GetField(DynValue obj, string fieldName)
    {
        var result = obj.Get(fieldName);
        return result ?? DynValue.Null();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Verb Implementations
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Filters an array by field condition. args[0]=array, args[1]=field name,
    /// args[2]=operator ("=","!=","&lt;","&lt;=","&gt;","&gt;=","contains","startsWith","endsWith"),
    /// args[3]=compare value.
    /// If only 2 args, tests field for truthiness.
    /// </summary>
    private static DynValue Filter(DynValue[] args, VerbContext ctx)
    {
        if (args.Length == 0) return DynValue.Null();
        var arr = ExtractArray(args[0]);
        var result = new List<DynValue>();

        // 4-arg form: filter array "field" "op" value
        if (args.Length >= 4)
        {
            string? fieldName = args[1].AsString();
            string? op = args[2].AsString();
            DynValue compareValue = args[3];

            if (fieldName == null || op == null)
                return DynValue.Array(new List<DynValue>());

            for (int i = 0; i < arr.Count; i++)
            {
                DynValue fieldVal = GetField(arr[i], fieldName);
                string fieldStr = fieldVal.AsString() ?? fieldVal.ToString();
                string cmpStr = compareValue.AsString() ?? compareValue.ToString();

                bool match = false;
                switch (op)
                {
                    case "=":
                    case "==":
                        match = fieldStr == cmpStr;
                        break;
                    case "!=":
                    case "<>":
                        match = fieldStr != cmpStr;
                        break;
                    case "<":
                    {
                        var a = ToDouble(fieldVal);
                        var b = ToDouble(compareValue);
                        match = a.HasValue && b.HasValue && a.Value < b.Value;
                        break;
                    }
                    case "<=":
                    {
                        var a = ToDouble(fieldVal);
                        var b = ToDouble(compareValue);
                        match = a.HasValue && b.HasValue && a.Value <= b.Value;
                        break;
                    }
                    case ">":
                    {
                        var a = ToDouble(fieldVal);
                        var b = ToDouble(compareValue);
                        match = a.HasValue && b.HasValue && a.Value > b.Value;
                        break;
                    }
                    case ">=":
                    {
                        var a = ToDouble(fieldVal);
                        var b = ToDouble(compareValue);
                        match = a.HasValue && b.HasValue && a.Value >= b.Value;
                        break;
                    }
                    case "contains":
                        match = fieldStr.IndexOf(cmpStr, StringComparison.Ordinal) >= 0;
                        break;
                    case "startsWith":
                        match = fieldStr.StartsWith(cmpStr, StringComparison.Ordinal);
                        break;
                    case "endsWith":
                        match = fieldStr.EndsWith(cmpStr, StringComparison.Ordinal);
                        break;
                }

                if (match)
                    result.Add(arr[i]);
            }

            return DynValue.Array(result);
        }

        // 2-arg form: filter by truthiness
        string? fName = args.Length >= 2 ? args[1].AsString() : null;

        for (int i = 0; i < arr.Count; i++)
        {
            DynValue testVal = fName != null ? GetField(arr[i], fName) : arr[i];
            if (IsTruthy(testVal))
                result.Add(arr[i]);
        }

        return DynValue.Array(result);
    }

    /// <summary>
    /// Flattens nested arrays by one level. args[0]=array.
    /// </summary>
    private static DynValue Flatten(DynValue[] args, VerbContext ctx)
    {
        if (args.Length == 0) return DynValue.Null();
        var arr = ExtractArray(args[0]);
        var result = new List<DynValue>();

        for (int i = 0; i < arr.Count; i++)
        {
            var inner = arr[i].AsArray();
            if (inner != null)
            {
                for (int j = 0; j < inner.Count; j++)
                    result.Add(inner[j]);
            }
            else
            {
                result.Add(arr[i]);
            }
        }

        return DynValue.Array(result);
    }

    /// <summary>
    /// Removes duplicate values from an array. args[0]=array.
    /// </summary>
    private static DynValue Distinct(DynValue[] args, VerbContext ctx)
    {
        if (args.Length == 0) return DynValue.Null();
        var arr = ExtractArray(args[0]);
        var result = new List<DynValue>();
        var seen = new HashSet<string>();

        for (int i = 0; i < arr.Count; i++)
        {
            var key = arr[i].ToString();
            if (seen.Add(key))
                result.Add(arr[i]);
        }

        return DynValue.Array(result);
    }

    /// <summary>
    /// Alias for distinct. Removes duplicate values from an array. args[0]=array.
    /// </summary>
    private static DynValue Unique(DynValue[] args, VerbContext ctx)
    {
        return Distinct(args, ctx);
    }

    /// <summary>
    /// Sorts an array in ascending order. args[0]=array.
    /// </summary>
    private static DynValue Sort(DynValue[] args, VerbContext ctx)
    {
        if (args.Length == 0) return DynValue.Null();
        var arr = ExtractArray(args[0]);
        var result = new List<DynValue>(arr);
        result.Sort(CompareDynValues);
        return DynValue.Array(result);
    }

    /// <summary>
    /// Sorts an array in descending order. args[0]=array.
    /// </summary>
    private static DynValue SortDesc(DynValue[] args, VerbContext ctx)
    {
        if (args.Length == 0) return DynValue.Null();
        var arr = ExtractArray(args[0]);
        var result = new List<DynValue>(arr);
        result.Sort((a, b) => CompareDynValues(b, a));
        return DynValue.Array(result);
    }

    /// <summary>
    /// Sorts an array of objects by a field name. args[0]=array, args[1]=field name.
    /// </summary>
    private static DynValue SortBy(DynValue[] args, VerbContext ctx)
    {
        if (args.Length < 2) return DynValue.Null();
        var arr = ExtractArray(args[0]);
        var fieldName = args[1].AsString();
        if (fieldName == null) return DynValue.Array(new List<DynValue>(arr));

        var result = new List<DynValue>(arr);
        result.Sort((a, b) => CompareDynValues(GetField(a, fieldName), GetField(b, fieldName)));
        return DynValue.Array(result);
    }

    /// <summary>
    /// Transforms each element in an array. In this simplified implementation, if args[1]
    /// is a field name string, it extracts that field from each object element.
    /// Otherwise returns the array unchanged. args[0]=array, args[1]=field name.
    /// </summary>
    private static DynValue Map(DynValue[] args, VerbContext ctx)
    {
        if (args.Length == 0) return DynValue.Null();
        var arr = ExtractArray(args[0]);

        if (args.Length < 2)
            return DynValue.Array(new List<DynValue>(arr));

        var fieldName = args[1].AsString();
        if (fieldName == null)
            return DynValue.Array(new List<DynValue>(arr));

        var result = new List<DynValue>();
        for (int i = 0; i < arr.Count; i++)
            result.Add(GetField(arr[i], fieldName));

        return DynValue.Array(result);
    }

    /// <summary>
    /// Returns the index of a value in an array, or -1 if not found.
    /// args[0]=array, args[1]=value.
    /// </summary>
    private static DynValue IndexOf(DynValue[] args, VerbContext ctx)
    {
        if (args.Length < 2) return DynValue.Integer(-1);
        var arr = ExtractArray(args[0]);

        for (int i = 0; i < arr.Count; i++)
        {
            if (AreEqual(arr[i], args[1]))
                return DynValue.Integer(i);
        }

        return DynValue.Integer(-1);
    }

    /// <summary>
    /// Returns the element at a given index. Supports negative indexing.
    /// args[0]=array, args[1]=index.
    /// </summary>
    private static DynValue At(DynValue[] args, VerbContext ctx)
    {
        if (args.Length < 2) return DynValue.Null();
        var arr = ExtractArray(args[0]);
        var idx = ToInt(args[1]);
        if (!idx.HasValue) return DynValue.Null();

        int index = idx.Value;
        if (index < 0) index = arr.Count + index;
        if (index < 0 || index >= arr.Count) return DynValue.Null();

        return arr[index];
    }

    /// <summary>
    /// Returns a slice of an array. args[0]=array, args[1]=start, args[2]=optional end.
    /// </summary>
    private static DynValue Slice(DynValue[] args, VerbContext ctx)
    {
        if (args.Length < 2) return DynValue.Null();
        var arr = ExtractArray(args[0]);
        var startIdx = ToInt(args[1]);
        if (!startIdx.HasValue) return DynValue.Null();

        int start = startIdx.Value;
        if (start < 0) start = Math.Max(0, arr.Count + start);
        if (start > arr.Count) start = arr.Count;

        int end = arr.Count;
        if (args.Length >= 3)
        {
            var endIdx = ToInt(args[2]);
            if (endIdx.HasValue)
            {
                end = endIdx.Value;
                if (end < 0) end = Math.Max(0, arr.Count + end);
                if (end > arr.Count) end = arr.Count;
            }
        }

        if (end <= start)
            return DynValue.Array(new List<DynValue>());

        var result = new List<DynValue>();
        for (int i = start; i < end; i++)
            result.Add(arr[i]);

        return DynValue.Array(result);
    }

    /// <summary>
    /// Reverses the order of an array. args[0]=array.
    /// </summary>
    private static DynValue Reverse(DynValue[] args, VerbContext ctx)
    {
        if (args.Length == 0) return DynValue.Null();
        var arr = ExtractArray(args[0]);
        var result = new List<DynValue>(arr);
        result.Reverse();
        return DynValue.Array(result);
    }

    /// <summary>
    /// Returns true if all elements are truthy. If args[1] is a field name,
    /// tests that field on each element. args[0]=array, args[1]=optional field.
    /// </summary>
    private static DynValue Every(DynValue[] args, VerbContext ctx)
    {
        if (args.Length == 0) return DynValue.Bool(true);
        var arr = ExtractArray(args[0]);
        string? fieldName = args.Length >= 2 ? args[1].AsString() : null;

        for (int i = 0; i < arr.Count; i++)
        {
            DynValue testVal = fieldName != null ? GetField(arr[i], fieldName) : arr[i];
            if (!IsTruthy(testVal))
                return DynValue.Bool(false);
        }

        return DynValue.Bool(true);
    }

    /// <summary>
    /// Returns true if any element is truthy. If args[1] is a field name,
    /// tests that field on each element. args[0]=array, args[1]=optional field.
    /// </summary>
    private static DynValue Some(DynValue[] args, VerbContext ctx)
    {
        if (args.Length == 0) return DynValue.Bool(false);
        var arr = ExtractArray(args[0]);
        string? fieldName = args.Length >= 2 ? args[1].AsString() : null;

        for (int i = 0; i < arr.Count; i++)
        {
            DynValue testVal = fieldName != null ? GetField(arr[i], fieldName) : arr[i];
            if (IsTruthy(testVal))
                return DynValue.Bool(true);
        }

        return DynValue.Bool(false);
    }

    /// <summary>
    /// Returns the first truthy element. If args[1] is a field name,
    /// tests that field on each element. args[0]=array, args[1]=optional field.
    /// </summary>
    private static DynValue Find(DynValue[] args, VerbContext ctx)
    {
        if (args.Length == 0) return DynValue.Null();
        var arr = ExtractArray(args[0]);
        string? fieldName = args.Length >= 2 ? args[1].AsString() : null;

        for (int i = 0; i < arr.Count; i++)
        {
            DynValue testVal = fieldName != null ? GetField(arr[i], fieldName) : arr[i];
            if (IsTruthy(testVal))
                return arr[i];
        }

        return DynValue.Null();
    }

    /// <summary>
    /// Returns the index of the first truthy element, or -1 if not found.
    /// args[0]=array, args[1]=optional field.
    /// </summary>
    private static DynValue FindIndex(DynValue[] args, VerbContext ctx)
    {
        if (args.Length == 0) return DynValue.Integer(-1);
        var arr = ExtractArray(args[0]);
        string? fieldName = args.Length >= 2 ? args[1].AsString() : null;

        for (int i = 0; i < arr.Count; i++)
        {
            DynValue testVal = fieldName != null ? GetField(arr[i], fieldName) : arr[i];
            if (IsTruthy(testVal))
                return DynValue.Integer(i);
        }

        return DynValue.Integer(-1);
    }

    /// <summary>
    /// Returns true if the array contains the given value. args[0]=array, args[1]=value.
    /// </summary>
    private static DynValue Includes(DynValue[] args, VerbContext ctx)
    {
        if (args.Length < 2) return DynValue.Bool(false);
        var arr = ExtractArray(args[0]);

        for (int i = 0; i < arr.Count; i++)
        {
            if (AreEqual(arr[i], args[1]))
                return DynValue.Bool(true);
        }

        return DynValue.Bool(false);
    }

    /// <summary>
    /// Concatenates multiple arrays into one. All arguments are arrays to merge.
    /// </summary>
    private static DynValue ConcatArrays(DynValue[] args, VerbContext ctx)
    {
        var result = new List<DynValue>();
        for (int i = 0; i < args.Length; i++)
        {
            var arr = ExtractArray(args[i]);
            for (int j = 0; j < arr.Count; j++)
                result.Add(arr[j]);
        }
        return DynValue.Array(result);
    }

    /// <summary>
    /// Combines multiple arrays element-wise into an array of arrays.
    /// args = array1, array2, ...
    /// </summary>
    private static DynValue Zip(DynValue[] args, VerbContext ctx)
    {
        if (args.Length == 0) return DynValue.Array(new List<DynValue>());

        var arrays = new List<List<DynValue>>();
        int maxLen = 0;
        for (int i = 0; i < args.Length; i++)
        {
            var arr = ExtractArray(args[i]);
            arrays.Add(arr);
            if (arr.Count > maxLen) maxLen = arr.Count;
        }

        var result = new List<DynValue>();
        for (int i = 0; i < maxLen; i++)
        {
            var tuple = new List<DynValue>();
            for (int j = 0; j < arrays.Count; j++)
            {
                tuple.Add(i < arrays[j].Count ? arrays[j][i] : DynValue.Null());
            }
            result.Add(DynValue.Array(tuple));
        }

        return DynValue.Array(result);
    }

    /// <summary>
    /// Groups an array of objects by a field value. args[0]=array, args[1]=field name.
    /// Returns an object where keys are group values and values are arrays of matching elements.
    /// </summary>
    private static DynValue GroupBy(DynValue[] args, VerbContext ctx)
    {
        if (args.Length < 2) return DynValue.Null();
        var arr = ExtractArray(args[0]);
        var fieldName = args[1].AsString();
        if (fieldName == null) return DynValue.Null();

        var groups = new Dictionary<string, List<DynValue>>();
        var orderedKeys = new List<string>();

        for (int i = 0; i < arr.Count; i++)
        {
            var keyVal = GetField(arr[i], fieldName);
            var key = keyVal.IsNull ? "null" : (keyVal.AsString() ?? keyVal.ToString());

            if (!groups.TryGetValue(key, out var groupList))
            {
                groupList = new List<DynValue>();
                groups[key] = groupList;
                orderedKeys.Add(key);
            }
            groupList.Add(arr[i]);
        }

        var entries = new List<KeyValuePair<string, DynValue>>();
        for (int i = 0; i < orderedKeys.Count; i++)
        {
            entries.Add(new KeyValuePair<string, DynValue>(orderedKeys[i], DynValue.Array(groups[orderedKeys[i]])));
        }

        return DynValue.Object(entries);
    }

    /// <summary>
    /// Splits an array into two: elements matching a condition and those not.
    /// If args[1] is a field name, tests that field for truthiness.
    /// args[0]=array, args[1]=field name. Returns [matching, non-matching].
    /// </summary>
    private static DynValue Partition(DynValue[] args, VerbContext ctx)
    {
        if (args.Length == 0) return DynValue.Null();
        var arr = ExtractArray(args[0]);
        string? fieldName = args.Length >= 2 ? args[1].AsString() : null;

        var pass = new List<DynValue>();
        var fail = new List<DynValue>();

        for (int i = 0; i < arr.Count; i++)
        {
            DynValue testVal = fieldName != null ? GetField(arr[i], fieldName) : arr[i];
            if (IsTruthy(testVal))
                pass.Add(arr[i]);
            else
                fail.Add(arr[i]);
        }

        var result = new List<DynValue> { DynValue.Array(pass), DynValue.Array(fail) };
        return DynValue.Array(result);
    }

    /// <summary>
    /// Returns the first N elements of an array. args[0]=array, args[1]=count.
    /// </summary>
    private static DynValue Take(DynValue[] args, VerbContext ctx)
    {
        if (args.Length < 2) return DynValue.Null();
        var arr = ExtractArray(args[0]);
        var count = ToInt(args[1]);
        if (!count.HasValue) return DynValue.Null();

        int n = Math.Min(Math.Max(0, count.Value), arr.Count);
        var result = new List<DynValue>();
        for (int i = 0; i < n; i++)
            result.Add(arr[i]);

        return DynValue.Array(result);
    }

    /// <summary>
    /// Skips the first N elements and returns the rest. args[0]=array, args[1]=count.
    /// </summary>
    private static DynValue Drop(DynValue[] args, VerbContext ctx)
    {
        if (args.Length < 2) return DynValue.Null();
        var arr = ExtractArray(args[0]);
        var count = ToInt(args[1]);
        if (!count.HasValue) return DynValue.Null();

        int skip = Math.Min(Math.Max(0, count.Value), arr.Count);
        var result = new List<DynValue>();
        for (int i = skip; i < arr.Count; i++)
            result.Add(arr[i]);

        return DynValue.Array(result);
    }

    /// <summary>
    /// Splits an array into chunks of size N. args[0]=array, args[1]=chunk size.
    /// </summary>
    private static DynValue Chunk(DynValue[] args, VerbContext ctx)
    {
        if (args.Length < 2) return DynValue.Null();
        var arr = ExtractArray(args[0]);
        var size = ToInt(args[1]);
        if (!size.HasValue || size.Value <= 0) return DynValue.Null();

        var result = new List<DynValue>();
        for (int i = 0; i < arr.Count; i += size.Value)
        {
            var chunkList = new List<DynValue>();
            int end = Math.Min(i + size.Value, arr.Count);
            for (int j = i; j < end; j++)
                chunkList.Add(arr[j]);
            result.Add(DynValue.Array(chunkList));
        }

        return DynValue.Array(result);
    }

    /// <summary>
    /// Generates a numeric range array. args[0]=start (or end if only 1 arg),
    /// args[1]=end, args[2]=optional step (default 1).
    /// </summary>
    private static DynValue Range(DynValue[] args, VerbContext ctx)
    {
        if (args.Length == 0) return DynValue.Array(new List<DynValue>());

        int start, end, step;

        if (args.Length == 1)
        {
            start = 0;
            var e = ToInt(args[0]);
            if (!e.HasValue) return DynValue.Array(new List<DynValue>());
            end = e.Value;
            step = 1;
        }
        else
        {
            var s = ToInt(args[0]);
            var e = ToInt(args[1]);
            if (!s.HasValue || !e.HasValue) return DynValue.Array(new List<DynValue>());
            start = s.Value;
            end = e.Value;
            step = 1;
            if (args.Length >= 3)
            {
                var st = ToInt(args[2]);
                if (st.HasValue && st.Value != 0) step = st.Value;
            }
        }

        var result = new List<DynValue>();
        // Safety limit to prevent runaway allocation
        int maxItems = 10000;

        if (step > 0)
        {
            for (int i = start; i < end && result.Count < maxItems; i += step)
                result.Add(DynValue.Integer(i));
        }
        else if (step < 0)
        {
            for (int i = start; i > end && result.Count < maxItems; i += step)
                result.Add(DynValue.Integer(i));
        }

        return DynValue.Array(result);
    }

    /// <summary>
    /// Removes null and empty string values from an array. args[0]=array.
    /// </summary>
    private static DynValue Compact(DynValue[] args, VerbContext ctx)
    {
        if (args.Length == 0) return DynValue.Null();
        var arr = ExtractArray(args[0]);
        var result = new List<DynValue>();

        for (int i = 0; i < arr.Count; i++)
        {
            if (arr[i].IsNull) continue;
            if (arr[i].Type == DynValueType.String && string.IsNullOrEmpty(arr[i].AsString())) continue;
            result.Add(arr[i]);
        }

        return DynValue.Array(result);
    }

    /// <summary>
    /// Extracts a single field from each object in an array.
    /// args[0]=array, args[1]=field name.
    /// </summary>
    private static DynValue Pluck(DynValue[] args, VerbContext ctx)
    {
        if (args.Length < 2) return DynValue.Null();
        var arr = ExtractArray(args[0]);
        var fieldName = args[1].AsString();
        if (fieldName == null) return DynValue.Null();

        var result = new List<DynValue>();
        for (int i = 0; i < arr.Count; i++)
            result.Add(GetField(arr[i], fieldName));

        return DynValue.Array(result);
    }

    /// <summary>
    /// Returns a row counter from accumulators, incrementing on each call.
    /// Uses ctx.Accumulators["_rowNumber"] to track state.
    /// </summary>
    private static DynValue RowNumber(DynValue[] args, VerbContext ctx)
    {
        long current = 0;
        if (ctx.Accumulators.TryGetValue("_rowNumber", out var acc))
        {
            var val = acc.AsInt64();
            if (val.HasValue) current = val.Value;
        }
        current++;
        ctx.Accumulators["_rowNumber"] = DynValue.Integer(current);
        return DynValue.Integer(current);
    }

    /// <summary>
    /// Returns N random elements from an array. args[0]=array, args[1]=count.
    /// </summary>
    private static DynValue Sample(DynValue[] args, VerbContext ctx)
    {
        if (args.Length < 2) return DynValue.Null();
        var arr = ExtractArray(args[0]);
        var count = ToInt(args[1]);
        if (!count.HasValue || count.Value <= 0) return DynValue.Array(new List<DynValue>());

        int n = Math.Min(count.Value, arr.Count);
        // Fisher-Yates shuffle on a copy, then take first N
        var shuffled = new List<DynValue>(arr);

        // Check for optional 3rd arg as integer seed
        if (args.Length >= 3)
        {
            var seedVal = ToInt(args[2]);
            if (seedVal.HasValue)
            {
                uint state = (uint)seedVal.Value;
                for (int i = shuffled.Count - 1; i > 0; i--)
                {
                    double r = NumericVerbs.Mulberry32(ref state);
                    int j = (int)Math.Floor(r * (i + 1));
                    var temp = shuffled[i];
                    shuffled[i] = shuffled[j];
                    shuffled[j] = temp;
                }

                var result = new List<DynValue>();
                for (int i = 0; i < n; i++)
                    result.Add(shuffled[i]);
                return DynValue.Array(result);
            }
        }

        var rng = new Random();
        for (int i = shuffled.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            var temp = shuffled[i];
            shuffled[i] = shuffled[j];
            shuffled[j] = temp;
        }

        var result2 = new List<DynValue>();
        for (int i = 0; i < n; i++)
            result2.Add(shuffled[i]);

        return DynValue.Array(result2);
    }

    /// <summary>
    /// Alias for take. Returns the first N elements. args[0]=array, args[1]=count.
    /// </summary>
    private static DynValue Limit(DynValue[] args, VerbContext ctx)
    {
        return Take(args, ctx);
    }

    /// <summary>
    /// Removes consecutive duplicate values from an array. args[0]=array.
    /// </summary>
    private static DynValue Dedupe(DynValue[] args, VerbContext ctx)
    {
        if (args.Length == 0) return DynValue.Null();
        var arr = ExtractArray(args[0]);
        if (arr.Count == 0) return DynValue.Array(new List<DynValue>());

        var result = new List<DynValue>();
        result.Add(arr[0]);

        for (int i = 1; i < arr.Count; i++)
        {
            if (!AreEqual(arr[i], arr[i - 1]))
                result.Add(arr[i]);
        }

        return DynValue.Array(result);
    }

    /// <summary>
    /// Cumulative sum of a numeric array. args[0]=array.
    /// </summary>
    private static DynValue Cumsum(DynValue[] args, VerbContext ctx)
    {
        if (args.Length == 0) return DynValue.Null();
        var arr = ExtractArray(args[0]);
        var result = new List<DynValue>();
        double running = 0;

        for (int i = 0; i < arr.Count; i++)
        {
            var v = ToDouble(arr[i]);
            if (v.HasValue)
            {
                running += v.Value;
                result.Add(NumericResult(running));
            }
            else
            {
                result.Add(DynValue.Null());
            }
        }

        return DynValue.Array(result);
    }

    /// <summary>
    /// Cumulative product of a numeric array. args[0]=array.
    /// </summary>
    private static DynValue Cumprod(DynValue[] args, VerbContext ctx)
    {
        if (args.Length == 0) return DynValue.Null();
        var arr = ExtractArray(args[0]);
        var result = new List<DynValue>();
        double running = 1;

        for (int i = 0; i < arr.Count; i++)
        {
            var v = ToDouble(arr[i]);
            if (v.HasValue)
            {
                running *= v.Value;
                result.Add(NumericResult(running));
            }
            else
            {
                result.Add(DynValue.Null());
            }
        }

        return DynValue.Array(result);
    }

    /// <summary>
    /// Differences between consecutive elements. First element is null.
    /// args[0]=array.
    /// </summary>
    private static DynValue Diff(DynValue[] args, VerbContext ctx)
    {
        if (args.Length == 0) return DynValue.Null();
        var arr = ExtractArray(args[0]);
        var result = new List<DynValue>();

        if (arr.Count > 0)
            result.Add(DynValue.Null());

        for (int i = 1; i < arr.Count; i++)
        {
            var curr = ToDouble(arr[i]);
            var prev = ToDouble(arr[i - 1]);
            if (curr.HasValue && prev.HasValue)
                result.Add(NumericResult(curr.Value - prev.Value));
            else
                result.Add(DynValue.Null());
        }

        return DynValue.Array(result);
    }

    /// <summary>
    /// Percentage change between consecutive elements. First element is null.
    /// args[0]=array.
    /// </summary>
    private static DynValue PctChange(DynValue[] args, VerbContext ctx)
    {
        if (args.Length == 0) return DynValue.Null();
        var arr = ExtractArray(args[0]);
        var result = new List<DynValue>();

        if (arr.Count > 0)
            result.Add(DynValue.Null());

        for (int i = 1; i < arr.Count; i++)
        {
            var curr = ToDouble(arr[i]);
            var prev = ToDouble(arr[i - 1]);
            // ReSharper disable once CompareOfFloatsByEqualityOperator
            if (curr.HasValue && prev.HasValue && prev.Value != 0.0)
                result.Add(DynValue.Float((curr.Value - prev.Value) / prev.Value));
            else
                result.Add(DynValue.Null());
        }

        return DynValue.Array(result);
    }

    /// <summary>
    /// Shifts array elements by N positions. Positive shifts right (prepends nulls),
    /// negative shifts left (appends nulls). args[0]=array, args[1]=positions.
    /// </summary>
    private static DynValue Shift(DynValue[] args, VerbContext ctx)
    {
        if (args.Length < 2) return DynValue.Null();
        var arr = ExtractArray(args[0]);
        var n = ToInt(args[1]);
        if (!n.HasValue) return DynValue.Array(new List<DynValue>(arr));

        return ShiftArray(arr, n.Value);
    }

    /// <summary>
    /// Shifts array elements forward by N positions (alias for shift with positive N).
    /// Previous values become null. args[0]=array, args[1]=positions (default 1).
    /// </summary>
    private static DynValue Lag(DynValue[] args, VerbContext ctx)
    {
        if (args.Length == 0) return DynValue.Null();
        var arr = ExtractArray(args[0]);
        int n = 1;
        if (args.Length >= 2)
        {
            var nVal = ToInt(args[1]);
            if (nVal.HasValue) n = nVal.Value;
        }

        return ShiftArray(arr, n);
    }

    /// <summary>
    /// Shifts array elements backward by N positions (alias for shift with negative N).
    /// Trailing values become null. args[0]=array, args[1]=positions (default 1).
    /// </summary>
    private static DynValue Lead(DynValue[] args, VerbContext ctx)
    {
        if (args.Length == 0) return DynValue.Null();
        var arr = ExtractArray(args[0]);
        int n = 1;
        if (args.Length >= 2)
        {
            var nVal = ToInt(args[1]);
            if (nVal.HasValue) n = nVal.Value;
        }

        return ShiftArray(arr, -n);
    }

    /// <summary>Internal helper to shift an array by N positions.</summary>
    private static DynValue ShiftArray(List<DynValue> arr, int n)
    {
        var result = new List<DynValue>(arr.Count);
        int absN = Math.Abs(n);

        if (n > 0)
        {
            // Shift right: prepend nulls
            for (int i = 0; i < Math.Min(absN, arr.Count); i++)
                result.Add(DynValue.Null());
            for (int i = 0; i < arr.Count - absN; i++)
                result.Add(arr[i]);
        }
        else if (n < 0)
        {
            // Shift left: append nulls
            for (int i = absN; i < arr.Count; i++)
                result.Add(arr[i]);
            for (int i = 0; i < Math.Min(absN, arr.Count); i++)
                result.Add(DynValue.Null());
        }
        else
        {
            for (int i = 0; i < arr.Count; i++)
                result.Add(arr[i]);
        }

        return DynValue.Array(result);
    }

    /// <summary>
    /// Ranks values in a numeric array. Equal values get the same rank (dense ranking).
    /// args[0]=array.
    /// </summary>
    private static DynValue Rank(DynValue[] args, VerbContext ctx)
    {
        if (args.Length == 0) return DynValue.Null();
        var arr = ExtractArray(args[0]);

        // Build list of (value, originalIndex) for non-null entries
        var indexed = new List<(double val, int idx)>();
        for (int i = 0; i < arr.Count; i++)
        {
            var v = ToDouble(arr[i]);
            if (v.HasValue)
                indexed.Add((v.Value, i));
        }

        // Sort descending for ranking (highest = rank 1)
        indexed.Sort((a, b) => b.val.CompareTo(a.val));

        var result = new DynValue[arr.Count];
        for (int i = 0; i < arr.Count; i++)
            result[i] = DynValue.Null();

        int currentRank = 1;
        for (int i = 0; i < indexed.Count; i++)
        {
            // ReSharper disable once CompareOfFloatsByEqualityOperator
            if (i > 0 && indexed[i].val != indexed[i - 1].val)
                currentRank = i + 1;
            result[indexed[i].idx] = DynValue.Integer(currentRank);
        }

        return DynValue.Array(new List<DynValue>(result));
    }

    /// <summary>
    /// Fills null values in an array. args[0]=array, args[1]=strategy or fill value.
    /// Strategy can be "forward" (use previous value), "backward" (use next value),
    /// or a literal value to fill with.
    /// </summary>
    private static DynValue FillMissing(DynValue[] args, VerbContext ctx)
    {
        if (args.Length < 2) return DynValue.Null();
        var arr = ExtractArray(args[0]);
        var strategyStr = args[1].AsString();

        var result = new List<DynValue>(arr);

        if (strategyStr == "forward")
        {
            DynValue? last = null;
            for (int i = 0; i < result.Count; i++)
            {
                if (result[i].IsNull && last != null)
                    result[i] = last;
                else if (!result[i].IsNull)
                    last = result[i];
            }
        }
        else if (strategyStr == "backward")
        {
            DynValue? next = null;
            for (int i = result.Count - 1; i >= 0; i--)
            {
                if (result[i].IsNull && next != null)
                    result[i] = next;
                else if (!result[i].IsNull)
                    next = result[i];
            }
        }
        else
        {
            // Fill with literal value
            for (int i = 0; i < result.Count; i++)
            {
                if (result[i].IsNull)
                    result[i] = args[1];
            }
        }

        return DynValue.Array(result);
    }

    /// <summary>Return Integer if whole, Float otherwise.</summary>
    private static DynValue NumericResult(double v)
    {
        if (v == Math.Floor(v) && Math.Abs(v) < long.MaxValue)
            return DynValue.Integer((long)v);
        return DynValue.Float(v);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Reduce / Pivot / Unpivot
    // ─────────────────────────────────────────────────────────────────────────

    private static DynValue Reduce(DynValue[] args, VerbContext ctx)
    {
        if (args.Length < 3) return DynValue.Null();

        var arr = ExtractArray(args[0]);
        if (arr.Count == 0) return args[2]; // empty array → return initial value

        var verbName = args[1].AsString();
        if (verbName == null) return DynValue.Null();

        var registry = VerbRegistry.Instance;
        DynValue accumulator = args[2];

        foreach (var item in arr)
        {
            accumulator = registry.Invoke(verbName, new[] { accumulator, item }, ctx);
        }

        return accumulator;
    }

    private static DynValue Pivot(DynValue[] args, VerbContext ctx)
    {
        if (args.Length < 3) return DynValue.Null();

        var arr = ExtractArray(args[0]);
        var keyField = args[1].AsString();
        var valueField = args[2].AsString();
        if (keyField == null || valueField == null) return DynValue.Null();

        var entries = new List<KeyValuePair<string, DynValue>>();

        foreach (var item in arr)
        {
            var objEntries = item.AsObject();
            if (objEntries == null) continue;

            var keyVal = item.Get(keyField);
            if (keyVal == null || keyVal.IsNull) continue;

            var valueVal = item.Get(valueField);
            if (valueVal == null) continue;

            string key;
            if (keyVal.AsString() != null) key = keyVal.AsString()!;
            else if (keyVal.AsInt64().HasValue) key = keyVal.AsInt64()!.Value.ToString(CultureInfo.InvariantCulture);
            else if (keyVal.AsDouble().HasValue) key = keyVal.AsDouble()!.Value.ToString(CultureInfo.InvariantCulture);
            else key = keyVal.ToString()!;

            // Remove existing entry with same key (last value wins)
            entries.RemoveAll(e => e.Key == key);
            entries.Add(new KeyValuePair<string, DynValue>(key, valueVal));
        }

        return DynValue.Object(entries);
    }

    private static DynValue Unpivot(DynValue[] args, VerbContext ctx)
    {
        if (args.Length < 3) return DynValue.Null();

        var objEntries = args[0].AsObject();
        if (objEntries == null)
        {
            objEntries = args[0].ExtractObject();
            if (objEntries == null) return DynValue.Null();
        }

        var keyName = args[1].AsString();
        var valueName = args[2].AsString();
        if (keyName == null || valueName == null) return DynValue.Null();

        var result = new List<DynValue>();

        foreach (var entry in objEntries)
        {
            var row = new List<KeyValuePair<string, DynValue>>
            {
                new KeyValuePair<string, DynValue>(keyName, DynValue.String(entry.Key)),
                new KeyValuePair<string, DynValue>(valueName, entry.Value)
            };
            result.Add(DynValue.Object(row));
        }

        return DynValue.Array(result);
    }
}
