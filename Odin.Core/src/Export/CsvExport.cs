using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Odin.Core.Types;

namespace Odin.Core.Export;

/// <summary>Options for CSV export from an OdinDocument.</summary>
public sealed class CsvExportOptions
{
    /// <summary>Dot-separated path to the array to export. If null, auto-detects the first array.</summary>
    public string? ArrayPath { get; init; }

    /// <summary>Field delimiter character (default: comma).</summary>
    public char Delimiter { get; init; } = ',';

    /// <summary>Whether to include a header row (default: true).</summary>
    public bool Header { get; init; } = true;
}

/// <summary>Exports an OdinDocument to CSV format.</summary>
public static class CsvExport
{
    private static readonly Regex ArrayIndexPattern = new Regex(@"^(.+?)\[(\d+)\]\.(.+)$", RegexOptions.Compiled);

    /// <summary>Convert an OdinDocument to a CSV string.</summary>
    public static string ToCsv(OdinDocument doc, CsvExportOptions? options = null)
    {
        options ??= new CsvExportOptions();
        var delimiter = options.Delimiter.ToString();

        // Collect array rows from assignments
        var rows = CollectRows(doc, options.ArrayPath);
        if (rows.Count == 0)
        {
            // Single-row fallback: treat all top-level fields as one row
            return SingleRowFallback(doc, options);
        }

        // Collect column names across all rows preserving order
        var columns = new List<string>();
        var columnSet = new HashSet<string>();
        foreach (var row in rows)
        {
            foreach (var kvp in row)
            {
                if (columnSet.Add(kvp.Key))
                    columns.Add(kvp.Key);
            }
        }

        var sb = new StringBuilder();

        // Header row
        if (options.Header)
        {
            for (int i = 0; i < columns.Count; i++)
            {
                if (i > 0) sb.Append(delimiter);
                sb.Append(EscapeCsv(columns[i], delimiter, '"'));
            }
            sb.Append('\n');
        }

        // Data rows
        foreach (var row in rows)
        {
            for (int i = 0; i < columns.Count; i++)
            {
                if (i > 0) sb.Append(delimiter);
                if (row.TryGetValue(columns[i], out var value))
                    sb.Append(EscapeCsv(FormatValue(value), delimiter, '"'));
            }
            sb.Append('\n');
        }

        return sb.ToString();
    }

    private static List<Dictionary<string, OdinValue>> CollectRows(OdinDocument doc, string? arrayPath)
    {
        var rows = new Dictionary<int, Dictionary<string, OdinValue>>();
        string? detectedPrefix = arrayPath;

        foreach (var entry in doc.Assignments)
        {
            var match = ArrayIndexPattern.Match(entry.Key);
            if (!match.Success) continue;

            string prefix = match.Groups[1].Value;
            int index = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
            string field = match.Groups[3].Value;

            if (detectedPrefix == null)
                detectedPrefix = prefix;

            if (prefix != detectedPrefix) continue;

            if (!rows.TryGetValue(index, out var row))
            {
                row = new Dictionary<string, OdinValue>();
                rows[index] = row;
            }
            row[field] = entry.Value;
        }

        // Sort by index
        var sortedIndices = new List<int>(rows.Keys);
        sortedIndices.Sort();

        var result = new List<Dictionary<string, OdinValue>>();
        foreach (var idx in sortedIndices)
            result.Add(rows[idx]);

        return result;
    }

    private static string SingleRowFallback(OdinDocument doc, CsvExportOptions options)
    {
        var delimiter = options.Delimiter.ToString();
        var columns = new List<string>();
        var values = new List<string>();

        foreach (var entry in doc.Assignments)
        {
            // Skip metadata
            if (entry.Key.StartsWith("$", StringComparison.Ordinal)) continue;

            // Only include simple (non-sectioned) fields for single-row
            if (entry.Key.Contains("[")) continue;

            columns.Add(entry.Key);
            values.Add(FormatValue(entry.Value));
        }

        if (columns.Count == 0) return "";

        var sb = new StringBuilder();

        if (options.Header)
        {
            for (int i = 0; i < columns.Count; i++)
            {
                if (i > 0) sb.Append(delimiter);
                sb.Append(EscapeCsv(columns[i], delimiter, '"'));
            }
            sb.Append('\n');
        }

        for (int i = 0; i < values.Count; i++)
        {
            if (i > 0) sb.Append(delimiter);
            sb.Append(EscapeCsv(values[i], delimiter, '"'));
        }
        sb.Append('\n');

        return sb.ToString();
    }

    private static string FormatValue(OdinValue value) => value switch
    {
        OdinNull => "",
        OdinBoolean b => b.Value ? "true" : "false",
        OdinString s => s.Value,
        OdinInteger i => i.Value.ToString(CultureInfo.InvariantCulture),
        OdinNumber n => FormatNumber(n.Value),
        OdinCurrency c => FormatNumber(c.Value),
        OdinPercent p => FormatNumber(p.Value),
        OdinDate d => d.Raw,
        OdinTimestamp ts => ts.Raw,
        OdinTime t => t.Value,
        OdinDuration d => d.Value,
        OdinReference r => "@" + r.Path,
        OdinBinary b => b.Algorithm != null
            ? "^" + b.Algorithm + ":" + Convert.ToBase64String(b.Data)
            : "^" + Convert.ToBase64String(b.Data),
        _ => value.ToString() ?? "",
    };

    private static string FormatNumber(double d)
    {
        if (d == Math.Floor(d) && !double.IsInfinity(d) && Math.Abs(d) < 1e15)
            return ((long)d).ToString(CultureInfo.InvariantCulture);
        return d.ToString("G", CultureInfo.InvariantCulture);
    }

    private static string EscapeCsv(string value, string delimiter, char quoteChar)
    {
        bool needsQuoting = value.IndexOf(delimiter, StringComparison.Ordinal) >= 0
            || value.IndexOf(quoteChar) >= 0
            || value.IndexOf('\n') >= 0
            || value.IndexOf('\r') >= 0;

        if (needsQuoting)
            return quoteChar + value.Replace(quoteChar.ToString(), new string(quoteChar, 2)) + quoteChar;

        return value;
    }
}
