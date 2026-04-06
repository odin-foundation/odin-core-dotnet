#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Odin.Core.Types;

namespace Odin.Core.Transform
{
    /// <summary>
    /// Formats a <see cref="DynValue"/> tree as fixed-width text.
    /// Supports two modes:
    /// 1. Segment-driven: Uses :pos/:len/:leftPad/:rightPad directives from transform segments
    /// 2. Config-driven: Uses "columns" option from target config
    /// </summary>
    public static class FixedWidthFormatter
    {
        /// <summary>
        /// Format fixed-width output using column definitions extracted from transform segments.
        /// Each segment produces one or more lines with fields at absolute positions.
        /// </summary>
        public static string FormatFromSegments(DynValue value, List<TransformSegment> segments, TargetConfig? config)
        {
            if (value == null || segments == null || segments.Count == 0)
                return "";

            // Check if any segment has :pos/:len directives — if so, use segment-driven mode
            bool hasPositionalFields = false;
            for (int s = 0; s < segments.Count; s++)
            {
                if (HasPositionalDirectives(segments[s]))
                {
                    hasPositionalFields = true;
                    break;
                }
            }

            if (!hasPositionalFields)
                return Format(value, config);

            var outputObj = value.AsObject();
            if (outputObj == null) return "";

            var sb = new StringBuilder();

            // Process each segment in order
            for (int s = 0; s < segments.Count; s++)
            {
                var seg = segments[s];
                var segName = seg.Name;
                if (string.IsNullOrEmpty(segName) || segName == "$" || segName == "_root")
                    continue;

                // Strip [] suffix for array segments
                bool isArray = segName.EndsWith("[]", StringComparison.Ordinal);
                var cleanName = isArray ? segName.Substring(0, segName.Length - 2) : segName;

                // Find the data for this segment in the output
                DynValue? segData = null;
                for (int i = 0; i < outputObj.Count; i++)
                {
                    if (outputObj[i].Key == cleanName)
                    {
                        segData = outputObj[i].Value;
                        break;
                    }
                }
                if (segData == null) continue;

                // Collect field definitions from segment mappings
                var fieldDefs = CollectFieldDefs(seg);
                if (fieldDefs.Count == 0) continue;

                if (isArray && segData.Type == DynValueType.Array)
                {
                    var items = segData.AsArray();
                    if (items != null)
                    {
                        for (int i = 0; i < items.Count; i++)
                        {
                            var itemObj = items[i].AsObject();
                            if (itemObj == null) continue;
                            sb.Append(BuildFixedWidthLine(fieldDefs, itemObj));
                            sb.Append('\n');
                        }
                    }
                }
                else if (segData.Type == DynValueType.Object)
                {
                    var obj = segData.AsObject();
                    if (obj != null)
                    {
                        sb.Append(BuildFixedWidthLine(fieldDefs, obj));
                        sb.Append('\n');
                    }
                }
            }

            return sb.ToString();
        }

        private static bool HasPositionalDirectives(TransformSegment segment)
        {
            foreach (var mapping in segment.Mappings)
            {
                for (int d = 0; d < mapping.Directives.Count; d++)
                {
                    if (mapping.Directives[d].Name == "pos" || mapping.Directives[d].Name == "len")
                        return true;
                }
            }
            // Also check items list
            for (int i = 0; i < segment.Items.Count; i++)
            {
                var m = segment.Items[i].AsMapping();
                if (m != null)
                {
                    for (int d = 0; d < m.Directives.Count; d++)
                    {
                        if (m.Directives[d].Name == "pos" || m.Directives[d].Name == "len")
                            return true;
                    }
                }
            }
            return false;
        }

        private sealed class FieldDef
        {
            public string Name = "";
            public int Pos;
            public int Len;
            public string? LeftPad;
            public string? RightPad;
        }

