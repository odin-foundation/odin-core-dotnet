using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using Odin.Core.Types;

namespace Odin.Core.Export;

/// <summary>Exports OdinDocument to JSON string.</summary>
public static class JsonExport
{
    /// <summary>Convert an OdinDocument to a JSON string.</summary>
    public static string ToJson(OdinDocument doc, bool preserveTypes = false, bool preserveModifiers = false)
    {
        using var stream = new System.IO.MemoryStream();
        using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions
        {
            Indented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });

        writer.WriteStartObject();
        WriteAssignments(writer, doc, preserveTypes, preserveModifiers);
        writer.WriteEndObject();

        writer.Flush();
        var json = Encoding.UTF8.GetString(stream.ToArray());
        // Convert escaped surrogate pairs (\uDxxx\uDxxx) back to literal UTF-8
        return FixSurrogatePairEscapes(json);
    }

    private static string FixSurrogatePairEscapes(string json)
    {
        return Regex.Replace(json,
            @"\\u([Dd][89AaBb][0-9A-Fa-f]{2})\\u([Dd][CcDdEeFf][0-9A-Fa-f]{2})",
            m =>
            {
                char high = (char)int.Parse(m.Groups[1].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                char low = (char)int.Parse(m.Groups[2].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                return char.ConvertFromUtf32(char.ConvertToUtf32(high, low));
            });
    }

    private static void WriteAssignments(Utf8JsonWriter writer, OdinDocument doc, bool preserveTypes, bool preserveModifiers)
    {
        var sections = new Dictionary<string, List<KeyValuePair<string, OdinValue>>>();
        var sectionOrder = new List<string>();

        // Separate into sections and array sections
        var arraySections = new Dictionary<string, List<List<KeyValuePair<string, OdinValue>>>>();

        foreach (var entry in doc.Assignments)
        {
            var dotIndex = entry.Key.IndexOf('.');
            if (dotIndex > 0)
            {
                var section = entry.Key.Substring(0, dotIndex);
                var field = entry.Key.Substring(dotIndex + 1);

                // Check for array index: section[N].field
                int bracketPos = section.IndexOf('[');
                if (bracketPos >= 0)
                {
                    string arrName = section.Substring(0, bracketPos);
                    string idxStr = section.Substring(bracketPos + 1, section.Length - bracketPos - 2);
                    if (int.TryParse(idxStr, out int arrIdx))
                    {
                        if (!arraySections.ContainsKey(arrName))
                        {
                            arraySections[arrName] = new List<List<KeyValuePair<string, OdinValue>>>();
                            if (!sectionOrder.Contains(arrName))
                                sectionOrder.Add(arrName);
                        }
                        while (arraySections[arrName].Count <= arrIdx)
                            arraySections[arrName].Add(new List<KeyValuePair<string, OdinValue>>());
                        arraySections[arrName][arrIdx].Add(new KeyValuePair<string, OdinValue>(field, entry.Value));
                        continue;
                    }
                }

                // Handle deeper nested paths: section.subsection.field
                int secondDot = field.IndexOf('.');
                if (secondDot > 0)
                {
                    string subsection = section + "." + field.Substring(0, secondDot);
                    string subfield = field.Substring(secondDot + 1);
                    if (!sections.ContainsKey(subsection))
                    {
                        sections[subsection] = new List<KeyValuePair<string, OdinValue>>();
                        if (!sectionOrder.Contains(subsection))
                            sectionOrder.Add(subsection);
                    }
                    sections[subsection].Add(new KeyValuePair<string, OdinValue>(subfield, entry.Value));
                }
                else
                {
                    if (!sections.ContainsKey(section))
                    {
                        sections[section] = new List<KeyValuePair<string, OdinValue>>();
                        if (!sectionOrder.Contains(section))
                            sectionOrder.Add(section);
                    }
                    sections[section].Add(new KeyValuePair<string, OdinValue>(field, entry.Value));
                }
            }
        }

        // Write non-$ sections first, then $
        foreach (var sectionName in sectionOrder)
        {
            if (sectionName == "$") continue;
            WriteSectionOrArray(writer, sectionName, sections, arraySections, preserveTypes, preserveModifiers, doc);
        }

        // Write $ section at the end
        if (sections.ContainsKey("$") || sectionOrder.Contains("$"))
        {
            WriteSectionOrArray(writer, "$", sections, arraySections, preserveTypes, preserveModifiers, doc);
        }
    }

    private static void WriteSectionOrArray(Utf8JsonWriter writer, string name,
        Dictionary<string, List<KeyValuePair<string, OdinValue>>> sections,
        Dictionary<string, List<List<KeyValuePair<string, OdinValue>>>> arraySections,
        bool preserveTypes, bool preserveModifiers, OdinDocument doc)
    {
        if (arraySections.ContainsKey(name))
        {
            writer.WritePropertyName(name);
            writer.WriteStartArray();
            foreach (var record in arraySections[name])
            {
                writer.WriteStartObject();
                foreach (var field in record)
                {
                    writer.WritePropertyName(field.Key);
                    WriteRawValue(writer, field.Value, preserveTypes);
                }
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
        }
        else if (sections.ContainsKey(name))
        {
            writer.WritePropertyName(name);
            writer.WriteStartObject();
            foreach (var field in sections[name])
            {
                writer.WritePropertyName(field.Key);
                WriteRawValue(writer, field.Value, preserveTypes);
            }
            writer.WriteEndObject();
        }
    }

    private static void WriteNumberWithCorrectCase(Utf8JsonWriter writer, double value)
    {
        // .NET may produce uppercase E in scientific notation (1E-18).
        // JSON convention uses lowercase e (1e-18). Use WriteRawValue for such cases.
        string formatted = value.ToString("G", CultureInfo.InvariantCulture);
        if (formatted.Contains("E"))
        {
            writer.WriteRawValue(formatted.Replace("E+", "e+").Replace("E-", "e-"), skipInputValidation: true);
        }
        else
        {
            writer.WriteNumberValue(value);
        }
    }

    private static void WriteRawValue(Utf8JsonWriter writer, OdinValue value, bool preserveTypes)
    {
        switch (value)
        {
            case OdinNull:
                writer.WriteNullValue();
                break;
            case OdinBoolean b:
                writer.WriteBooleanValue(b.Value);
                break;
            case OdinString s:
                writer.WriteStringValue(s.Value);
                break;
            case OdinInteger i:
                writer.WriteNumberValue(i.Value);
                break;
            case OdinNumber n:
                WriteNumberWithCorrectCase(writer, n.Value);
                break;
            case OdinCurrency c:
                WriteNumberWithCorrectCase(writer, c.Value);
                break;
            case OdinPercent p:
                WriteNumberWithCorrectCase(writer, p.Value);
                break;
            case OdinDate d:
                writer.WriteStringValue(d.Raw);
                break;
            case OdinTimestamp ts:
                writer.WriteStringValue(ts.Raw);
                break;
            case OdinTime t:
                writer.WriteStringValue(t.Value);
                break;
            case OdinDuration d:
                writer.WriteStringValue(d.Value);
                break;
            case OdinReference r:
                writer.WriteStringValue("@" + r.Path);
                break;
            case OdinBinary b:
            {
                var b64 = Convert.ToBase64String(b.Data);
                writer.WriteStringValue(b.Algorithm != null ? "^" + b.Algorithm + ":" + b64 : "^" + b64);
                break;
            }
            case OdinArray arr:
                writer.WriteStartArray();
                foreach (var item in arr.Items)
                {
                    if (item is OdinArrayValue av)
                        WriteRawValue(writer, av.Value, preserveTypes);
                    else if (item is OdinArrayRecord rec)
                    {
                        writer.WriteStartObject();
                        foreach (var f in rec.Fields)
                        {
                            writer.WritePropertyName(f.Key);
                            WriteRawValue(writer, f.Value, preserveTypes);
                        }
                        writer.WriteEndObject();
                    }
                }
                writer.WriteEndArray();
                break;
            case OdinObject obj:
                writer.WriteStartObject();
                foreach (var f in obj.Fields)
                {
                    writer.WritePropertyName(f.Key);
                    WriteRawValue(writer, f.Value, preserveTypes);
                }
                writer.WriteEndObject();
                break;
            default:
                writer.WriteNullValue();
                break;
        }
    }
}
