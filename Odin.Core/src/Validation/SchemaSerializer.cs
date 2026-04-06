using System.Collections.Generic;
using System.Text;
using Odin.Core.Types;

namespace Odin.Core.Validation
{
    /// <summary>
    /// Serializes an <see cref="OdinSchemaDefinition"/> back to ODIN schema text.
    /// Enables round-tripping: parse schema, modify, serialize back to text.
    /// </summary>
    public static class SchemaSerializer
    {
        /// <summary>
        /// Serialize an <see cref="OdinSchemaDefinition"/> to ODIN schema text.
        /// </summary>
        /// <param name="schema">The schema definition to serialize.</param>
        /// <returns>The schema as ODIN text.</returns>
        public static string Serialize(OdinSchemaDefinition schema)
        {
            var sb = new StringBuilder();

            // Metadata section
            sb.AppendLine("{$}");
            if (schema.Metadata.Id != null)
                sb.AppendLine("id = \"" + schema.Metadata.Id + "\"");
            if (schema.Metadata.Title != null)
                sb.AppendLine("title = \"" + schema.Metadata.Title + "\"");
            if (schema.Metadata.Description != null)
                sb.AppendLine("description = \"" + schema.Metadata.Description + "\"");
            if (schema.Metadata.Version != null)
                sb.AppendLine("version = \"" + schema.Metadata.Version + "\"");
            sb.AppendLine();

            // Imports
            foreach (var import in schema.Imports)
            {
                if (import.Alias != null)
                    sb.AppendLine("@import \"" + import.Path + "\" as " + import.Alias);
                else
                    sb.AppendLine("@import \"" + import.Path + "\"");
            }
            if (schema.Imports.Count > 0)
                sb.AppendLine();

            // Type definitions
            foreach (var kvp in schema.Types)
            {
                SerializeType(sb, kvp.Key, kvp.Value);
                sb.AppendLine();
            }

            // Top-level fields (organized by section)
            var sections = new List<KeyValuePair<string, List<KeyValuePair<string, SchemaField>>>>();

            foreach (var kvp in schema.Fields)
            {
                var path = kvp.Key;
                var field = kvp.Value;
                int dotPos = path.IndexOf('.');
                string section;

                if (dotPos >= 0)
                {
                    section = path.Substring(0, dotPos);
                }
                else
                {
                    section = "";
                }

                // Find or create section
                int sectionIdx = -1;
                for (int i = 0; i < sections.Count; i++)
                {
                    if (sections[i].Key == section)
                    {
                        sectionIdx = i;
                        break;
                    }
                }

                if (sectionIdx >= 0)
                {
                    sections[sectionIdx].Value.Add(new KeyValuePair<string, SchemaField>(path, field));
                }
                else
                {
                    var list = new List<KeyValuePair<string, SchemaField>>
                    {
                        new KeyValuePair<string, SchemaField>(path, field)
                    };
                    sections.Add(new KeyValuePair<string, List<KeyValuePair<string, SchemaField>>>(section, list));
                }
            }

            foreach (var section in sections)
            {
                if (section.Key.Length > 0)
                    sb.AppendLine("{" + section.Key + "}");

                foreach (var kvp in section.Value)
                {
                    string fieldName;
                    if (section.Key.Length == 0)
                        fieldName = kvp.Key;
                    else if (kvp.Key.StartsWith(section.Key + ".", System.StringComparison.Ordinal))
                        fieldName = kvp.Key.Substring(section.Key.Length + 1);
                    else
                        fieldName = kvp.Key;

                    SerializeField(sb, fieldName, kvp.Value);
                }
            }

            // Array definitions
            foreach (var kvp in schema.Arrays)
            {
                var path = kvp.Key;
                var arrayDef = kvp.Value;
                sb.Append(path + "[] = ");
                sb.Append(FormatFieldType(arrayDef.ItemType));
                if (arrayDef.MinItems.HasValue)
                {
                    if (arrayDef.MaxItems.HasValue)
                        sb.Append(" :(" + arrayDef.MinItems.Value + ".." + arrayDef.MaxItems.Value + ")");
                    else
                        sb.Append(" :(" + arrayDef.MinItems.Value + "..)");
                }
                else if (arrayDef.MaxItems.HasValue)
                {
                    sb.Append(" :(.." + arrayDef.MaxItems.Value + ")");
                }
                if (arrayDef.IsUnique)
                    sb.Append(" :unique");
                sb.AppendLine();
            }

            // Object constraints
            foreach (var kvp in schema.Constraints)
            {
                var path = kvp.Key;
                foreach (var constraint in kvp.Value)
                {
                    if (constraint is InvariantConstraint inv)
                    {
                        sb.AppendLine("; invariant at " + path + ": " + inv.Expression);
                    }
                    else if (constraint is CardinalityConstraint card)
                    {
                        var fieldsStr = string.Join(", ", card.Fields);
                        if (card.Min == 1 && card.Max == 1)
                            sb.AppendLine(":exactly_one(" + fieldsStr + ") ; at " + path);
                        else if (card.Min == 1 && !card.Max.HasValue)
                            sb.AppendLine(":one_of(" + fieldsStr + ") ; at " + path);
                        else if (!card.Min.HasValue && card.Max == 1)
                            sb.AppendLine(":at_most_one(" + fieldsStr + ") ; at " + path);
                        else
                            sb.AppendLine("; cardinality(" + fieldsStr + ") at " + path + ": min=" + card.Min + " max=" + card.Max);
                    }
                }
            }

            return sb.ToString();
        }