        private static List<FieldDef> CollectFieldDefs(TransformSegment segment)
        {
            var defs = new List<FieldDef>();

            // Collect from Items list (interleaved mappings/children)
            for (int i = 0; i < segment.Items.Count; i++)
            {
                var m = segment.Items[i].AsMapping();
                if (m != null)
                {
                    var def = ExtractFieldDef(m);
                    if (def != null) defs.Add(def);
                }
            }

            // Also collect from Mappings list if Items is empty
            if (defs.Count == 0)
            {
                for (int i = 0; i < segment.Mappings.Count; i++)
                {
                    var def = ExtractFieldDef(segment.Mappings[i]);
                    if (def != null) defs.Add(def);
                }
            }

            // Sort by position
            defs.Sort((a, b) => a.Pos.CompareTo(b.Pos));
            return defs;
        }

        private static FieldDef? ExtractFieldDef(FieldMapping mapping)
        {
            // Skip internal fields
            if (mapping.Target.StartsWith("_", StringComparison.Ordinal))
                return null;

            int pos = -1, len = -1;
            string? leftPad = null, rightPad = null;

            for (int d = 0; d < mapping.Directives.Count; d++)
            {
                var dir = mapping.Directives[d];
                switch (dir.Name)
                {
                    case "pos":
                        pos = (int)(dir.Value?.AsNumber() ?? -1);
                        break;
                    case "len":
                        len = (int)(dir.Value?.AsNumber() ?? -1);
                        break;
                    case "leftPad":
                        leftPad = dir.Value?.AsString();
                        break;
                    case "rightPad":
                        rightPad = dir.Value?.AsString();
                        break;
                }
            }

            if (pos < 0 || len <= 0) return null;

            return new FieldDef
            {
                Name = mapping.Target,
                Pos = pos,
                Len = len,
                LeftPad = leftPad,
                RightPad = rightPad,
            };
        }

        private static string BuildFixedWidthLine(List<FieldDef> fieldDefs, List<KeyValuePair<string, DynValue>> data)
        {
            // Calculate line width from max(pos + len)
            int lineWidth = 0;
            for (int i = 0; i < fieldDefs.Count; i++)
            {
                int end = fieldDefs[i].Pos + fieldDefs[i].Len;
                if (end > lineWidth) lineWidth = end;
            }

            // Create line buffer filled with spaces
            var line = new char[lineWidth];
            for (int i = 0; i < lineWidth; i++) line[i] = ' ';

            // Place each field
            for (int i = 0; i < fieldDefs.Count; i++)
            {
                var def = fieldDefs[i];
                DynValue? fieldVal = FindField(data, def.Name);
                string text = FieldToString(fieldVal);

                // Truncate if too long
                if (text.Length > def.Len)
                    text = text.Substring(0, def.Len);

                // Determine padding
                if (def.LeftPad != null && def.LeftPad.Length > 0)
                {
                    // Left-pad (right-align): pad on left with specified char
                    char padChar = def.LeftPad[0];
                    int padding = def.Len - text.Length;
                    for (int p = 0; p < padding; p++)
                        line[def.Pos + p] = padChar;
                    for (int c = 0; c < text.Length; c++)
                        line[def.Pos + padding + c] = text[c];
                }
                else if (def.RightPad != null && def.RightPad.Length > 0)
                {
                    // Right-pad (left-align): pad on right with specified char
                    char padChar = def.RightPad[0];
                    for (int c = 0; c < text.Length; c++)
                        line[def.Pos + c] = text[c];
                    for (int p = text.Length; p < def.Len; p++)
                        line[def.Pos + p] = padChar;
                }
                else
                {
                    // Default: left-align, pad with spaces
                    for (int c = 0; c < text.Length; c++)
                        line[def.Pos + c] = text[c];
                    // Spaces already in buffer from initialization
                }
            }

            return new string(line);
        }

        private static DynValue? FindField(List<KeyValuePair<string, DynValue>> obj, string key)
        {
            for (int i = 0; i < obj.Count; i++)
            {
                if (obj[i].Key == key) return obj[i].Value;
            }
            return null;
        }

