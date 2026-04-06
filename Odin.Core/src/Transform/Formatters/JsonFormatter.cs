#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Odin.Core.Types;

namespace Odin.Core.Transform
{
    /// <summary>
    /// Formats a <see cref="DynValue"/> tree as a JSON string.
    /// </summary>
    public static class JsonFormatter
    {
        /// <summary>
        /// Serialize a <see cref="DynValue"/> to a JSON string.
        /// </summary>
        /// <param name="value">The value to serialize.</param>
        /// <param name="config">Optional target configuration. Supports "indent" option ("true"/"false").</param>
        /// <returns>A JSON-formatted string.</returns>
        public static string Format(DynValue value, TargetConfig? config)
        {
            if (value == null) return "null";

            bool pretty = true; // Default to indented JSON
            string nullsMode = "include";
            string emptyArraysMode = "include";

            if (config != null)
            {
                if (config.Options.TryGetValue("indent", out var indentVal))
                    pretty = indentVal != "false" && indentVal != "0";
                if (config.Options.TryGetValue("nulls", out var nullsVal))
                    nullsMode = nullsVal;
                if (config.Options.TryGetValue("emptyArrays", out var eaVal))
                    emptyArraysMode = eaVal;
            }

            // Apply null/empty-array filtering before serialization
            var filtered = FilterValue(value, nullsMode, emptyArraysMode);
            if (filtered == null) return "null";

            using (var stream = new MemoryStream())
            {
                var options = new JsonWriterOptions
                {
                    Indented = pretty,
                    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };
                using (var writer = new Utf8JsonWriter(stream, options))
                {
                    WriteValue(writer, filtered);
                }

                return Encoding.UTF8.GetString(stream.ToArray());
            }
        }

        /// <summary>
        /// Recursively filter a DynValue tree, removing null-valued keys and/or empty arrays
        /// from objects based on the specified modes.
        /// Returns null if the value itself should be omitted.
        /// </summary>
        private static DynValue? FilterValue(DynValue value, string nullsMode, string emptyArraysMode)
        {
            if (nullsMode == "include" && emptyArraysMode == "include")
                return value; // No filtering needed

            if (value.Type == DynValueType.Null && nullsMode == "omit")
                return null;

            if (value.Type == DynValueType.Array)
            {
                var items = value.AsArray();
                if (items != null && items.Count == 0 && emptyArraysMode == "omit")
                    return null;

                if (items != null)
                {
                    var filtered = new List<DynValue>();
                    for (int i = 0; i < items.Count; i++)
                    {
                        var f = FilterValue(items[i], nullsMode, emptyArraysMode);
                        if (f != null)
                            filtered.Add(f);
                    }
                    return DynValue.Array(filtered);
                }
            }

            if (value.Type == DynValueType.Object)
            {
                var entries = value.AsObject();
                if (entries != null)
                {
                    var filtered = new List<KeyValuePair<string, DynValue>>();
                    for (int i = 0; i < entries.Count; i++)
                    {
                        var f = FilterValue(entries[i].Value, nullsMode, emptyArraysMode);
                        if (f != null)
                            filtered.Add(new KeyValuePair<string, DynValue>(entries[i].Key, f));
                    }
                    return DynValue.Object(filtered);
                }
            }

            return value;
        }

        private static void WriteValue(Utf8JsonWriter writer, DynValue value)
        {
            switch (value.Type)
            {
                case DynValueType.Null:
                    writer.WriteNullValue();
                    break;

                case DynValueType.Bool:
                    writer.WriteBooleanValue(value.AsBool() ?? false);
                    break;

                case DynValueType.Integer:
                    writer.WriteNumberValue(value.AsInt64() ?? 0);
                    break;

                case DynValueType.Float:
                case DynValueType.Currency:
                case DynValueType.Percent:
                {
                    double d = value.AsDouble() ?? 0.0;
                    if (double.IsInfinity(d) || double.IsNaN(d))
                        writer.WriteNullValue();
                    else
                        writer.WriteNumberValue(d);
                    break;
                }

                case DynValueType.FloatRaw:
                case DynValueType.CurrencyRaw:
                {
                    string? raw = value.AsString();
                    if (raw != null && double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed))
                        writer.WriteNumberValue(parsed);
                    else
                        writer.WriteStringValue(raw);
                    break;
                }

                case DynValueType.String:
                case DynValueType.Reference:
                case DynValueType.Binary:
                case DynValueType.Date:
                case DynValueType.Timestamp:
                case DynValueType.Time:
                case DynValueType.Duration:
                    writer.WriteStringValue(value.AsString());
                    break;

                case DynValueType.Array:
                {
                    var items = value.AsArray();
                    writer.WriteStartArray();
                    if (items != null)
                    {
                        for (int i = 0; i < items.Count; i++)
                            WriteValue(writer, items[i]);
                    }
                    writer.WriteEndArray();
                    break;
                }

                case DynValueType.Object:
                {
                    var entries = value.AsObject();
                    writer.WriteStartObject();
                    if (entries != null)
                    {
                        for (int i = 0; i < entries.Count; i++)
                        {
                            writer.WritePropertyName(entries[i].Key);
                            WriteValue(writer, entries[i].Value);
                        }
                    }
                    writer.WriteEndObject();
                    break;
                }

                default:
                    writer.WriteNullValue();
                    break;
            }
        }
    }
}
