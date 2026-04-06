using System;
using System.Collections.Generic;
using System.Globalization;
using Odin.Core.Types;

namespace Odin.Core.Export;

/// <summary>Alignment for fixed-width fields.</summary>
public enum FixedWidthAlign
{
    /// <summary>Left-align the value (pad on the right).</summary>
    Left,

    /// <summary>Right-align the value (pad on the left).</summary>
    Right,
}

/// <summary>Defines a single field in a fixed-width record.</summary>
public sealed class FixedWidthField
{
    /// <summary>Dot-separated path to the value in the OdinDocument.</summary>
    public string Path { get; init; } = "";

    /// <summary>Zero-based character position in the output line.</summary>
    public int Pos { get; init; }

    /// <summary>Maximum character length for this field.</summary>
    public int Len { get; init; }

    /// <summary>Per-field pad character override (null uses the global default).</summary>
    public char? PadChar { get; init; }

    /// <summary>Field alignment (default: Left).</summary>
    public FixedWidthAlign Align { get; init; } = FixedWidthAlign.Left;
}

/// <summary>Options for fixed-width export from an OdinDocument.</summary>
public sealed class FixedWidthExportOptions
{
    /// <summary>Total line width in characters.</summary>
    public int LineWidth { get; init; }

    /// <summary>Field definitions.</summary>
    public IReadOnlyList<FixedWidthField> Fields { get; init; } = Array.Empty<FixedWidthField>();

    /// <summary>Default pad character (default: space).</summary>
    public char PadChar { get; init; } = ' ';
}

/// <summary>Exports an OdinDocument to fixed-width format.</summary>
public static class FixedWidthExport
{
    /// <summary>Convert an OdinDocument to a fixed-width string.</summary>
    public static string ToFixedWidth(OdinDocument doc, FixedWidthExportOptions options)
    {
        if (options.Fields.Count == 0 || options.LineWidth <= 0)
            return "";

        // Build the line buffer
        var line = new char[options.LineWidth];
        for (int i = 0; i < options.LineWidth; i++)
            line[i] = options.PadChar;

        // Place each field
        foreach (var field in options.Fields)
        {
            string value = GetValueAtPath(doc, field.Path);

            // Truncate to field length
            if (value.Length > field.Len)
                value = value.Substring(0, field.Len);

            char padChar = field.PadChar ?? options.PadChar;

            // Fill field region with pad char first
            int end = Math.Min(field.Pos + field.Len, options.LineWidth);
            for (int i = field.Pos; i < end; i++)
                line[i] = padChar;

            if (field.Align == FixedWidthAlign.Right)
            {
                // Right-align: text at end of field
                int startPos = field.Pos + field.Len - value.Length;
                for (int i = 0; i < value.Length && startPos + i < options.LineWidth; i++)
                    line[startPos + i] = value[i];
            }
            else
            {
                // Left-align: text at start of field
                for (int i = 0; i < value.Length && field.Pos + i < options.LineWidth; i++)
                    line[field.Pos + i] = value[i];
            }
        }

        return new string(line);
    }

    private static string GetValueAtPath(OdinDocument doc, string path)
    {
        // Try exact match first
        if (doc.Assignments.TryGetValue(path, out var value))
            return FormatValue(value);

        return "";
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
}