        /// <summary>
        /// Serialize a <see cref="DynValue"/> to a fixed-width text string using config-based columns.
        /// </summary>
        public static string Format(DynValue value, TargetConfig? config)
        {
            if (value == null) return "";

            var columns = ParseColumns(config);
            if (columns.Count == 0) return "";

            // Unwrap single-key objects containing arrays
            DynValue resolved = value;
            var objEntries = value.AsObject();
            if (objEntries != null && objEntries.Count == 1)
            {
                var inner = objEntries[0].Value;
                if (inner.AsArray() != null)
                    resolved = inner;
            }

            // Collect records
            var records = new List<List<KeyValuePair<string, DynValue>>>();
            var arr = resolved.AsArray();
            if (arr != null)
            {
                for (int i = 0; i < arr.Count; i++)
                {
                    var obj = arr[i].AsObject();
                    if (obj != null) records.Add(obj);
                }
            }
            else
            {
                var singleObj = resolved.AsObject();
                if (singleObj != null) records.Add(singleObj);
            }

            if (records.Count == 0) return "";

            var sb = new StringBuilder();

            for (int r = 0; r < records.Count; r++)
            {
                var fields = records[r];

                for (int c = 0; c < columns.Count; c++)
                {
                    var col = columns[c];
                    DynValue? fieldVal = null;

                    for (int f = 0; f < fields.Count; f++)
                    {
                        if (fields[f].Key == col.Name)
                        {
                            fieldVal = fields[f].Value;
                            break;
                        }
                    }

                    string text = FieldToString(fieldVal);
                    bool isNumeric = fieldVal != null && IsNumericType(fieldVal);
                    bool rightAlign = col.Alignment == "right" || (col.Alignment == null && isNumeric);

                    if (text.Length > col.Width)
                        text = text.Substring(0, col.Width);

                    if (rightAlign)
                    {
                        int padding = col.Width - text.Length;
                        for (int p = 0; p < padding; p++) sb.Append(' ');
                        sb.Append(text);
                    }
                    else
                    {
                        sb.Append(text);
                        int padding = col.Width - text.Length;
                        for (int p = 0; p < padding; p++) sb.Append(' ');
                    }
                }

                sb.Append('\n');
            }

            return sb.ToString();
        }

        private static bool IsNumericType(DynValue value)
        {
            return value.Type == DynValueType.Integer
                || value.Type == DynValueType.Float
                || value.Type == DynValueType.Currency
                || value.Type == DynValueType.Percent
                || value.Type == DynValueType.FloatRaw
                || value.Type == DynValueType.CurrencyRaw;
        }

        private static string FieldToString(DynValue? value)
        {
            if (value == null) return "";

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
                case DynValueType.Currency:
                case DynValueType.Percent:
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
                    return value.AsString() ?? "";
            }
        }

        private static List<ColumnDef> ParseColumns(TargetConfig? config)
        {
            var columns = new List<ColumnDef>();
            if (config == null) return columns;

            if (!config.Options.TryGetValue("columns", out var columnsStr) || string.IsNullOrEmpty(columnsStr))
                return columns;

            var parts = columnsStr.Split(';');
            for (int i = 0; i < parts.Length; i++)
            {
                var part = parts[i].Trim();
                if (string.IsNullOrEmpty(part)) continue;

                var segs = part.Split(':');
                if (segs.Length < 2) continue;

                string name = segs[0].Trim();
                if (!int.TryParse(segs[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int width))
                    continue;

                string? alignment = null;
                if (segs.Length >= 3)
                    alignment = segs[2].Trim();

                columns.Add(new ColumnDef { Name = name, Width = width, Alignment = alignment });
            }

            return columns;
        }

        private sealed class ColumnDef
        {
            public string Name { get; set; } = "";
            public int Width { get; set; }
            public string? Alignment { get; set; }
        }
    }
}
