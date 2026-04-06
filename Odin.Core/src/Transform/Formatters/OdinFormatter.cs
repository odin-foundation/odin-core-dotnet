#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Odin.Core.Types;

namespace Odin.Core.Transform
{
    /// <summary>
    /// Formats a <see cref="DynValue"/> tree as ODIN text notation.
    /// Uses ODIN type prefixes: # for numbers, ## for integers, #$ for currency,
    /// ? for booleans, ~ for null, @ for references, ^ for binary.
    /// Top-level object keys become {SectionName} headers.
    /// </summary>
    public static class OdinFormatter
    {
        /// <summary>
        /// Serialize a <see cref="DynValue"/> to ODIN text format with modifier prefixes.
        /// </summary>
        /// <param name="value">The value to serialize.</param>
        /// <param name="config">Optional target configuration.</param>
        /// <param name="modifiers">Field modifier map keyed by dotted path.</param>
        /// <returns>An ODIN-formatted string with modifier prefixes.</returns>
        public static string FormatWithModifiers(DynValue value, TargetConfig? config,
            Dictionary<string, OdinModifiers> modifiers)
        {
            return FormatImpl(value, config, modifiers);
        }

        /// <summary>
        /// Serialize a <see cref="DynValue"/> to ODIN text format.
        /// </summary>
        /// <param name="value">The value to serialize.</param>
        /// <param name="config">Optional target configuration. Supports "includeHeader" (default "true").</param>
        /// <returns>An ODIN-formatted string.</returns>
        public static string Format(DynValue value, TargetConfig? config)
        {
            return FormatImpl(value, config, null);
        }

        private static string FormatImpl(DynValue value, TargetConfig? config,
            Dictionary<string, OdinModifiers>? modifiers)
        {
            if (value == null) return "";

            bool includeHeader = true;
            if (config != null && config.Options.TryGetValue("header", out var hdr))
                includeHeader = hdr == "true";
            else if (config != null && config.Options.TryGetValue("includeHeader", out var hdr2))
                includeHeader = hdr2 != "false";

            var sb = new StringBuilder();

            if (includeHeader)
                sb.Append("{$}\nodin = \"1.0.0\"\n");

            var entries = value.AsObject();
            if (entries != null)
            {
                bool hasSections = false;
                for (int i = 0; i < entries.Count; i++)
                {
                    if (entries[i].Value.Type == DynValueType.Object || entries[i].Value.Type == DynValueType.Array)
                    {
                        hasSections = true;
                        break;
                    }
                }

                if (hasSections)
                {
                    if (includeHeader)
                        sb.Append("{}\n");

                    // First pass: flat top-level fields and leaf chains
                    for (int i = 0; i < entries.Count; i++)
                    {
                        var key = entries[i].Key;
                        var val = entries[i].Value;

                        if (val.Type == DynValueType.Object)
                            CollectLeafPaths(sb, key, val, key, modifiers);
                        else if (val.Type != DynValueType.Array)
                            WriteAssignment(sb, key, val, key, modifiers);
                    }

                    // Second pass: proper sections (non-leaf-chain objects and arrays)
                    string lastCtx = "";
                    for (int i = 0; i < entries.Count; i++)
                    {
                        var key = entries[i].Key;
                        var val = entries[i].Value;

                        if (val.Type == DynValueType.Object && !IsPureLeafChain(val))
                        {
                            WriteSection(sb, key, key, null, val, modifiers, ref lastCtx);
                        }
                        else if (val.Type == DynValueType.Array)
                        {
                            WriteArraySectionSmart(sb, key, null, val.AsArray()!, modifiers);
                        }
                    }
                }
                else
                {
                    for (int i = 0; i < entries.Count; i++)
                        WriteAssignment(sb, entries[i].Key, entries[i].Value, entries[i].Key, modifiers);
                }
            }
            else if (value.Type == DynValueType.Array)
            {
                var items = value.AsArray();
                if (items != null)
                {
                    for (int i = 0; i < items.Count; i++)
                    {
                        if (i > 0) sb.Append('\n');
                        sb.Append("{item}\n");
                        WriteFieldsSimple(sb, items[i]);
                    }
                }
            }
            else
            {
                sb.Append(ValueToOdinString(value));
                sb.Append('\n');
            }

            return sb.ToString();
        }

        // ─────────────────────────────────────────────────────────────────────
        // Section writing with relative path support
        // ─────────────────────────────────────────────────────────────────────

