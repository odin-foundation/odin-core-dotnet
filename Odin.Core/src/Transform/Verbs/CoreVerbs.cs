#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using Odin.Core.Types;

using Odin.Core.Transform;

namespace Odin.Core.Transform.Verbs;

/// <summary>
/// Core transformation verbs: string manipulation, coalescing, conditionals, and lookups.
/// </summary>
internal static class CoreVerbs
{
    /// <summary>
    /// Registers all core verbs into the provided dictionary.
    /// </summary>
    /// <param name="reg">The verb registration dictionary.</param>
    public static void Register(Dictionary<string, Func<DynValue[], VerbContext, DynValue>> reg)
    {
        reg["concat"] = Concat;
        reg["upper"] = Upper;
        reg["lower"] = Lower;
        reg["trim"] = Trim;
        reg["trimLeft"] = TrimLeft;
        reg["trimRight"] = TrimRight;
        reg["coalesce"] = Coalesce;
        reg["ifNull"] = IfNull;
        reg["ifEmpty"] = IfEmpty;
        reg["ifElse"] = IfElse;
        reg["lookup"] = Lookup;
        reg["lookupDefault"] = LookupDefault;
    }

    /// <summary>
    /// Concatenates all arguments as strings. Null values are skipped.
    /// </summary>
    private static DynValue Concat(DynValue[] args, VerbContext ctx)
    {
        var result = new System.Text.StringBuilder();
        for (int i = 0; i < args.Length; i++)
        {
            if (!args[i].IsNull)
                result.Append(VerbHelpers.CoerceStr(args[i]));
        }
        return DynValue.String(result.ToString());
    }

    /// <summary>
    /// Converts a string to upper case. Null passes through.
    /// </summary>
    private static DynValue Upper(DynValue[] args, VerbContext ctx)
    {
        if (args.Length == 0)
            throw new InvalidOperationException("upper: expected string argument");

        var s = args[0].AsString();
        if (args[0].IsNull) return DynValue.Null();
        if (s == null)
            throw new InvalidOperationException("upper: expected string argument");

        return DynValue.String(s.ToUpperInvariant());
    }

    /// <summary>
    /// Converts a string to lower case. Null passes through.
    /// </summary>
    private static DynValue Lower(DynValue[] args, VerbContext ctx)
    {
        if (args.Length == 0)
            throw new InvalidOperationException("lower: expected string argument");

        var s = args[0].AsString();
        if (args[0].IsNull) return DynValue.Null();
        if (s == null)
            throw new InvalidOperationException("lower: expected string argument");

        return DynValue.String(s.ToLowerInvariant());
    }

    /// <summary>
    /// Trims leading and trailing whitespace from a string. Null passes through.
    /// </summary>
    private static DynValue Trim(DynValue[] args, VerbContext ctx)
    {
        if (args.Length == 0)
            throw new InvalidOperationException("trim: expected string argument");

        var s = args[0].AsString();
        if (args[0].IsNull) return DynValue.Null();
        if (s == null)
            throw new InvalidOperationException("trim: expected string argument");

        return DynValue.String(s.Trim());
    }

    /// <summary>
    /// Trims leading whitespace from a string. Null passes through.
    /// </summary>
    private static DynValue TrimLeft(DynValue[] args, VerbContext ctx)
    {
        if (args.Length == 0)
            throw new InvalidOperationException("trimLeft: expected string argument");

        var s = args[0].AsString();
        if (args[0].IsNull) return DynValue.Null();
        if (s == null)
            throw new InvalidOperationException("trimLeft: expected string argument");

        return DynValue.String(s.TrimStart());
    }

    /// <summary>
    /// Trims trailing whitespace from a string. Null passes through.
    /// </summary>
    private static DynValue TrimRight(DynValue[] args, VerbContext ctx)
    {
        if (args.Length == 0)
            throw new InvalidOperationException("trimRight: expected string argument");

        var s = args[0].AsString();
        if (args[0].IsNull) return DynValue.Null();
        if (s == null)
            throw new InvalidOperationException("trimRight: expected string argument");

        return DynValue.String(s.TrimEnd());
    }