        private static void SerializeType(StringBuilder sb, string name, SchemaType schemaType)
        {
            sb.Append('@');
            sb.Append(name);
            if (schemaType.Parents.Count > 0)
            {
                sb.Append(" : ");
                sb.Append(string.Join(" & ", schemaType.Parents));
            }
            sb.AppendLine();

            foreach (var field in schemaType.SchemaFields)
            {
                SerializeField(sb, field.Name, field);
            }
        }

        private static void SerializeField(StringBuilder sb, string name, SchemaField field)
        {
            sb.Append(name);
            sb.Append(" = ");

            // Type prefix
            sb.Append(FormatFieldType(field.FieldType));

            // Constraints
            foreach (var constraint in field.Constraints)
            {
                if (constraint is BoundsConstraint bounds)
                {
                    if (bounds.Min != null && bounds.Max != null)
                        sb.Append(" :(" + bounds.Min + ".." + bounds.Max + ")");
                    else if (bounds.Min != null)
                        sb.Append(" :(" + bounds.Min + "..)");
                    else if (bounds.Max != null)
                        sb.Append(" :(.." + bounds.Max + ")");
                }
                else if (constraint is PatternConstraint pat)
                {
                    sb.Append(" :pattern \"" + pat.PatternValue + "\"");
                }
                else if (constraint is EnumConstraint en)
                {
                    var vals = new List<string>();
                    foreach (var v in en.Values)
                        vals.Add("\"" + v + "\"");
                    sb.Append(" :enum(" + string.Join(", ", vals) + ")");
                }
                else if (constraint is FormatConstraint fmt)
                {
                    sb.Append(" :format " + fmt.FormatName);
                }
                else if (constraint is UniqueConstraint)
                {
                    sb.Append(" :unique");
                }
                else if (constraint is SizeConstraint sz)
                {
                    if (sz.Min.HasValue && sz.Max.HasValue)
                        sb.Append(" :size(" + sz.Min.Value + ".." + sz.Max.Value + ")");
                    else if (sz.Min.HasValue)
                        sb.Append(" :size(" + sz.Min.Value + "..)");
                    else if (sz.Max.HasValue)
                        sb.Append(" :size(.." + sz.Max.Value + ")");
                }
            }

            // Modifiers
            if (field.Required)
                sb.Append(" :required");
            if (field.Confidential)
                sb.Append(" :confidential");
            if (field.Deprecated)
                sb.Append(" :deprecated");

            sb.AppendLine();
        }

        /// <summary>
        /// Format a <see cref="SchemaFieldType"/> as its ODIN schema text representation.
        /// </summary>
        internal static string FormatFieldType(SchemaFieldType ft)
        {
            if (ft is StringFieldType) return "\"\"";
            if (ft is BooleanFieldType) return "?";
            if (ft is NullFieldType) return "~";
            if (ft is NumberFieldType || ft is DecimalFieldType) return "#";
            if (ft is IntegerFieldType) return "##";
            if (ft is CurrencyFieldType) return "#$";
            if (ft is DateFieldType) return ":date";
            if (ft is TimestampFieldType) return ":timestamp";
            if (ft is TimeFieldType) return ":time";
            if (ft is DurationFieldType) return ":duration";
            if (ft is PercentFieldType) return "#%";
            if (ft is BinaryFieldType) return "^";
            if (ft is EnumFieldType enumFt)
            {
                var vals = new List<string>();
                foreach (var v in enumFt.Values)
                    vals.Add("\"" + v + "\"");
                return string.Join("|", vals);
            }
            if (ft is UnionFieldType unionFt)
            {
                var parts = new List<string>();
                foreach (var m in unionFt.Types)
                    parts.Add(FormatFieldType(m));
                return string.Join("|", parts);
            }
            if (ft is ReferenceFieldType refFt)
                return "@" + refFt.Target;
            if (ft is TypeRefFieldType typeRefFt)
                return "@" + typeRefFt.Name;

            return "\"\"";
        }
    }
}
