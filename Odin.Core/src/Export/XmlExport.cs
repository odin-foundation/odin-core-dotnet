using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Odin.Core.Types;

namespace Odin.Core.Export;

/// <summary>Exports OdinDocument to XML string.</summary>
public static class XmlExport
{
    /// <summary>Convert an OdinDocument to an XML string.</summary>
    public static string ToXml(OdinDocument doc, bool preserveTypes = false, bool preserveModifiers = false, string rootElement = "root")
    {
        // When modifiers are preserved, types must also be preserved (golden test contract)
        if (preserveModifiers) preserveTypes = true;

        var sb = new StringBuilder();
        sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n");

        // Add xmlns:odin namespace if preserveTypes or preserveModifiers
        sb.Append('<').Append(rootElement);
        if (preserveTypes || preserveModifiers)
            sb.Append(" xmlns:odin=\"https://odin.foundation/ns\"");
        sb.Append(">\n");

        var sections = new Dictionary<string, List<KeyValuePair<string, OdinValue>>>();
        var sectionOrder = new List<string>();

        foreach (var entry in doc.Assignments)
        {
            if (entry.Key.StartsWith("$", StringComparison.Ordinal))
                continue; // Skip metadata

            var dotIndex = entry.Key.IndexOf('.');
            if (dotIndex > 0)
            {
                var section = entry.Key.Substring(0, dotIndex);
                var field = entry.Key.Substring(dotIndex + 1);
                if (!sections.ContainsKey(section))
                {
                    sections[section] = new List<KeyValuePair<string, OdinValue>>();
                    sectionOrder.Add(section);
                }
                sections[section].Add(new KeyValuePair<string, OdinValue>(field, entry.Value));
            }
            else
            {
                WriteElement(sb, entry.Key, entry.Value, "  ", preserveTypes, preserveModifiers, doc);
            }
        }

        foreach (var sectionName in sectionOrder)
        {
            sb.Append("  <").Append(sectionName).Append(">\n");
            foreach (var field in sections[sectionName])
            {
                WriteElement(sb, field.Key, field.Value, "    ", preserveTypes, preserveModifiers, doc);
            }
            sb.Append("  </").Append(sectionName).Append(">\n");
        }

        sb.Append("</").Append(rootElement).Append(">\n");
        return sb.ToString();
    }

    private static void WriteElement(StringBuilder sb, string name, OdinValue value, string indent, bool preserveTypes, bool preserveModifiers, OdinDocument doc)
    {
        // Skip null values entirely
        if (value is OdinNull) return;

        sb.Append(indent).Append('<').Append(EscapeXmlName(name));

        // Type attributes (skip for string type — it's the default in XML)
        if (preserveTypes && !(value is OdinString) && !(value is OdinNull))
        {
            string typeName = GetOdinTypeName(value);
            if (typeName != null)
                sb.Append(" odin:type=\"").Append(typeName).Append('"');

            // Currency code
            if (value is OdinCurrency c && c.CurrencyCode != null)
                sb.Append(" odin:currencyCode=\"").Append(c.CurrencyCode).Append('"');
        }

        // Modifier attributes
        if (preserveModifiers && value.Modifiers is { } mods && mods.HasAny)
        {
            if (mods.Required) sb.Append(" odin:required=\"true\"");
            if (mods.Confidential) sb.Append(" odin:confidential=\"true\"");
            if (mods.Deprecated) sb.Append(" odin:deprecated=\"true\"");
        }

        switch (value)
        {
            case OdinArray arr:
                sb.Append(">\n");
                foreach (var item in arr.Items)
                {
                    if (item is OdinArrayValue av)
                        WriteElement(sb, "item", av.Value, indent + "  ", preserveTypes, preserveModifiers, doc);
                    else if (item is OdinArrayRecord rec)
                    {
                        sb.Append(indent).Append("  <item>\n");
                        foreach (var f in rec.Fields)
                            WriteElement(sb, f.Key, f.Value, indent + "    ", preserveTypes, preserveModifiers, doc);
                        sb.Append(indent).Append("  </item>\n");
                    }
                }
                sb.Append(indent).Append("</").Append(EscapeXmlName(name)).Append(">\n");
                break;
            case OdinObject obj:
                sb.Append(">\n");
                foreach (var f in obj.Fields)
                    WriteElement(sb, f.Key, f.Value, indent + "  ", preserveTypes, preserveModifiers, doc);
                sb.Append(indent).Append("</").Append(EscapeXmlName(name)).Append(">\n");
                break;
            default:
                sb.Append('>');
                sb.Append(EscapeXmlContent(FormatValueText(value)));
                sb.Append("</").Append(EscapeXmlName(name)).Append(">\n");
                break;
        }
    }

    private static string? GetOdinTypeName(OdinValue value) => value switch
    {
        OdinInteger => "integer",
        OdinNumber => "number",
        OdinCurrency => "currency",
        OdinPercent => "percent",
        OdinBoolean => "boolean",
        OdinDate => "date",
        OdinTimestamp => "timestamp",
        OdinTime => "time",
        OdinDuration => "duration",
        OdinReference => "reference",
        OdinBinary => "binary",
        _ => null,
    };

    private static string FormatValueText(OdinValue value) => value switch
    {
        OdinBoolean b => b.Value ? "true" : "false",
        OdinString s => s.Value,
        OdinInteger i => i.Raw ?? i.Value.ToString(CultureInfo.InvariantCulture),
        OdinNumber n => n.Raw ?? n.Value.ToString(CultureInfo.InvariantCulture),
        OdinCurrency c => FormatCurrencyText(c),
        OdinPercent p => p.Raw ?? p.Value.ToString(CultureInfo.InvariantCulture),
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

    private static string FormatCurrencyText(OdinCurrency c)
    {
        string raw = c.Raw ?? c.Value.ToString(CultureInfo.InvariantCulture);
        // Strip currency code suffix (e.g., "250.00:USD" → "250.00")
        int colonIdx = raw.IndexOf(':');
        if (colonIdx >= 0) raw = raw.Substring(0, colonIdx);
        return raw;
    }

    private static string EscapeXmlContent(string text)
    {
        return text
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&apos;");
    }

    private static string EscapeXmlName(string name)
    {
        if (name.Length > 0 && char.IsDigit(name[0]))
            return "_" + name;
        return name.Replace(' ', '_').Replace('[', '_').Replace(']', '_');
    }
}