    /// <summary>
    /// Returns the first non-null, non-empty argument. Returns null if all are null/empty.
    /// </summary>
    private static DynValue Coalesce(DynValue[] args, VerbContext ctx)
    {
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i].IsNull) continue;
            if (args[i].Type == DynValueType.String && args[i].AsString() == "") continue;
            return args[i];
        }
        return DynValue.Null();
    }

    /// <summary>
    /// If the first argument is null, returns the second argument; otherwise returns the first.
    /// </summary>
    private static DynValue IfNull(DynValue[] args, VerbContext ctx)
    {
        if (args.Length < 2)
            throw new InvalidOperationException("ifNull: requires 2 arguments");

        return args[0].IsNull ? args[1] : args[0];
    }

    /// <summary>
    /// If the first argument is null or an empty string, returns the second argument;
    /// otherwise returns the first.
    /// </summary>
    private static DynValue IfEmpty(DynValue[] args, VerbContext ctx)
    {
        if (args.Length < 2)
            throw new InvalidOperationException("ifEmpty: requires 2 arguments");

        bool isEmpty = args[0].IsNull
            || (args[0].Type == DynValueType.String && args[0].AsString() == "");

        return isEmpty ? args[1] : args[0];
    }

    /// <summary>
    /// If the first argument is truthy, returns the second; otherwise returns the third.
    /// </summary>
    private static DynValue IfElse(DynValue[] args, VerbContext ctx)
    {
        if (args.Length < 3)
            throw new InvalidOperationException("ifElse: requires 3 arguments");

        return VerbHelpers.IsTruthy(args[0]) ? args[1] : args[2];
    }

    /// <summary>
    /// Looks up a value from a lookup table. First arg is "tableName.column", remaining args are keys.
    /// Returns the table default or null if not found.
    /// </summary>
    private static DynValue Lookup(DynValue[] args, VerbContext ctx)
    {
        if (args.Length < 2)
            throw new InvalidOperationException("lookup: requires at least 2 arguments (table.column, key...)");

        var tableRef = args[0].AsString();
        if (tableRef == null)
            throw new InvalidOperationException("lookup: first argument must be a string table reference");

        var keys = new DynValue[args.Length - 1];
        Array.Copy(args, 1, keys, 0, keys.Length);

        var result = DoTableLookup(tableRef, keys, ctx.Tables);
        if (result != null) return result;

        var dotIdx = tableRef.IndexOf('.');
        var tableName = dotIdx >= 0 ? tableRef.Substring(0, dotIdx) : tableRef;

        // Undeclared table is T003, distinct from a missing key (T004).
        if (!ctx.Tables.TryGetValue(tableName, out var table))
        {
            ReportTableNotFound(ctx, tableName);
            return DynValue.Null();
        }

        // Check for table-level default
        if (table.Default != null)
            return table.Default;

        // Miss: report via the onMissing policy (default silent null).
        ReportLookupMiss(ctx, tableName, FormatMatchKey(keys));
        return DynValue.Null();
    }

    /// <summary>
    /// Report a lookup miss honoring the onMissing policy.
    /// Defaults to silent null; raises only when onMissing is fail/warn.
    /// </summary>
    private static void ReportLookupMiss(VerbContext ctx, string tableName, string key)
    {
        var message = string.Format(CultureInfo.InvariantCulture,
            "Lookup key not found in table '{0}': {1}", tableName, key);
        if (ctx.OnMissing == "fail")
            ctx.Errors.Add(new TransformError
            {
                Code = TransformErrorCode.LookupKeyNotFound.Code(),
                Message = message,
            });
        else if (ctx.OnMissing == "warn")
            ctx.Warnings.Add(new TransformWarning
            {
                Code = TransformErrorCode.LookupKeyNotFound.Code(),
                Message = message,
            });
    }

    /// <summary>
    /// Report a missing lookup table (T003) honoring the onMissing policy.
    /// Distinct from a missing key (T004): the referenced table was never declared.
    /// </summary>
    private static void ReportTableNotFound(VerbContext ctx, string tableName)
    {
        var message = "Lookup table not found: " + tableName;
        if (ctx.OnMissing == "fail")
            ctx.Errors.Add(new TransformError
            {
                Code = TransformErrorCode.LookupTableNotFound.Code(),
                Message = message,
            });
        else if (ctx.OnMissing == "warn")
            ctx.Warnings.Add(new TransformWarning
            {
                Code = TransformErrorCode.LookupTableNotFound.Code(),
                Message = message,
            });
    }

    private static string FormatMatchKey(DynValue[] keys)
    {
        var parts = new string[keys.Length];
        for (int i = 0; i < keys.Length; i++)
            parts[i] = VerbHelpers.CoerceStr(keys[i]);
        return string.Join(", ", parts);
    }

    /// <summary>
    /// Like lookup, but the last argument is a default value returned when the key is not found.
    /// </summary>
    private static DynValue LookupDefault(DynValue[] args, VerbContext ctx)
    {
        if (args.Length < 3)
            throw new InvalidOperationException("lookupDefault: requires at least 3 arguments (table.column, key..., default)");

        var tableRef = args[0].AsString();
        if (tableRef == null)
            throw new InvalidOperationException("lookupDefault: first argument must be a string table reference");

        var defaultVal = args[args.Length - 1];
        var keys = new DynValue[args.Length - 2];
        Array.Copy(args, 1, keys, 0, keys.Length);

        if (keys.Length == 0)
            throw new InvalidOperationException("lookupDefault: requires at least one lookup key");

        var result = DoTableLookup(tableRef, keys, ctx.Tables);
        return result ?? defaultVal;
    }

    /// <summary>
    /// Performs a table lookup by matching key columns and returning a result column.
    /// Match columns are all columns EXCEPT the return column.
    /// </summary>
    private static DynValue? DoTableLookup(string tableRef, DynValue[] keys, Dictionary<string, LookupTable> tables)
    {
        var dotIdx = tableRef.IndexOf('.');
        if (dotIdx < 0) return null;

        var tableName = tableRef.Substring(0, dotIdx);
        var resultCol = tableRef.Substring(dotIdx + 1);

        if (!tables.TryGetValue(tableName, out var table)) return null;

        // Find result column index
        int resultIdx = -1;
        for (int i = 0; i < table.Columns.Count; i++)
        {
            if (table.Columns[i] == resultCol)
            {
                resultIdx = i;
                break;
            }
        }
        if (resultIdx < 0) return null;

        // Build list of match column indices (all columns except return column)
        var matchColIndices = new List<int>();
        for (int i = 0; i < table.Columns.Count; i++)
        {
            if (i != resultIdx)
                matchColIndices.Add(i);
        }

        int numKeys = keys.Length;

        // Find matching row
        for (int r = 0; r < table.Rows.Count; r++)
        {
            var row = table.Rows[r];
            if (row.Count <= resultIdx) continue;

            bool allMatch = true;
            for (int k = 0; k < numKeys && k < matchColIndices.Count; k++)
            {
                int colIdx = matchColIndices[k];
                if (colIdx >= row.Count)
                {
                    allMatch = false;
                    break;
                }
                if (!DynMatchesKey(row[colIdx], keys[k]))
                {
                    allMatch = false;
                    break;
                }
            }
            if (allMatch && numKeys > 0)
                return row[resultIdx];
        }

        return null;
    }

    /// <summary>
    /// Compares a table cell to a lookup key using cross-type string comparison.
    /// </summary>
    private static bool DynMatchesKey(DynValue cell, DynValue key)
    {
        if (cell.Equals(key)) return true;

        var cellStr = CoerceKeyStr(cell);
        var keyStr = CoerceKeyStr(key);

        if (cellStr == null || keyStr == null) return false;
        return cellStr == keyStr;
    }

    private static string? CoerceKeyStr(DynValue val)
    {
        if (val.IsNull) return null;
        return val.Type switch
        {
            DynValueType.String => val.AsString(),
            DynValueType.Integer => val.AsInt64()?.ToString(CultureInfo.InvariantCulture),
            DynValueType.Float => val.AsDouble()?.ToString(CultureInfo.InvariantCulture),
            DynValueType.Bool => val.AsBool()?.ToString().ToLowerInvariant(),
            _ => null,
        };
    }
}
