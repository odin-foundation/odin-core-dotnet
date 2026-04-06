#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Odin.Core.Types;

namespace Odin.Core.Transform
{
    /// <summary>
    /// Formats a <see cref="DynValue"/> tree as a CSV string.
    /// Expects the input to be an array of objects (or a single-key object wrapping an array).
    /// </summary>
    public static class CsvFormatter
    {
        /// <summary>
        /// Serialize a <see cref="DynValue"/> to a CSV string.
        /// </summary>
        /// <param name="value">The value to serialize. Must be an array of objects.</param>
        /// <param name="config">Optional target configuration. Supports "delimiter" (default ","),
        /// "includeHeader" (default "true"), and "quoteChar" (default '"') options.</param>
        /// <returns>A CSV-formatted string.</returns>
        public static string Format(DynValue value, TargetConfig? config)
        {
            if (value == null) return "";

            string delimiter = ",";
            bool includeHeader = true;
            char quoteChar = '"';

            if (config != null)
            {
                if (config.Options.TryGetValue("delimiter", out var delim) && !string.IsNullOrEmpty(delim))
                    delimiter = delim;
                if (config.Options.TryGetValue("includeHeader", out var hdr))
                    includeHeader = hdr != "false";
                else if (config.Options.TryGetValue("header", out var hdr2))
                    includeHeader = hdr2 != "false";
                if (config.Options.TryGetValue("quoteChar", out var qc) && !string.IsNullOrEmpty(qc))
                    quoteChar = qc[0];
            }

            // Unwrap single-key objects containing arrays
            DynValue resolved = value;
            var objEntries = value.AsObject();
            if (objEntries != null && objEntries.Count == 1)
            {
                var inner = objEntries[0].Value;
                if (inner.AsArray() != null)
                    resolved = inner;
            }

            var rows = resolved.AsArray();
            if (rows == null || rows.Count == 0) return "";

            var sb = new StringBuilder();

            // Get headers from first object
            var firstObj = rows[0].AsObject();
            if (firstObj == null) return "";

            var headers = new List<string>();
            for (int i = 0; i < firstObj.Count; i++)
                headers.Add(firstObj[i].Key);

            // Write header row
            if (includeHeader)
            {
                for (int i = 0; i < headers.Count; i++)
                {
                    if (i > 0) sb.Append(delimiter);
                    sb.Append(headers[i]);
                }
                sb.Append('\n');
            }

            // Write data rows
            for (int r = 0; r < rows.Count; r++)
            {
                var rowObj = rows[r].AsObject();
                if (rowObj == null) continue;

                for (int c = 0; c < rowObj.Count; c++)
                {
                    if (c > 0) sb.Append(delimiter);
                    sb.Append(FormatCsvValue(rowObj[c].Value, delimiter, quoteChar));
                }
                sb.Append('\n');
            }

            return sb.ToString();
        }

        private static string FormatCsvValue(DynValue value, string delimiter, char quoteChar)
        {
            switch (value.Type)
            {
                case DynValueType.Null:
                case DynValueType.Array:
                case DynValueType.Object:
                    return "";

                case DynValueType.Bool:
                    return (value.AsBool() ?? false) ? "true" : "false";

                case DynValueType.Integer:
                    return (value.AsInt64() ?? 0).ToString(CultureInfo.InvariantCulture);

                case DynValueType.Float:
                case DynValueType.Percent:
                {
                    double d = value.AsDouble() ?? 0.0;
                    if (d == Math.Floor(d) && !double.IsInfinity(d) && Math.Abs(d) < 1e15)
                        return ((long)d).ToString(CultureInfo.InvariantCulture);
                    return d.ToString("G", CultureInfo.InvariantCulture);
                }

                case DynValueType.Currency:
                {
                    double d = value.AsDouble() ?? 0.0;
                    if (d == Math.Floor(d) && !double.IsInfinity(d) && Math.Abs(d) < 1e15)
                        return ((long)d).ToString(CultureInfo.InvariantCulture);
                    return d.ToString("G", CultureInfo.InvariantCulture);
                }

                case DynValueType.FloatRaw:
                case DynValueType.CurrencyRaw:
                    return value.AsString() ?? "";

                default:
                {
                    string s = value.AsString() ?? "";
                    bool needsQuoting = s.IndexOf(delimiter, StringComparison.Ordinal) >= 0
                        || s.IndexOf(quoteChar) >= 0
                        || s.IndexOf('\n') >= 0;
                    if (needsQuoting)
                    {
                        return quoteChar + s.Replace(
                            quoteChar.ToString(),
                            new string(quoteChar, 2)) + quoteChar;
                    }
                    return s;
                }
            }
        }
    }
}
