#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Odin.Core.Types;

namespace Odin.Core.Transform
{
    /// <summary>
    /// Formats a <see cref="DynValue"/> tree as an XML string.
    /// Supports type attributes, modifier attributes, :attr fields, and multi-root output.
    /// </summary>
    public static class XmlFormatter
    {
        /// <summary>
        /// Serialize a <see cref="DynValue"/> to an XML string with modifier and type attribute support.
        /// Each top-level section becomes its own root element. Array sections become repeated elements.
        /// Fields with :attr modifier become XML attributes instead of child elements.
        /// </summary>
        public static string FormatWithModifiers(DynValue value, TargetConfig? config,
            Dictionary<string, OdinModifiers> modifiers)
        {
            if (value == null) return "";

            int indent = 2;
            bool declaration = true;
            if (config != null)
            {
                if (config.Options.TryGetValue("indent", out var indentStr))
                    int.TryParse(indentStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out indent);
                if (config.Options.TryGetValue("declaration", out var declVal))
                    declaration = declVal != "false";
            }

            var sb = new StringBuilder();
            if (declaration)
                sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n");

            var entries = value.AsObject();
            if (entries == null)
            {
                // Not an object — wrap in root element
                string rootEl = "root";
                if (config != null && config.Options.TryGetValue("rootElement", out var re) && !string.IsNullOrEmpty(re))
                    rootEl = re;
                WriteElement(sb, rootEl, value, indent, 0, modifiers, "", false);
                return sb.ToString();
            }

            // Check if any values have types that need odin namespace
            bool needsNamespace = HasTypedValues(value);

            // Multi-root: each top-level key is its own root element
            for (int i = 0; i < entries.Count; i++)
            {
                string key = entries[i].Key;
                var child = entries[i].Value;

                if (child.Type == DynValueType.Array)
                {
                    // Array section: repeated elements
                    var items = child.AsArray();
                    if (items != null)
                    {
                        for (int j = 0; j < items.Count; j++)
                        {
                            WriteArrayItemElement(sb, key, items[j], indent, 0, modifiers, key);
                        }
                    }
                }
                else
                {
                    // Object section: single root element with namespace
                    sb.Append('<').Append(key);
                    if (needsNamespace)
                        sb.Append(" xmlns:odin=\"https://odin.foundation/ns\"");
                    sb.Append(">\n");
                    WriteObjectChildren(sb, child, indent, 1, modifiers, key);
                    sb.Append("</").Append(key).Append(">\n");
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Serialize a <see cref="DynValue"/> to an XML string.
        /// </summary>
        public static string Format(DynValue value, TargetConfig? config)
        {
            return FormatWithModifiers(value, config, new Dictionary<string, OdinModifiers>());
        }

        private static void WriteArrayItemElement(StringBuilder sb, string tag, DynValue item,
            int indent, int depth, Dictionary<string, OdinModifiers> modifiers, string pathPrefix)
        {
            var pad = Pad(indent, depth);

            if (item.Type == DynValueType.Object)
            {
                var entries = item.AsObject();
                if (entries == null) return;

                // Collect :attr fields
                var attrFields = new List<KeyValuePair<string, DynValue>>();
                var childFields = new List<KeyValuePair<string, DynValue>>();

                for (int i = 0; i < entries.Count; i++)
                {
                    string modKey = pathPrefix + "." + entries[i].Key;
                    if (modifiers.TryGetValue(modKey, out var mods) && mods.Attr)
                        attrFields.Add(entries[i]);
                    else
                        childFields.Add(entries[i]);
                }

                sb.Append(pad).Append('<').Append(tag);
                for (int i = 0; i < attrFields.Count; i++)
                {
                    sb.Append(' ').Append(attrFields[i].Key).Append("=\"");
                    sb.Append(XmlEscape(ScalarToString(attrFields[i].Value)));
                    sb.Append('"');
                }
                sb.Append(">\n");

                for (int i = 0; i < childFields.Count; i++)
                {
                    WriteElement(sb, childFields[i].Key, childFields[i].Value, indent, depth + 1,
                        modifiers, pathPrefix + "." + childFields[i].Key, true);
                }

                sb.Append(pad).Append("</").Append(tag).Append(">\n");
            }
            else
            {
                WriteElement(sb, tag, item, indent, depth, modifiers, pathPrefix, true);
            }
        }

        private static void WriteObjectChildren(StringBuilder sb, DynValue value, int indent, int depth,
            Dictionary<string, OdinModifiers> modifiers, string pathPrefix)
        {
            var entries = value.AsObject();
            if (entries == null) return;

            for (int i = 0; i < entries.Count; i++)
            {
                string childPath = pathPrefix + "." + entries[i].Key;
                WriteElement(sb, entries[i].Key, entries[i].Value, indent, depth, modifiers, childPath, true);
            }
        }

        private static void WriteElement(StringBuilder sb, string tag, DynValue value, int indent, int depth,
            Dictionary<string, OdinModifiers> modifiers, string modKey, bool includeTypeAttr)
        {
            var pad = Pad(indent, depth);

            switch (value.Type)
            {
                case DynValueType.Null:
                    sb.Append(pad).Append('<').Append(tag).Append(" odin:type=\"null\"></").Append(tag).Append(">\n");
                    break;

                case DynValueType.Array:
                {
                    var items = value.AsArray();
                    if (items != null)
                    {
                        for (int i = 0; i < items.Count; i++)
                            WriteArrayItemElement(sb, tag, items[i], indent, depth, modifiers, modKey);
                    }
                    break;
                }

                case DynValueType.Object:
                {
                    sb.Append(pad).Append('<').Append(tag).Append(">\n");
                    WriteObjectChildren(sb, value, indent, depth + 1, modifiers, modKey);
                    sb.Append(pad).Append("</").Append(tag).Append(">\n");
                    break;
                }

                default:
                {
                    sb.Append(pad).Append('<').Append(tag);

                    // Type attribute
                    if (includeTypeAttr)
                    {
                        string? typeAttr = GetTypeAttribute(value);
                        if (typeAttr != null)
                            sb.Append(" odin:type=\"").Append(typeAttr).Append('"');
                    }

                    // Modifier attributes
                    if (modifiers.TryGetValue(modKey, out var mods))
                    {
                        if (mods.Required) sb.Append(" odin:required=\"true\"");
                        if (mods.Confidential) sb.Append(" odin:confidential=\"true\"");
                        if (mods.Deprecated) sb.Append(" odin:deprecated=\"true\"");
                    }

                    sb.Append('>');
                    sb.Append(XmlEscape(ScalarToString(value)));
                    sb.Append("</").Append(tag).Append(">\n");
                    break;
                }
            }
        }

        private static string? GetTypeAttribute(DynValue value)
        {
            switch (value.Type)
            {
                case DynValueType.Bool:
                    return "boolean";
                case DynValueType.Integer:
                    return "integer";
                case DynValueType.Float:
                    return "number";
                case DynValueType.Currency:
                case DynValueType.CurrencyRaw:
                {
                    // Whole-number currencies → integer, fractional → number
                    double d = value.AsDouble() ?? 0.0;
                    if (d == Math.Floor(d) && !double.IsInfinity(d))
                        return "integer";
                    return "number";
                }
                case DynValueType.Percent:
                case DynValueType.FloatRaw:
                    return "number";
                // String, Date, Timestamp, Time, Duration — no type attribute
                default:
                    return null;
            }
        }

        private static bool HasTypedValues(DynValue value)
        {
            if (value.Type == DynValueType.Object)
            {
                var entries = value.AsObject();
                if (entries != null)
                {
                    for (int i = 0; i < entries.Count; i++)
                    {
                        if (HasTypedValues(entries[i].Value)) return true;
                    }
                }
            }
            else if (value.Type == DynValueType.Array)
            {
                var items = value.AsArray();
                if (items != null)
                {
                    for (int i = 0; i < items.Count; i++)
                    {
                        if (HasTypedValues(items[i])) return true;
                    }
                }
            }
            else if (value.Type != DynValueType.String)
            {
                return true;
            }
            return false;
        }

        private static string ScalarToString(DynValue value)
        {
            switch (value.Type)
            {
                case DynValueType.Bool:
                    return (value.AsBool() ?? false) ? "true" : "false";
                case DynValueType.Integer:
                    return (value.AsInt64() ?? 0).ToString(CultureInfo.InvariantCulture);
                case DynValueType.Float:
                case DynValueType.Percent:
                    return FormatFloat(value.AsDouble() ?? 0.0);
                case DynValueType.Currency:
                    return FormatFloat(value.AsDouble() ?? 0.0);
                case DynValueType.FloatRaw:
                case DynValueType.CurrencyRaw:
                    return value.AsString() ?? "0";
                case DynValueType.String:
                case DynValueType.Reference:
                case DynValueType.Binary:
                case DynValueType.Date:
                case DynValueType.Timestamp:
                case DynValueType.Time:
                case DynValueType.Duration:
                    return value.AsString() ?? "";
                default:
                    return "";
            }
        }

        private static string FormatFloat(double n)
        {
            if (n == Math.Floor(n) && !double.IsInfinity(n) && Math.Abs(n) < 1e15)
                return ((long)n).ToString(CultureInfo.InvariantCulture);
            return n.ToString("G", CultureInfo.InvariantCulture);
        }

        private static string XmlEscape(string s)
        {
            var sb = new StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                switch (c)
                {
                    case '&': sb.Append("&amp;"); break;
                    case '<': sb.Append("&lt;"); break;
                    case '>': sb.Append("&gt;"); break;
                    case '"': sb.Append("&quot;"); break;
                    case '\'': sb.Append("&apos;"); break;
                    default: sb.Append(c); break;
                }
            }
            return sb.ToString();
        }

        private static string Pad(int indent, int depth)
        {
            int count = indent * depth;
            if (count == 0) return "";
            return new string(' ', count);
        }
    }
}