        private static void WriteSection(StringBuilder sb, string fullPath, string displayPath,
            string? parentSection, DynValue val, Dictionary<string, OdinModifiers>? modifiers,
            ref string lastEmittedContext, bool insideRelative = false)
        {
            var entries = val.AsObject();
            if (entries == null) return;

            bool isRelative = displayPath.Length > 0 && displayPath[0] == '.';
            sb.Append('{');
            sb.Append(displayPath);
            sb.Append("}\n");

            lastEmittedContext = fullPath;

            // Pass 1: scalar assignments and pure leaf chains
            for (int i = 0; i < entries.Count; i++)
            {
                var child = entries[i].Value;
                string childFullPath = fullPath + "." + entries[i].Key;
                if (child.Type == DynValueType.Object && IsPureLeafChain(child))
                {
                    CollectLeafPathsInner(sb, entries[i].Key, child, childFullPath, modifiers);
                }
                else if (child.Type != DynValueType.Object && child.Type != DynValueType.Array)
                {
                    WriteAssignment(sb, entries[i].Key, child, childFullPath, modifiers);
                }
            }

            // Pass 2: array sections
            for (int i = 0; i < entries.Count; i++)
            {
                var child = entries[i].Value;
                if (child.Type == DynValueType.Array)
                {
                    string arrParent = lastEmittedContext == fullPath ? fullPath : null!;
                    WriteArraySectionSmart(sb, entries[i].Key, arrParent, child.AsArray()!, modifiers);
                    lastEmittedContext = fullPath;
                }
            }

            // Pass 3: object subsections (non-leaf-chain)
            // Children of relative sections must use absolute paths (no nested relative)
            for (int i = 0; i < entries.Count; i++)
            {
                var child = entries[i].Value;
                if (child.Type == DynValueType.Object && !IsPureLeafChain(child))
                {
                    string childFullPath = fullPath + "." + entries[i].Key;
                    string childDisplay;
                    if (!isRelative && !insideRelative && lastEmittedContext == fullPath)
                        childDisplay = "." + entries[i].Key;
                    else
                        childDisplay = childFullPath;
                    WriteSection(sb, childFullPath, childDisplay, fullPath, child, modifiers, ref lastEmittedContext, isRelative || insideRelative);
                    // Reset context to parent so next sibling can also use relative notation
                    lastEmittedContext = fullPath;
                }
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Smart array sections: tabular or value-only
        // ─────────────────────────────────────────────────────────────────────

        private static void WriteArraySectionSmart(StringBuilder sb, string name,
            string? parentSection, List<DynValue> items,
            Dictionary<string, OdinModifiers>? modifiers)
        {
            if (items.Count == 0)
            {
                // Empty array — write as value array with placeholder
                string prefix = parentSection != null ? "." : "";
                sb.Append('{');
                sb.Append(prefix);
                sb.Append(name);
                sb.Append("[] : ~}\n");
                sb.Append("~\n");
                return;
            }

            // Check if all items are scalars (value-only array)
            bool allScalar = true;
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].Type == DynValueType.Object || items[i].Type == DynValueType.Array)
                {
                    allScalar = false;
                    break;
                }
            }

            if (allScalar)
            {
                // Value-only array: {.name[] : ~}
                string prefix = parentSection != null ? "." : "";
                sb.Append('{');
                sb.Append(prefix);
                sb.Append(name);
                sb.Append("[] : ~}\n");
                for (int i = 0; i < items.Count; i++)
                {
                    sb.Append(ValueToOdinString(items[i]));
                    sb.Append('\n');
                }
                return;
            }

            // Check if all items are objects with consistent keys (tabular array)
            var columns = GetConsistentColumns(items);
            if (columns != null && columns.Count > 0)
            {
                // Tabular array: {name[] : col1, col2}
                sb.Append('{');
                if (parentSection != null)
                    sb.Append('.');
                sb.Append(name);
                sb.Append("[] : ");
                sb.Append(FormatColumnsWithRelative(columns));
                sb.Append("}\n");

                for (int i = 0; i < items.Count; i++)
                {
                    var obj = items[i].AsObject();
                    if (obj == null) continue;
                    for (int c = 0; c < columns.Count; c++)
                    {
                        if (c > 0) sb.Append(", ");
                        DynValue? fieldVal = FindField(obj, columns[c]);
                        if (fieldVal != null)
                            sb.Append(ValueToOdinString(fieldVal));
                        // else: field not present in this item — leave empty
                    }
                    sb.Append('\n');
                }
                return;
            }

            // Fallback: standard array section with {---} separators
            sb.Append('{');
            sb.Append(name);
            sb.Append("[]}\n");
            for (int i = 0; i < items.Count; i++)
            {
                if (i > 0) sb.Append("{---}\n");
                WriteFieldsSimple(sb, items[i]);
            }
        }

