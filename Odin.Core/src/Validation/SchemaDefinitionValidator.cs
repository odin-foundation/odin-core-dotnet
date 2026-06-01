using System;
using System.Collections.Generic;
using System.Globalization;
using Odin.Core.Resolver;
using Odin.Core.Types;

namespace Odin.Core.Validation
{
    /// <summary>
    /// Validates that a schema is well-formed, independent of any document:
    /// override restrictiveness, intersection field conflicts, tabular column rules,
    /// and default-value rules. Violations are reported as V017.
    /// </summary>
    internal static class SchemaDefinitionValidator
    {
        private static readonly HashSet<string> PrimitiveTypes = new HashSet<string>
        {
            nameof(StringFieldType), nameof(BooleanFieldType), nameof(NumberFieldType),
            nameof(IntegerFieldType), nameof(DecimalFieldType), nameof(CurrencyFieldType),
            nameof(PercentFieldType), nameof(DateFieldType), nameof(TimestampFieldType),
            nameof(TimeFieldType), nameof(DurationFieldType), nameof(EnumFieldType),
            nameof(BinaryFieldType), nameof(NullFieldType),
        };

        /// <summary>Run all schema-definition validations.</summary>
        public static void Validate(
            OdinSchemaDefinition schema,
            TypeRegistry? registry,
            List<ValidationError> errors)
        {
            ValidateTypeDefinitions(schema, registry, errors);
            ValidatePathCompositions(schema, registry, errors);
            ValidateTabularColumns(schema, registry, errors);
            ValidateDefaults(schema, errors);
        }

        private static void AddError(List<ValidationError> errors, string path, string message)
        {
            errors.Add(new ValidationError(ValidationErrorCode.SchemaDefinitionError, path, message));
        }

