#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using Odin.Core.Types;

namespace Odin.Core.Transform
{
    /// <summary>
    /// Parses a CSV string into a <see cref="DynValue"/> array of objects.
    /// Handles quoted fields per RFC 4180 and auto-types values (booleans, integers, floats).
    /// </summary>
    public static class CsvSourceParser
    {
        /// <summary>
        /// Parse a CSV string into a <see cref="DynValue"/>.
        /// </summary>
        /// <param name="input">The CSV string to parse.</param>
        /// <param name="config">Optional source configuration. Supports "delimiter" (default ",")
        /// and "hasHeader" (default "true") options.</param>
        /// <returns>A <see cref="DynValue"/> array of objects (if headers) or array of arrays.</returns>
        /// <exception cref="ArgumentException">Thrown when the input is null or empty.</exception>
        /// <exception cref="FormatException">Thrown when the CSV contains an unterminated quoted field.</exception>
        public static DynValue Parse(string input, SourceConfig? config)
        {
            if (string.IsNullOrEmpty(input))
                return DynValue.Array(new List<DynValue>());

            char delimiter = ',';
            bool hasHeader = true;

            if (config != null)
            {
                if (config.Options.TryGetValue("delimiter", out var delim) && delim.Length > 0)
                    delimiter = delim[0];
                if (config.Options.TryGetValue("hasHeader", out var hdr))
                    hasHeader = hdr != "false";
            }

            var rows = SplitRows(input, delimiter);

            if (rows.Count == 0)
                return DynValue.Array(new List<DynValue>());

            if (hasHeader)
            {
                var headers = rows[0];
                var result = new List<DynValue>();

                for (int r = 1; r < rows.Count; r++)
                {
                    var row = rows[r];
                    var entries = new List<KeyValuePair<string, DynValue>>();
                    for (int c = 0; c < headers.Count; c++)
                    {
                        string val = c < row.Count ? row[c] : "";
                        entries.Add(new KeyValuePair<string, DynValue>(headers[c], InferType(val)));
                    }
                    result.Add(DynValue.Object(entries));
                }

                return DynValue.Array(result);
            }
            else
            {
                var result = new List<DynValue>();
                for (int r = 0; r < rows.Count; r++)
                {
                    var items = new List<DynValue>();
                    for (int c = 0; c < rows[r].Count; c++)
                        items.Add(InferType(rows[r][c]));
                    result.Add(DynValue.Array(items));
                }
                return DynValue.Array(result);
            }
        }

        private static List<List<string>> SplitRows(string input, char delimiter)
        {
            var rows = new List<List<string>>();
            var currentField = new System.Text.StringBuilder();
            var currentRow = new List<string>();
            bool inQuotes = false;
            int i = 0;

            while (i < input.Length)
            {
                char ch = input[i];

                if (inQuotes)
                {
                    if (ch == '"')
                    {
                        // Check for escaped quote
                        if (i + 1 < input.Length && input[i + 1] == '"')
                        {
                            currentField.Append('"');
                            i += 2;
                        }
                        else
                        {
                            inQuotes = false;
                            i++;
                        }
                    }
                    else
                    {
                        currentField.Append(ch);
                        i++;
                    }
                }
                else if (ch == '"')
                {
                    inQuotes = true;
                    i++;
                }
                else if (ch == delimiter)
                {
                    currentRow.Add(currentField.ToString());
                    currentField.Clear();
                    i++;
                }
                else if (ch == '\r')
                {
                    if (i + 1 < input.Length && input[i + 1] == '\n')
                        i++;
                    currentRow.Add(currentField.ToString());
                    currentField.Clear();
                    if (currentRow.Count > 0)
                        rows.Add(new List<string>(currentRow));
                    currentRow.Clear();
                    i++;
                }
                else if (ch == '\n')
                {
                    currentRow.Add(currentField.ToString());
                    currentField.Clear();
                    if (currentRow.Count > 0)
                        rows.Add(new List<string>(currentRow));
                    currentRow.Clear();
                    i++;
                }
                else
                {
                    currentField.Append(ch);
                    i++;
                }
            }

            if (inQuotes)
                throw new FormatException("Unterminated quoted field in CSV.");

            // Flush remaining
            if (currentField.Length > 0 || currentRow.Count > 0)
            {
                currentRow.Add(currentField.ToString());
                rows.Add(currentRow);
            }

            return rows;
        }

        private static DynValue InferType(string s)
        {
            string trimmed = s.Trim();

            if (trimmed.Length == 0)
                return DynValue.String("");

            if (string.Equals(trimmed, "true", StringComparison.OrdinalIgnoreCase))
                return DynValue.Bool(true);
            if (string.Equals(trimmed, "false", StringComparison.OrdinalIgnoreCase))
                return DynValue.Bool(false);
            if (string.Equals(trimmed, "null", StringComparison.OrdinalIgnoreCase))
                return DynValue.Null();

            if (long.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out long intVal))
                return DynValue.Integer(intVal);

            if (double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out double dblVal))
            {
                // Only treat as float if it contains a dot or exponent
                if (trimmed.IndexOf('.') >= 0 || trimmed.IndexOf('e') >= 0 || trimmed.IndexOf('E') >= 0)
                    return DynValue.Float(dblVal);
            }

            return DynValue.String(trimmed);
        }
    }
}
