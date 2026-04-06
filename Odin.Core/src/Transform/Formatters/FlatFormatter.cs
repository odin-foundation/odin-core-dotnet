#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Odin.Core.Types;

namespace Odin.Core.Transform
{
    /// <summary>
    /// Formats a <see cref="DynValue"/> tree as flat output.
    /// Supports multiple styles:
    /// - 'kvp' (default): Simple key=value pairs with dot notation
    /// - 'yaml': YAML format with indentation
    /// </summary>
    public static class FlatFormatter
    {
        /// <summary>
        /// Serialize a <see cref="DynValue"/> to flat text.
        /// </summary>
        /// <param name="value">The value to serialize.</param>
        /// <param name="config">Optional target configuration. Supports "style" option ("kvp" or "yaml").</param>
        /// <returns>A formatted string.</returns>
        public static string Format(DynValue value, TargetConfig? config)
        {
            if (value == null) return "";

            string style = "kvp";
            if (config != null && config.Options.TryGetValue("style", out var s) && !string.IsNullOrEmpty(s))
                style = s;

            switch (style.ToLowerInvariant())
            {
                case "yaml":
                    return FormatYaml(value);
                case "kvp":
                default:
                    return FormatKvp(value);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // KVP style
        // ─────────────────────────────────────────────────────────────────────

        private static string FormatKvp(DynValue value)
        {
            var pairs = new List<KeyValuePair<string, string>>();
            CollectPairs(pairs, value, "");
            pairs.Sort((a, b) => string.Compare(a.Key, b.Key, StringComparison.Ordinal));

            var sb = new StringBuilder();
            for (int i = 0; i < pairs.Count; i++)
            {
                sb.Append(pairs[i].Key);
                sb.Append('=');
                sb.Append(pairs[i].Value);
                sb.Append('\n');
            }

            return sb.ToString();
        }

        private static void CollectPairs(List<KeyValuePair<string, string>> pairs, DynValue value, string prefix)
        {
            switch (value.Type)
            {
                case DynValueType.Null:
                    break;

                case DynValueType.Bool:
                    pairs.Add(new KeyValuePair<string, string>(prefix, (value.AsBool() ?? false) ? "true" : "false"));
                    break;

                case DynValueType.Integer:
                    pairs.Add(new KeyValuePair<string, string>(prefix, (value.AsInt64() ?? 0).ToString(CultureInfo.InvariantCulture)));
                    break;

                case DynValueType.Float:
                case DynValueType.Currency:
                case DynValueType.Percent:
                {
                    double d = value.AsDouble() ?? 0.0;
                    string formatted;
                    if (d == Math.Floor(d) && !double.IsInfinity(d) && Math.Abs(d) < 1e15)
                        formatted = ((long)d).ToString(CultureInfo.InvariantCulture);
                    else
                        formatted = d.ToString("G", CultureInfo.InvariantCulture);
                    pairs.Add(new KeyValuePair<string, string>(prefix, formatted));
                    break;
                }

                case DynValueType.FloatRaw:
                case DynValueType.CurrencyRaw:
                case DynValueType.String:
                case DynValueType.Reference:
                case DynValueType.Binary:
                case DynValueType.Date:
                case DynValueType.Timestamp:
                case DynValueType.Time:
                case DynValueType.Duration:
                    pairs.Add(new KeyValuePair<string, string>(prefix, value.AsString() ?? ""));
                    break;

                case DynValueType.Array:
                {
                    var items = value.AsArray();
                    if (items != null)
                    {
                        for (int i = 0; i < items.Count; i++)
                        {
                            string childPrefix = prefix + "[" + i.ToString(CultureInfo.InvariantCulture) + "]";
                            CollectPairs(pairs, items[i], childPrefix);
                        }
                    }
                    break;
                }

                case DynValueType.Object:
                {
                    var entries = value.AsObject();
                    if (entries != null)
                    {
                        for (int i = 0; i < entries.Count; i++)
                        {
                            string childPrefix = string.IsNullOrEmpty(prefix)
                                ? entries[i].Key
                                : prefix + "." + entries[i].Key;
                            CollectPairs(pairs, entries[i].Value, childPrefix);
                        }
                    }
                    break;
                }
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // YAML style
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>Internal tree node for YAML generation.</summary>
        private sealed class YamlNode
        {
            public string? Value;
            public List<KeyValuePair<string, YamlNode>> Children = new List<KeyValuePair<string, YamlNode>>();
            public bool IsArrayItem;
        }

        private static string FormatYaml(DynValue value)
        {
            // Collect sorted flat pairs
            var pairs = new List<KeyValuePair<string, string>>();
            CollectPairs(pairs, value, "");
            pairs.Sort((a, b) => string.Compare(a.Key, b.Key, StringComparison.Ordinal));

            // Build tree from flat paths
            var root = new YamlNode();

            for (int p = 0; p < pairs.Count; p++)
            {
                string path = pairs[p].Key;
                string val = pairs[p].Value;

                // Parse path into segments
                var segments = ParsePathSegments(path);

                // Build tree
                var node = root;
                for (int i = 0; i < segments.Count - 1; i++)
                {
                    var seg = segments[i];
                    int idx = FindChild(node.Children, seg.Name);
                    if (idx < 0)
                    {
                        var child = new YamlNode { IsArrayItem = seg.IsArrayIndex };
                        node.Children.Add(new KeyValuePair<string, YamlNode>(seg.Name, child));
                        node = child;
                    }
                    else
                    {
                        node = node.Children[idx].Value;
                    }
                }

                var lastSeg = segments[segments.Count - 1];
                int leafIdx = FindChild(node.Children, lastSeg.Name);
                if (leafIdx < 0)
                {
                    var leaf = new YamlNode { IsArrayItem = lastSeg.IsArrayIndex, Value = val };
                    node.Children.Add(new KeyValuePair<string, YamlNode>(lastSeg.Name, leaf));
                }
                else
                {
                    node.Children[leafIdx].Value.Value = val;
                }
            }

            // Render tree to YAML
            var lines = new List<string>();
            RenderYamlNode(lines, root, 0, false);
            return string.Join("\n", lines);
        }

        private struct PathSegment
        {
            public string Name;
            public bool IsArrayIndex;
        }

        private static List<PathSegment> ParsePathSegments(string path)
        {
            var segments = new List<PathSegment>();
            var current = new StringBuilder();
            int i = 0;

            while (i < path.Length)
            {
                char c = path[i];
                if (c == '.')
                {
                    if (current.Length > 0)
                    {
                        segments.Add(new PathSegment { Name = current.ToString(), IsArrayIndex = false });
                        current.Clear();
                    }
                    i++;
                }
                else if (c == '[')
                {
                    if (current.Length > 0)
                    {
                        segments.Add(new PathSegment { Name = current.ToString(), IsArrayIndex = false });
                        current.Clear();
                    }
                    i++;
                    var indexStr = new StringBuilder();
                    while (i < path.Length && path[i] != ']')
                    {
                        indexStr.Append(path[i]);
                        i++;
                    }
                    segments.Add(new PathSegment { Name = indexStr.ToString(), IsArrayIndex = true });
                    i++; // skip ']'
                }
                else
                {
                    current.Append(c);
                    i++;
                }
            }

            if (current.Length > 0)
                segments.Add(new PathSegment { Name = current.ToString(), IsArrayIndex = false });

            return segments;
        }

        private static int FindChild(List<KeyValuePair<string, YamlNode>> children, string name)
        {
            for (int i = 0; i < children.Count; i++)
                if (children[i].Key == name) return i;
            return -1;
        }

        private static void RenderYamlNode(List<string> lines, YamlNode node, int indent, bool isArrayContext)
        {
            string pad = new string(' ', indent * 2);

            // Sort children: numeric keys by value, others alphabetically
            var sorted = new List<KeyValuePair<string, YamlNode>>(node.Children);
            sorted.Sort((a, b) =>
            {
                bool aNum = int.TryParse(a.Key, out int aVal);
                bool bNum = int.TryParse(b.Key, out int bVal);
                if (aNum && bNum) return aVal.CompareTo(bVal);
                return string.Compare(a.Key, b.Key, StringComparison.Ordinal);
            });

            for (int i = 0; i < sorted.Count; i++)
            {
                string key = sorted[i].Key;
                var child = sorted[i].Value;
                bool isArrayItem = child.IsArrayItem;

                if (child.Value != null && child.Children.Count == 0)
                {
                    // Leaf node
                    if (isArrayItem && isArrayContext)
                        lines.Add(pad + "- " + key + ": " + YamlQuote(child.Value));
                    else if (isArrayItem)
                        lines.Add(pad + "  " + key + ": " + YamlQuote(child.Value));
                    else
                        lines.Add(pad + key + ": " + YamlQuote(child.Value));
                }
                else if (child.Children.Count > 0)
                {
                    // Container node
                    bool childrenAreArrayItems = child.Children.Count > 0 &&
                        int.TryParse(child.Children[0].Key, out _);

                    if (isArrayItem)
                    {
                        // Array element with nested properties
                        var childEntries = new List<KeyValuePair<string, YamlNode>>(child.Children);
                        childEntries.Sort((a, b) =>
                        {
                            bool aNum = int.TryParse(a.Key, out int aVal);
                            bool bNum = int.TryParse(b.Key, out int bVal);
                            if (aNum && bNum) return aVal.CompareTo(bVal);
                            return string.Compare(a.Key, b.Key, StringComparison.Ordinal);
                        });

                        bool first = true;
                        for (int j = 0; j < childEntries.Count; j++)
                        {
                            string childKey = childEntries[j].Key;
                            var childNode = childEntries[j].Value;

                            if (first)
                            {
                                if (childNode.Value != null)
                                    lines.Add(pad + "- " + childKey + ": " + YamlQuote(childNode.Value));
                                else
                                {
                                    lines.Add(pad + "- " + childKey + ":");
                                    RenderYamlNode(lines, childNode, indent + 2, false);
                                }
                                first = false;
                            }
                            else
                            {
                                if (childNode.Value != null)
                                    lines.Add(pad + "  " + childKey + ": " + YamlQuote(childNode.Value));
                                else
                                {
                                    lines.Add(pad + "  " + childKey + ":");
                                    RenderYamlNode(lines, childNode, indent + 2, false);
                                }
                            }
                        }
                    }
                    else
                    {
                        lines.Add(pad + key + ":");
                        if (childrenAreArrayItems)
                            RenderYamlNode(lines, child, indent + 1, true);
                        else
                            RenderYamlNode(lines, child, indent + 1, false);
                    }
                }
            }
        }

        private static string YamlQuote(string value)
        {
            if (value == "" ||
                value == "true" || value == "false" ||
                value == "null" ||
                value == "yes" || value == "no" ||
                value.Contains(":") || value.Contains("#") ||
                value.Contains("\n") ||
                (value.Length > 0 && value[0] == ' ') ||
                (value.Length > 0 && value[value.Length - 1] == ' ') ||
                (value.Length > 0 && value[0] == '"') ||
                (value.Length > 0 && value[0] == '\''))
            {
                return "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n") + "\"";
            }
            return value;
        }
    }
}
