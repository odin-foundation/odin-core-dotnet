#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using Odin.Core.Types;

namespace Odin.Core.Transform
{
    /// <summary>
    /// Parses fixed-width text into a <see cref="DynValue"/> array of objects.
    /// Column definitions (name, start position, width) come from the source config.
    /// </summary>
    public static class FixedWidthSourceParser
    {
        /// <summary>
        /// Parse fixed-width text into a <see cref="DynValue"/>.
        /// </summary>
        /// <param name="input">The fixed-width text to parse.</param>
        /// <param name="config">Source configuration providing column definitions via Options.
        /// Expected key: "columns" as a semicolon-separated list of "name:start:width" entries
        /// (e.g., "Name:0:20;Amount:20:10"). Start and width are zero-based character positions.</param>
        /// <returns>A <see cref="DynValue"/> array of objects (multiple lines) or a single object (one line).</returns>
        /// <exception cref="ArgumentException">Thrown when column definitions are missing or invalid.</exception>
        public static DynValue Parse(string input, SourceConfig? config)
        {
            if (string.IsNullOrEmpty(input))
                return DynValue.Array(new List<DynValue>());

            var columns = ParseColumns(config);
            if (columns.Count == 0)
                throw new ArgumentException("Fixed-width source config must include column definitions (columns option).");

            var lines = SplitLines(input);
            if (lines.Count == 0)
                return DynValue.Array(new List<DynValue>());

            var records = new List<DynValue>();

            for (int lineIdx = 0; lineIdx < lines.Count; lineIdx++)
            {
                string line = lines[lineIdx];
                var entries = new List<KeyValuePair<string, DynValue>>();

                for (int c = 0; c < columns.Count; c++)
                {
                    var col = columns[c];
                    string value;

                    if (col.Start >= line.Length)
                    {
                        value = "";
                    }
                    else
                    {
                        int end = Math.Min(col.Start + col.Width, line.Length);
                        value = line.Substring(col.Start, end - col.Start).TrimEnd();
                    }

                    entries.Add(new KeyValuePair<string, DynValue>(col.Name, DynValue.String(value)));
                }

                records.Add(DynValue.Object(entries));
            }

            if (records.Count == 1)
                return records[0];

            return DynValue.Array(records);
        }

        private static List<string> SplitLines(string input)
        {
            var lines = new List<string>();
            int start = 0;
            for (int i = 0; i < input.Length; i++)
            {
                if (input[i] == '\n')
                {
                    int end = i > 0 && input[i - 1] == '\r' ? i - 1 : i;
                    string line = input.Substring(start, end - start);
                    if (line.Trim().Length > 0)
                        lines.Add(line);
                    start = i + 1;
                }
            }
            if (start < input.Length)
            {
                string last = input.Substring(start);
                if (last.Trim().Length > 0)
                    lines.Add(last);
            }
            return lines;
        }

        private static List<ColumnDef> ParseColumns(SourceConfig? config)
        {
            var columns = new List<ColumnDef>();
            if (config == null) return columns;

            if (!config.Options.TryGetValue("columns", out var columnsStr) || string.IsNullOrEmpty(columnsStr))
                return columns;

            // Parse semicolon-separated "name:start:width"
            var parts = columnsStr.Split(';');
            for (int i = 0; i < parts.Length; i++)
            {
                var part = parts[i].Trim();
                if (string.IsNullOrEmpty(part)) continue;

                var segments = part.Split(':');
                if (segments.Length < 3) continue;

                string name = segments[0].Trim();
                if (!int.TryParse(segments[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int start))
                    continue;
                if (!int.TryParse(segments[2].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int width))
                    continue;

                columns.Add(new ColumnDef { Name = name, Start = start, Width = width });
            }

            return columns;
        }

        private sealed class ColumnDef
        {
            public string Name { get; set; } = "";
            public int Start { get; set; }
            public int Width { get; set; }
        }
    }
}