        private static List<string>? GetConsistentColumns(List<DynValue> items)
        {
            // Collect flattened columns from all items (union of all fields)
            var allColumns = new List<string>();
            var columnSet = new HashSet<string>();

            for (int i = 0; i < items.Count; i++)
            {
                var obj = items[i].AsObject();
                if (obj == null) return null;

                var itemCols = new List<string>();
                if (!CollectFlatColumns(obj, "", itemCols))
                    return null;

                for (int j = 0; j < itemCols.Count; j++)
                {
                    if (columnSet.Add(itemCols[j]))
                        allColumns.Add(itemCols[j]);
                }
            }

            return allColumns.Count > 0 ? allColumns : null;
        }

        private static bool CollectFlatColumns(List<KeyValuePair<string, DynValue>> obj, string prefix, List<string> columns)
        {
            for (int i = 0; i < obj.Count; i++)
            {
                string colName = prefix.Length > 0 ? prefix + "." + obj[i].Key : obj[i].Key;
                var val = obj[i].Value;

                if (val.Type == DynValueType.Object)
                {
                    // Single-level nesting only
                    var nested = val.AsObject();
                    if (nested == null) return false;
                    if (prefix.Length > 0) return false; // No multi-level nesting in tabular
                    if (!CollectFlatColumns(nested, obj[i].Key, columns))
                        return false;
                }
                else if (val.Type == DynValueType.Array)
                {
                    return false; // Arrays not supported in tabular columns
                }
                else
                {
                    columns.Add(colName);
                }
            }
            return true;
        }

        private static string FormatColumnsWithRelative(List<string> columns)
        {
            var sb = new StringBuilder();
            string currentParent = "";

            for (int i = 0; i < columns.Count; i++)
            {
                if (i > 0) sb.Append(", ");

                int dotIdx = columns[i].IndexOf('.');
                if (dotIdx > 0)
                {
                    string parent = columns[i].Substring(0, dotIdx);
                    string field = columns[i].Substring(dotIdx + 1);
                    if (parent == currentParent)
                    {
                        sb.Append('.').Append(field);
                    }
                    else
                    {
                        sb.Append(columns[i]);
                        currentParent = parent;
                    }
                }
                else
                {
                    sb.Append(columns[i]);
                    currentParent = "";
                }
            }

            return sb.ToString();
        }