        private static SchemaType? LookupBaseType(OdinSchemaDefinition schema, TypeRegistry? registry, string name)
        {
            if (registry != null)
            {
                var t = registry.Lookup(name);
                if (t != null) return t;
            }
            return schema.Types.TryGetValue(name, out var local) ? local : null;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Override and intersection (type definitions)
        // ─────────────────────────────────────────────────────────────────────

        private static void ValidateTypeDefinitions(
            OdinSchemaDefinition schema, TypeRegistry? registry, List<ValidationError> errors)
        {
            foreach (var kvp in schema.Types)
            {
                var typeName = kvp.Key;
                var type = kvp.Value;
                var composition = FindComposition(type.SchemaFields);
                if (composition is not TypeRefFieldType typeRef) continue;

                var memberNames = SplitMembers(typeRef.Name);
                if (typeRef.Override)
                    ValidateOverride(schema, registry, typeName, type, memberNames, errors);
                else if (memberNames.Count > 1)
                    ValidateIntersectionConflicts(schema, registry, typeName, memberNames, errors);
            }
        }

        private static void ValidateOverride(
            OdinSchemaDefinition schema, TypeRegistry? registry, string typeName,
            SchemaType type, List<string> baseNames, List<ValidationError> errors)
        {
            var baseFields = new Dictionary<string, SchemaField>();
            foreach (var baseName in baseNames)
            {
                var b = LookupBaseType(schema, registry, baseName);
                if (b == null) continue;
                foreach (var f in b.SchemaFields)
                    if (f.Name != "_composition") baseFields[f.Name] = f;
            }

            foreach (var ov in type.SchemaFields)
            {
                if (ov.Name == "_composition") continue;
                if (!baseFields.TryGetValue(ov.Name, out var bf)) continue;
                CheckOverrideField(string.Format(CultureInfo.InvariantCulture, "@{0}.{1}", typeName, ov.Name), bf, ov, errors);
            }
        }

        private static void CheckOverrideField(
            string label, SchemaField baseField, SchemaField ov, List<ValidationError> errors)
        {
            // Base type must match.
            if (!SameBaseType(baseField.FieldType, ov.FieldType))
                AddError(errors, label, "Override changes field type");

            // required: optional→required allowed, required→optional forbidden.
            if (baseField.Required && !ov.Required)
                AddError(errors, label, "Override relaxes required field to optional");

            // nullable: may remove, may not add.
            if (!baseField.Nullable && ov.Nullable)
                AddError(errors, label, "Override adds nullability");

            // bounds: may only narrow.
            var baseBounds = FindBounds(baseField.Constraints);
            var ovBounds = FindBounds(ov.Constraints);
            if (baseBounds != null && ovBounds != null && WidensBounds(baseBounds, ovBounds))
                AddError(errors, label, "Override widens constraint bounds");
        }

        private static void ValidateIntersectionConflicts(
            OdinSchemaDefinition schema, TypeRegistry? registry, string typeName,
            List<string> memberNames, List<ValidationError> errors)
        {
            var seen = new Dictionary<string, (string Member, SchemaField Field)>();
            foreach (var memberName in memberNames)
            {
                var member = LookupBaseType(schema, registry, memberName);
                if (member == null) continue;
                foreach (var f in member.SchemaFields)
                {
                    if (f.Name == "_composition") continue;
                    if (seen.TryGetValue(f.Name, out var prior))
                    {
                        if (!SameFieldDefinition(prior.Field, f))
                            AddError(errors,
                                string.Format(CultureInfo.InvariantCulture, "@{0}.{1}", typeName, f.Name),
                                string.Format(CultureInfo.InvariantCulture,
                                    "Intersection field conflict: '{0}' differs between @{1} and @{2}",
                                    f.Name, prior.Member, memberName));
                    }
                    else
                    {
                        seen[f.Name] = (memberName, f);
                    }
                }
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Path-level compositions ({path} = @base :override)
        // ─────────────────────────────────────────────────────────────────────

        private static void ValidatePathCompositions(
            OdinSchemaDefinition schema, TypeRegistry? registry, List<ValidationError> errors)
        {
            foreach (var kvp in schema.Fields)
            {
                var key = kvp.Key;
                if (!(key.EndsWith("._composition", StringComparison.Ordinal) || key == "_composition")) continue;
                if (kvp.Value.FieldType is not TypeRefFieldType typeRef) continue;

                var parentPath = key.Length > "_composition".Length
                    ? key.Substring(0, key.Length - "._composition".Length)
                    : "";
                var memberNames = SplitMembers(typeRef.Name);

                if (typeRef.Override)
                {
                    var baseFields = new Dictionary<string, SchemaField>();
                    foreach (var baseName in memberNames)
                    {
                        var b = LookupBaseType(schema, registry, baseName);
                        if (b == null) continue;
                        foreach (var f in b.SchemaFields)
                            if (f.Name != "_composition") baseFields[f.Name] = f;
                    }
                    foreach (var fieldKvp in schema.Fields)
                    {
                        var fp = fieldKvp.Key;
                        if (!fp.StartsWith(parentPath + ".", StringComparison.Ordinal)
                            || fp.EndsWith("._composition", StringComparison.Ordinal)) continue;
                        var localName = fp.Substring(parentPath.Length + 1);
                        if (localName.Contains(".")) continue;
                        if (!baseFields.TryGetValue(localName, out var bf)) continue;
                        CheckOverrideField(fp, bf, fieldKvp.Value, errors);
                    }
                }
                else if (memberNames.Count > 1)
                {
                    ValidateIntersectionConflicts(schema, registry, parentPath, memberNames, errors);
                }
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Tabular column rules
        // ─────────────────────────────────────────────────────────────────────

        private static void ValidateTabularColumns(
            OdinSchemaDefinition schema, TypeRegistry? registry, List<ValidationError> errors)
        {
            foreach (var kvp in schema.Arrays)
            {
                var arrayPath = kvp.Key;
                var array = kvp.Value;
                if (array.Columns.Count == 0) continue;

                foreach (var column in array.Columns)
                {
                    var label = string.Format(CultureInfo.InvariantCulture, "{0}[].{1}", arrayPath, column);

                    if (IsMultiLevelColumn(column))
                    {
                        AddError(errors, label, "Tabular column uses multi-level path");
                        continue;
                    }

                    var itemName = StripIndex(column);
                    // Array item fields are stored as "arrayPath[].fieldName".
                    if (!schema.Fields.TryGetValue(arrayPath + "[]." + itemName, out var field)
                        && !schema.Fields.TryGetValue(arrayPath + "[]." + column, out field))
                        continue;

                    if (!IsPrimitiveColumnType(schema, registry, field.FieldType))
                        AddError(errors, label, "Tabular column must be a primitive type");
                }
            }
        }

        private static bool IsMultiLevelColumn(string column)
        {
            int dotCount = 0, indexCount = 0;
            for (int i = 0; i < column.Length; i++)
                if (column[i] == '.') dotCount++;
            int idx = 0;
            while ((idx = column.IndexOf('[', idx)) >= 0)
            {
                int close = column.IndexOf(']', idx);
                if (close < 0) break;
                bool allDigits = close > idx + 1;
                for (int j = idx + 1; j < close; j++)
                    if (!char.IsDigit(column[j])) { allDigits = false; break; }
                if (allDigits) indexCount++;
                idx = close + 1;
            }
            if (dotCount > 1 || indexCount > 1) return true;
            if (dotCount == 1 && indexCount == 1) return true;
            return false;
        }

        private static string StripIndex(string column)
        {
            int bracket = column.IndexOf('[');
            if (bracket < 0) return column;
            int close = column.IndexOf(']', bracket);
            if (close == column.Length - 1)
            {
                bool allDigits = close > bracket + 1;
                for (int j = bracket + 1; j < close; j++)
                    if (!char.IsDigit(column[j])) { allDigits = false; break; }
                if (allDigits) return column.Substring(0, bracket);
            }
            return column;
        }

        private static bool IsPrimitiveColumnType(
            OdinSchemaDefinition schema, TypeRegistry? registry, SchemaFieldType type)
        {
            if (type is TypeRefFieldType) return false;
            if (type is UnionFieldType union)
            {
                foreach (var t in union.Types)
                    if (!IsPrimitiveColumnType(schema, registry, t)) return false;
                return true;
            }
            // A reference whose target names a defined type is a type ref, not a primitive.
            if (type is ReferenceFieldType) return false;
            return PrimitiveTypes.Contains(type.GetType().Name);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Default value rules
        // ─────────────────────────────────────────────────────────────────────

        private static void ValidateDefaults(OdinSchemaDefinition schema, List<ValidationError> errors)
        {
            foreach (var kvp in schema.Fields)
            {
                if (kvp.Key.EndsWith("._composition", StringComparison.Ordinal) || kvp.Key == "_composition")
                    continue;
                CheckDefault(kvp.Key, kvp.Value, errors);
            }
            foreach (var kvp in schema.Types)
            {
                foreach (var f in kvp.Value.SchemaFields)
                {
                    if (f.Name == "_composition") continue;
                    CheckDefault(string.Format(CultureInfo.InvariantCulture, "@{0}.{1}", kvp.Value.Name, f.Name), f, errors);
                }
            }
        }

        private static void CheckDefault(string label, SchemaField field, List<ValidationError> errors)
        {
            if (field.TypedDefault == null) return;

            if (field.Required)
            {
                AddError(errors, label, "Required field cannot have a default value");
                return;
            }

            if (!DefaultSatisfiesConstraints(field, field.TypedDefault))
                AddError(errors, label, "Default value violates field constraints");
        }

        private static bool DefaultSatisfiesConstraints(SchemaField field, SchemaDefaultValue value)
        {
            foreach (var c in field.Constraints)
            {
                if (c is BoundsConstraint bounds)
                {
                    if (!BoundsSatisfied(bounds, value)) return false;
                }
                else if (c is EnumConstraint enumC)
                {
                    if (value.Type != "string" || value.Text == null || !enumC.Values.Contains(value.Text))
                        return false;
                }
                else if (c is PatternConstraint pat)
                {
                    if (value.Type == "string" && value.Text != null)
                    {
                        try
                        {
                            if (!System.Text.RegularExpressions.Regex.IsMatch(value.Text, pat.PatternValue))
                                return false;
                        }
                        catch (ArgumentException) { /* invalid pattern handled elsewhere */ }
                    }
                }
            }
            if (field.FieldType is EnumFieldType ef)
            {
                if (value.Type != "string" || value.Text == null || !ef.Values.Contains(value.Text))
                    return false;
            }
            return true;
        }

        private static bool BoundsSatisfied(BoundsConstraint c, SchemaDefaultValue value)
        {
            double? target = null;
            if (value.Number.HasValue) target = value.Number.Value;
            else if (value.Type == "string" && value.Text != null) target = value.Text.Length;
            if (!target.HasValue) return true;

            if (c.Min != null && double.TryParse(c.Min, NumberStyles.Float, CultureInfo.InvariantCulture, out var min)
                && target.Value < min) return false;
            if (c.Max != null && double.TryParse(c.Max, NumberStyles.Float, CultureInfo.InvariantCulture, out var max)
                && target.Value > max) return false;
            return true;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────────────────────────────

        private static SchemaFieldType? FindComposition(List<SchemaField> fields)
        {
            foreach (var f in fields)
                if (f.Name == "_composition") return f.FieldType;
            return null;
        }

        private static List<string> SplitMembers(string name)
        {
            var members = new List<string>();
            foreach (var part in name.Split('&'))
            {
                var p = part.Trim();
                if (p.Length > 0) members.Add(p);
            }
            return members;
        }

        private static bool SameBaseType(SchemaFieldType a, SchemaFieldType b)
        {
            return a.GetType() == b.GetType();
        }

        private static BoundsConstraint? FindBounds(List<SchemaConstraint> constraints)
        {
            foreach (var c in constraints)
                if (c is BoundsConstraint b) return b;
            return null;
        }

        private static bool WidensBounds(BoundsConstraint baseB, BoundsConstraint ov)
        {
            // min: override may only raise (narrow). Removing or lowering min widens.
            if (TryNum(baseB.Min, out var baseMin))
            {
                if (!TryNum(ov.Min, out var ovMin) || ovMin < baseMin) return true;
            }
            // max: override may only lower (narrow). Removing or raising max widens.
            if (TryNum(baseB.Max, out var baseMax))
            {
                if (!TryNum(ov.Max, out var ovMax) || ovMax > baseMax) return true;
            }
            return false;
        }

        private static bool TryNum(string? s, out double value)
        {
            value = 0;
            return s != null && double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        private static bool SameFieldDefinition(SchemaField a, SchemaField b)
        {
            if (a.FieldType.GetType() != b.FieldType.GetType()) return false;
            if (a.Required != b.Required) return false;
            if (a.Nullable != b.Nullable) return false;
            if (a.Constraints.Count != b.Constraints.Count) return false;
            for (int i = 0; i < a.Constraints.Count; i++)
                if (!ConstraintEquals(a.Constraints[i], b.Constraints[i])) return false;
            return true;
        }

        private static bool ConstraintEquals(SchemaConstraint a, SchemaConstraint b)
        {
            if (a.GetType() != b.GetType()) return false;
            switch (a)
            {
                case BoundsConstraint ab when b is BoundsConstraint bb:
                    return ab.Min == bb.Min && ab.Max == bb.Max
                        && ab.MinExclusive == bb.MinExclusive && ab.MaxExclusive == bb.MaxExclusive;
                case PatternConstraint ap when b is PatternConstraint bp:
                    return ap.PatternValue == bp.PatternValue;
                case EnumConstraint ae when b is EnumConstraint be:
                    if (ae.Values.Count != be.Values.Count) return false;
                    for (int i = 0; i < ae.Values.Count; i++)
                        if (ae.Values[i] != be.Values[i]) return false;
                    return true;
                case FormatConstraint af when b is FormatConstraint bf:
                    return af.FormatName == bf.FormatName;
                case SizeConstraint asz when b is SizeConstraint bsz:
                    return asz.Min == bsz.Min && asz.Max == bsz.Max;
                default:
                    return true;
            }
        }
    }
}