        private static DynValue? FindField(List<KeyValuePair<string, DynValue>> obj, string key)
        {
            // Support dotted paths for nested object access
            int dotIdx = key.IndexOf('.');
            if (dotIdx > 0)
            {
                string parent = key.Substring(0, dotIdx);
                string child = key.Substring(dotIdx + 1);
                for (int i = 0; i < obj.Count; i++)
                {
                    if (obj[i].Key == parent && obj[i].Value.Type == DynValueType.Object)
                    {
                        var nested = obj[i].Value.AsObject();
                        if (nested != null)
                            return FindField(nested, child);
                    }
                }
                return null;
            }

            for (int i = 0; i < obj.Count; i++)
            {
                if (obj[i].Key == key) return obj[i].Value;
            }
            return null;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Leaf chain detection and collection
        // ─────────────────────────────────────────────────────────────────────

        private static bool IsPureLeafChain(DynValue val)
        {
            var entries = val.AsObject();
            if (entries == null || entries.Count != 1) return false;

            var child = entries[0].Value;
            if (child.Type == DynValueType.Object) return IsPureLeafChain(child);
            if (child.Type == DynValueType.Array) return false;
            return true; // scalar leaf
        }

        private static void CollectLeafPaths(StringBuilder sb, string prefix, DynValue val,
            string modPath, Dictionary<string, OdinModifiers>? modifiers)
        {
            if (!IsPureLeafChain(val)) return;
            CollectLeafPathsInner(sb, prefix, val, modPath, modifiers);
        }

        private static void CollectLeafPathsInner(StringBuilder sb, string prefix, DynValue val,
            string modPath, Dictionary<string, OdinModifiers>? modifiers)
        {
            var entries = val.AsObject();
            if (entries != null)
            {
                for (int i = 0; i < entries.Count; i++)
                {
                    string path = prefix + "." + entries[i].Key;
                    string mp = modPath + "." + entries[i].Key;
                    if (entries[i].Value.Type == DynValueType.Object)
                        CollectLeafPathsInner(sb, path, entries[i].Value, mp, modifiers);
                    else
                        WriteAssignment(sb, path, entries[i].Value, mp, modifiers);
                }
            }
            else
            {
                WriteAssignment(sb, prefix, val, modPath, modifiers);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Assignment writing
        // ─────────────────────────────────────────────────────────────────────

        private static string ModifierPrefix(string path, Dictionary<string, OdinModifiers>? modifiers)
        {
            if (modifiers == null || !modifiers.TryGetValue(path, out var mods))
                return "";

            var sb = new StringBuilder();
            if (mods.Required) sb.Append('!');
            if (mods.Deprecated) sb.Append('-');
            if (mods.Confidential) sb.Append('*');
            return sb.ToString();
        }

        private static void WriteAssignment(StringBuilder sb, string key, DynValue value,
            string fullPath, Dictionary<string, OdinModifiers>? modifiers)
        {
            sb.Append(key);
            sb.Append(" = ");
            string prefix = ModifierPrefix(fullPath, modifiers);
            sb.Append(prefix);
            sb.Append(ValueToOdinString(value));
            sb.Append('\n');
        }

        private static void WriteFieldsSimple(StringBuilder sb, DynValue value)
        {
            var entries = value.AsObject();
            if (entries == null) return;

            for (int i = 0; i < entries.Count; i++)
            {
                sb.Append(entries[i].Key);
                sb.Append(" = ");
                sb.Append(ValueToOdinString(entries[i].Value));
                sb.Append('\n');
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Value serialization
        // ─────────────────────────────────────────────────────────────────────

        private static string ValueToOdinString(DynValue value)
        {
            switch (value.Type)
            {
                case DynValueType.Null:
                    return "~";

                case DynValueType.Bool:
                    return (value.AsBool() ?? false) ? "?true" : "?false";

                case DynValueType.Integer:
                    return "##" + (value.AsInt64() ?? 0).ToString(CultureInfo.InvariantCulture);

                case DynValueType.Float:
                {
                    double n = value.AsDouble() ?? 0.0;
                    if (!double.IsInfinity(n) && !double.IsNaN(n))
                    {
                        if (n == Math.Floor(n) && Math.Abs(n) < 1e15)
                            return "#" + ((long)n).ToString(CultureInfo.InvariantCulture);
                        // Use lowercase 'e' for scientific notation to match TypeScript/Rust
                        string s = n.ToString("G", CultureInfo.InvariantCulture);
                        return "#" + s.Replace("E+", "e+").Replace("E-", "e-");
                    }
                    return "~";
                }

                case DynValueType.FloatRaw:
                    return "#" + (value.AsString() ?? "0");

                case DynValueType.Currency:
                {
                    double n = value.AsDouble() ?? 0.0;
                    if (!double.IsInfinity(n) && !double.IsNaN(n))
                    {
                        int dp = value.GetDecimalPlaces();
                        string formatted = n.ToString("F" + dp.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
                        string? code = value.GetCurrencyCode();
                        return code != null ? "#$" + formatted + ":" + code : "#$" + formatted;
                    }
                    return "~";
                }

                case DynValueType.CurrencyRaw:
                {
                    string raw = value.AsString() ?? "0";
                    string? code = value.GetCurrencyCode();
                    return code != null ? "#$" + raw + ":" + code : "#$" + raw;
                }

                case DynValueType.Percent:
                {
                    double n = value.AsDouble() ?? 0.0;
                    if (!double.IsInfinity(n) && !double.IsNaN(n))
                    {
                        string s = n.ToString("G", CultureInfo.InvariantCulture);
                        return "#%" + s.Replace("E+", "e+").Replace("E-", "e-");
                    }
                    return "~";
                }

                case DynValueType.Reference:
                    return "@" + (value.AsString() ?? "");

                case DynValueType.Binary:
                    return "^" + (value.AsString() ?? "");

                case DynValueType.Date:
                case DynValueType.Duration:
                    return value.AsString() ?? "";

                case DynValueType.Timestamp:
                    return value.AsString() ?? "";

                case DynValueType.Time:
                {
                    string t = value.AsString() ?? "";
                    if (t.Length > 0 && t[0] != 'T') return "T" + t;
                    return t;
                }

                case DynValueType.String:
                    return "\"" + EscapeOdinString(value.AsString() ?? "") + "\"";

                case DynValueType.Array:
                case DynValueType.Object:
                    return "~";

                default:
                    return "~";
            }
        }

        private static string EscapeOdinString(string s)
        {
            var sb = new StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default: sb.Append(c); break;
                }
            }
            return sb.ToString();
        }
    }
}
