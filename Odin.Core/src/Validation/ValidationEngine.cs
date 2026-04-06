using System;
using System.Collections.Generic;
using System.Globalization;
using Odin.Core.Types;

namespace Odin.Core.Validation
{
    /// <summary>
    /// Validates an <see cref="OdinDocument"/> against an <see cref="OdinSchemaDefinition"/>.
    /// Implements all V001-V013 error codes. Supports strict mode, reference validation,
    /// conditional requirements, and type composition expansion.
    /// </summary>
    public static class ValidationEngine
    {
        private const int MaxCircularRefDepth = 100;
        private static readonly char[] LineSeparators = { '\r', '\n' };

        /// <summary>
        /// Validate a document against a schema.
        /// </summary>
        /// <param name="doc">The document to validate.</param>
        /// <param name="schema">The schema to validate against.</param>
        /// <param name="options">Optional validation options.</param>
        /// <returns>A <see cref="ValidationResult"/> with any errors found.</returns>
        public static ValidationResult Validate(
            OdinDocument doc,
            OdinSchemaDefinition schema,
            ValidateOptions? options = null)
        {
            var opts = options ?? ValidateOptions.Default;
            var errors = new List<ValidationError>();

            // 0. Expand type composition (merge base type fields into derived types)
            schema = ExpandTypeComposition(schema);

            // 1. Validate fields defined in schema (skip array element templates)
            foreach (var kvp in schema.Fields)
            {
                // Skip array element template fields like "items[].name"
                if (kvp.Key.Contains("[]."))
                    continue;
                ValidateField(doc, kvp.Key, kvp.Value, opts, errors);
                if (opts.FailFast && errors.Count > 0)
                    return ValidationResult.WithErrors(errors);
            }

            // 2. Validate type definitions' fields for sections that match
            foreach (var typeKvp in schema.Types)
            {
                var typeName = typeKvp.Key;
                var schemaType = typeKvp.Value;
                foreach (var field in schemaType.SchemaFields)
                {
                    var path = typeName + "." + field.Name;
                    if (doc.Has(path))
                    {
                        ValidateField(doc, path, field, opts, errors);
                        if (opts.FailFast && errors.Count > 0)
                            return ValidationResult.WithErrors(errors);
                    }
                }
            }

            // 3. Validate array constraints
            foreach (var kvp in schema.Arrays)
            {
                ValidateArray(doc, kvp.Key, kvp.Value, errors);
                if (opts.FailFast && errors.Count > 0)
                    return ValidationResult.WithErrors(errors);
            }

            // 4. Validate object-level constraints
            foreach (var kvp in schema.Constraints)
            {
                foreach (var constraint in kvp.Value)
                {
                    ValidateObjectConstraint(doc, kvp.Key, constraint, errors);
                    if (opts.FailFast && errors.Count > 0)
                        return ValidationResult.WithErrors(errors);
                }
            }

            // 5. Validate references (V012/V013)
            if (opts.ValidateReferences)
            {
                ValidateReferences(doc, errors);
                ValidateSchemaReferences(schema, errors);
            }

            // 6. Strict mode: check for unknown fields
            if (opts.Strict)
                ValidateStrict(doc, schema, errors);

            return errors.Count == 0
                ? ValidationResult.Valid()
                : ValidationResult.WithErrors(errors);
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Field Validation
        // ─────────────────────────────────────────────────────────────────────────

        private static void ValidateField(
            OdinDocument doc,
            string path,
            SchemaField field,
            ValidateOptions opts,
            List<ValidationError> errors)
        {
            var value = doc.Get(path);

            // Required check (V001 / V010)
            if (field.Required && value == null)
            {
                if (field.Conditionals.Count == 0)
                {
                    errors.Add(new ValidationError(
                        ValidationErrorCode.RequiredFieldMissing,
                        path,
                        string.Format(CultureInfo.InvariantCulture, "Required field '{0}' is missing", field.Name)));
                }
                else
                {
                    bool shouldBeRequired = false;
                    foreach (var cond in field.Conditionals)
                    {
                        int lastDot = path.LastIndexOf('.');
                        var parentPath = lastDot >= 0 ? path.Substring(0, lastDot) : "";
                        var condFieldPath = cond.Field.IndexOf('.') >= 0 || parentPath.Length == 0
                            ? cond.Field
                            : parentPath + "." + cond.Field;
                        var condValue = doc.Get(condFieldPath);
                        bool matches = MatchesConditionValue(condValue!, cond.Operator, cond.CondValue);
                        if (cond.Unless ? !matches : matches)
                        {
                            shouldBeRequired = true;
                            break;
                        }
                    }
                    if (shouldBeRequired)
                    {
                        errors.Add(new ValidationError(
                            ValidationErrorCode.ConditionalRequirementNotMet,
                            path,
                            string.Format(CultureInfo.InvariantCulture,
                                "Conditional requirement not met: field '{0}' is required", field.Name)));
                    }
                }
                return;
            }

            if (value == null) return; // Optional field not present — ok

            // Null check for required fields (V002)
            if (field.Required && value.IsNull)
            {
                errors.Add(new ValidationError(
                    ValidationErrorCode.TypeMismatch,
                    path,
                    string.Format(CultureInfo.InvariantCulture,
                        "Required field '{0}' cannot be null", field.Name)));
                return;
            }

            // Type check (V002)
            if (!CheckTypeMatch(value, field.FieldType))
            {
                errors.Add(new ValidationError(
                    ValidationErrorCode.TypeMismatch,
                    path,
                    string.Format(CultureInfo.InvariantCulture,
                        "Expected type {0}, got {1}",
                        field.FieldType.GetType().Name,
                        ValueTypeName(value)))
                {
                    Expected = field.FieldType.GetType().Name,
                    Actual = ValueTypeName(value),
                });
                return;
            }

            // Constraint validation
            foreach (var constraint in field.Constraints)
            {
                ValidateConstraint(value, path, constraint, errors);
            }
        }

        /// <summary>
        /// Check if an <see cref="OdinValue"/> matches a <see cref="SchemaFieldType"/>.
        /// </summary>
        internal static bool CheckTypeMatch(OdinValue value, SchemaFieldType expected)
        {
            // Null is allowed unless required (checked separately)
            if (value.IsNull) return true;

            if (value is OdinString && expected is StringFieldType) return true;
            // Date/time types are valid for string fields (e.g., :format date-iso)
            if (value is OdinDate && expected is StringFieldType) return true;
            if (value is OdinTimestamp && expected is StringFieldType) return true;
            if (value is OdinTime && expected is StringFieldType) return true;
            if (value is OdinBoolean && expected is BooleanFieldType) return true;
            if (value is OdinInteger && expected is IntegerFieldType) return true;
            if (value is OdinInteger && expected is NumberFieldType) return true; // int is a number
            if (value is OdinNumber && expected is NumberFieldType) return true;
            if (value is OdinNumber && expected is DecimalFieldType) return true;
            if (value is OdinCurrency && expected is CurrencyFieldType) return true;
            if (value is OdinPercent && expected is PercentFieldType) return true;
            if (value is OdinDate && expected is DateFieldType) return true;
            if (value is OdinTimestamp && expected is TimestampFieldType) return true;
            if (value is OdinTime && expected is TimeFieldType) return true;
            if (value is OdinDuration && expected is DurationFieldType) return true;
            if (value is OdinBinary && expected is BinaryFieldType) return true;
            if (value is OdinReference && expected is ReferenceFieldType) return true;
            if (value is OdinString && expected is EnumFieldType) return true; // checked separately
            if (expected is TypeRefFieldType) return true; // can't resolve without registry

            if (expected is UnionFieldType union)
            {
                foreach (var member in union.Types)
                {
                    if (CheckTypeMatch(value, member))
                        return true;
                }
                return false;
            }

            return false;
        }

        private static string ValueTypeName(OdinValue value)
        {
            switch (value.Type)
            {
                case OdinValueType.Null: return "null";
                case OdinValueType.Boolean: return "boolean";
                case OdinValueType.String: return "string";
                case OdinValueType.Integer: return "integer";
                case OdinValueType.Number: return "number";
                case OdinValueType.Currency: return "currency";
                case OdinValueType.Percent: return "percent";
                case OdinValueType.Date: return "date";
                case OdinValueType.Timestamp: return "timestamp";
                case OdinValueType.Time: return "time";
                case OdinValueType.Duration: return "duration";
                case OdinValueType.Reference: return "reference";
                case OdinValueType.Binary: return "binary";
                case OdinValueType.Verb: return "verb";
                case OdinValueType.Array: return "array";
                case OdinValueType.Object: return "object";
                default: return "unknown";
            }
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Constraint Validation
        // ─────────────────────────────────────────────────────────────────────────

        private static void ValidateConstraint(
            OdinValue value,
            string path,
            SchemaConstraint constraint,
            List<ValidationError> errors)
        {
            if (constraint is BoundsConstraint bounds)
                ValidateBounds(value, path, bounds, errors);
            else if (constraint is PatternConstraint pattern)
                ValidatePattern(value, path, pattern.PatternValue, errors);
            else if (constraint is EnumConstraint enumC)
                ValidateEnum(value, path, enumC.Values, errors);
            else if (constraint is FormatConstraint format)
                ValidateFormat(value, path, format.FormatName, errors);
            else if (constraint is SizeConstraint size)
                ValidateSize(value, path, size, errors);
            // UniqueConstraint is validated at array level
        }

        private static void ValidateBounds(
            OdinValue value,
            string path,
            BoundsConstraint bounds,
            List<ValidationError> errors)
        {
            // Numeric comparison
            var num = value.AsDouble();
            if (num.HasValue)
            {
                if (bounds.Min != null &&
                    double.TryParse(bounds.Min, NumberStyles.Float, CultureInfo.InvariantCulture, out var minVal))
                {
                    bool fail = bounds.MinExclusive ? num.Value <= minVal : num.Value < minVal;
                    if (fail)
                    {
                        errors.Add(new ValidationError(
                            ValidationErrorCode.ValueOutOfBounds,
                            path,
                            string.Format(CultureInfo.InvariantCulture,
                                "Value {0} is below minimum {1}", num.Value, bounds.Min)));
                        return;
                    }
                }
                if (bounds.Max != null &&
                    double.TryParse(bounds.Max, NumberStyles.Float, CultureInfo.InvariantCulture, out var maxVal))
                {
                    bool fail = bounds.MaxExclusive ? num.Value >= maxVal : num.Value > maxVal;
                    if (fail)
                    {
                        errors.Add(new ValidationError(
                            ValidationErrorCode.ValueOutOfBounds,
                            path,
                            string.Format(CultureInfo.InvariantCulture,
                                "Value {0} is above maximum {1}", num.Value, bounds.Max)));
                    }
                }
                return;
            }

            // String length comparison
            var str = value.AsString();
            if (str != null)
            {
                int len = str.Length;
                if (bounds.Min != null &&
                    int.TryParse(bounds.Min, NumberStyles.Integer, CultureInfo.InvariantCulture, out var minLen))
                {
                    bool fail = bounds.MinExclusive ? len <= minLen : len < minLen;
                    if (fail)
                    {
                        errors.Add(new ValidationError(
                            ValidationErrorCode.ValueOutOfBounds,
                            path,
                            string.Format(CultureInfo.InvariantCulture,
                                "String length {0} is below minimum {1}", len, bounds.Min)));
                        return;
                    }
                }
                if (bounds.Max != null &&
                    int.TryParse(bounds.Max, NumberStyles.Integer, CultureInfo.InvariantCulture, out var maxLen))
                {
                    bool fail = bounds.MaxExclusive ? len >= maxLen : len > maxLen;
                    if (fail)
                    {
                        errors.Add(new ValidationError(
                            ValidationErrorCode.ValueOutOfBounds,
                            path,
                            string.Format(CultureInfo.InvariantCulture,
                                "String length {0} is above maximum {1}", len, bounds.Max)));
                    }
                }
            }
        }

        private static void ValidatePattern(
            OdinValue value,
            string path,
            string pattern,
            List<ValidationError> errors)
        {
            // ReDoS safety check
            var redosCheck = ReDoSProtection.AnalyzePattern(pattern);
            if (!redosCheck.Safe)
            {
                errors.Add(new ValidationError(
                    ValidationErrorCode.PatternMismatch,
                    path,
                    string.Format(CultureInfo.InvariantCulture,
                        "Unsafe regex pattern: {0}", redosCheck.Reason ?? "")));
                return;
            }

            var str = value.AsString();
            if (str != null)
            {
                try
                {
                    var regex = new System.Text.RegularExpressions.Regex(
                        pattern,
                        System.Text.RegularExpressions.RegexOptions.None,
                        TimeSpan.FromSeconds(1));
                    if (!regex.IsMatch(str))
                    {
                        errors.Add(new ValidationError(
                            ValidationErrorCode.PatternMismatch,
                            path,
                            string.Format(CultureInfo.InvariantCulture,
                                "Value '{0}' does not match pattern '{1}'", str, pattern)));
                    }
                }
                catch (ArgumentException)
                {
                    errors.Add(new ValidationError(
                        ValidationErrorCode.PatternMismatch,
                        path,
                        string.Format(CultureInfo.InvariantCulture,
                            "Invalid regex pattern: '{0}'", pattern)));
                }
                catch (System.Text.RegularExpressions.RegexMatchTimeoutException)
                {
                    errors.Add(new ValidationError(
                        ValidationErrorCode.PatternMismatch,
                        path,
                        string.Format(CultureInfo.InvariantCulture,
                            "Regex pattern timed out: '{0}'", pattern)));
                }
            }
        }

        private static void ValidateEnum(
            OdinValue value,
            string path,
            List<string> allowed,
            List<ValidationError> errors)
        {
            var str = value.AsString();
            if (str != null && !allowed.Contains(str))
            {
                errors.Add(new ValidationError(
                    ValidationErrorCode.InvalidEnumValue,
                    path,
                    string.Format(CultureInfo.InvariantCulture,
                        "Value '{0}' is not one of allowed values: [{1}]",
                        str, string.Join(", ", allowed))));
            }
        }

        private static void ValidateFormat(
            OdinValue value,
            string path,
            string formatName,
            List<ValidationError> errors)
        {
            var str = value.AsString();
            if (str != null)
            {
                var result = FormatValidators.ValidateFormat(str, formatName);
                if (result != null && !result.IsValid)
                {
                    errors.Add(new ValidationError(
                        ValidationErrorCode.PatternMismatch,
                        path,
                        result.ErrorMessage));
                }
            }

            // Date values are valid for date-iso format
            if (value is OdinDate && formatName == "date-iso")
            {
                // Already a date — valid
            }
        }

        private static void ValidateSize(
            OdinValue value,
            string path,
            SizeConstraint size,
            List<ValidationError> errors)
        {
            if (value is OdinBinary bin)
            {
                long dataSize = bin.Data.Length;
                if (size.Min.HasValue && dataSize < size.Min.Value)
                {
                    errors.Add(new ValidationError(
                        ValidationErrorCode.ValueOutOfBounds,
                        path,
                        string.Format(CultureInfo.InvariantCulture,
                            "Binary size {0} is below minimum {1}", dataSize, size.Min.Value)));
                }
                if (size.Max.HasValue && dataSize > size.Max.Value)
                {
                    errors.Add(new ValidationError(
                        ValidationErrorCode.ValueOutOfBounds,
                        path,
                        string.Format(CultureInfo.InvariantCulture,
                            "Binary size {0} is above maximum {1}", dataSize, size.Max.Value)));
                }
            }
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Array Validation
        // ─────────────────────────────────────────────────────────────────────────

        private static void ValidateArray(
            OdinDocument doc,
            string path,
            SchemaArray arrayDef,
            List<ValidationError> errors)
        {
            var prefix = path + "[";
            int? maxIndex = null;

            foreach (var key in doc.Assignments.Keys)
            {
                if (key.StartsWith(prefix, StringComparison.Ordinal))
                {
                    var afterPrefix = key.Substring(prefix.Length);
                    int bracketEnd = afterPrefix.IndexOf(']');
                    if (bracketEnd >= 0)
                    {
                        var indexStr = afterPrefix.Substring(0, bracketEnd);
                        if (int.TryParse(indexStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var idx))
                        {
                            if (!maxIndex.HasValue || idx > maxIndex.Value)
                                maxIndex = idx;
                        }
                    }
                }
            }

            int count = maxIndex.HasValue ? maxIndex.Value + 1 : 0;

            // Min items (V006)
            if (arrayDef.MinItems.HasValue && count < arrayDef.MinItems.Value)
            {
                errors.Add(new ValidationError(
                    ValidationErrorCode.ArrayLengthViolation,
                    path,
                    string.Format(CultureInfo.InvariantCulture,
                        "Array has {0} items, minimum is {1}", count, arrayDef.MinItems.Value)));
            }

            // Max items (V006)
            if (arrayDef.MaxItems.HasValue && count > arrayDef.MaxItems.Value)
            {
                errors.Add(new ValidationError(
                    ValidationErrorCode.ArrayLengthViolation,
                    path,
                    string.Format(CultureInfo.InvariantCulture,
                        "Array has {0} items, maximum is {1}", count, arrayDef.MaxItems.Value)));
            }

            // Unique check (V007) — compare serialized values of array elements
            if (arrayDef.IsUnique && count > 1)
            {
                var seen = new HashSet<string>();
                for (int i = 0; i < count; i++)
                {
                    var itemPrefix = string.Format(CultureInfo.InvariantCulture, "{0}[{1}]", path, i);
                    var fingerprint = new System.Text.StringBuilder();

                    // Try direct value first
                    var directVal = doc.Get(itemPrefix);
                    if (directVal != null)
                    {
                        fingerprint.Append(directVal.ToString() ?? "");
                    }
                    else
                    {
                        // Collect sub-fields of this array element
                        var subPrefix = itemPrefix + ".";
                        foreach (var key in doc.Assignments.Keys)
                        {
                            if (key.StartsWith(subPrefix, StringComparison.Ordinal))
                            {
                                var subVal = doc.Assignments[key];
                                fingerprint.Append(key.Substring(subPrefix.Length));
                                fingerprint.Append('=');
                                fingerprint.Append(subVal?.ToString() ?? "");
                                fingerprint.Append(';');
                            }
                        }
                    }

                    var fp = fingerprint.ToString();
                    if (fp.Length > 0 && !seen.Add(fp))
                    {
                        errors.Add(new ValidationError(
                            ValidationErrorCode.UniqueConstraintViolation,
                            path,
                            string.Format(CultureInfo.InvariantCulture,
                                "Duplicate item at index {0}", i)));
                        break;
                    }
                }
            }
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Object-Level Constraints
        // ─────────────────────────────────────────────────────────────────────────

        private static void ValidateObjectConstraint(
            OdinDocument doc,
            string path,
            SchemaObjectConstraint constraint,
            List<ValidationError> errors)
        {
            if (constraint is InvariantConstraint inv)
                ValidateInvariant(doc, path, inv.Expression, errors);
            else if (constraint is CardinalityConstraint card)
                ValidateCardinality(doc, path, card, errors);
        }

        private static void ValidateInvariant(
            OdinDocument doc,
            string path,
            string expr,
            List<ValidationError> errors)
        {
            // Expression evaluator: supports field refs, arithmetic, comparisons
            string[] ops = { ">=", "<=", "!=", "==", ">", "<", "=" };
            foreach (var op in ops)
            {
                int pos;
                if (op == "=")
                {
                    // For bare '=', must not be preceded by !, <, > and not followed by =
                    pos = -1;
                    for (int i = 0; i < expr.Length; i++)
                    {
                        if (expr[i] == '=' &&
                            (i == 0 || (expr[i - 1] != '!' && expr[i - 1] != '<' && expr[i - 1] != '>')) &&
                            (i + 1 >= expr.Length || expr[i + 1] != '='))
                        {
                            pos = i;
                            break;
                        }
                    }
                }
                else
                {
                    pos = expr.IndexOf(op, StringComparison.Ordinal);
                }

                if (pos >= 0)
                {
                    var lhsExpr = expr.Substring(0, pos).Trim();
                    var rhsExpr = expr.Substring(pos + op.Length).Trim();

                    var lhsVal = ResolveInvariantExpr(doc, path, lhsExpr);
                    var rhsVal = ResolveInvariantExpr(doc, path, rhsExpr);

                    if (lhsVal.HasValue && rhsVal.HasValue)
                    {
                        string effectiveOp = op == "=" ? "==" : op;
                        bool passes;
                        switch (effectiveOp)
                        {
                            case ">": passes = lhsVal.Value > rhsVal.Value; break;
                            case "<": passes = lhsVal.Value < rhsVal.Value; break;
                            case ">=": passes = lhsVal.Value >= rhsVal.Value; break;
                            case "<=": passes = lhsVal.Value <= rhsVal.Value; break;
                            case "==": passes = Math.Abs(lhsVal.Value - rhsVal.Value) < 0.001; break;
                            case "!=": passes = Math.Abs(lhsVal.Value - rhsVal.Value) >= 0.001; break;
                            default: passes = true; break;
                        }
                        if (!passes)
                        {
                            errors.Add(new ValidationError(
                                ValidationErrorCode.InvariantViolation,
                                path,
                                string.Format(CultureInfo.InvariantCulture,
                                    "Invariant '{0}' violated", expr)));
                        }
                    }
                    return;
                }
            }
        }

        /// <summary>
        /// Resolve an invariant expression to a numeric value.
        /// Supports field references, literal numbers, and simple arithmetic (a + b, a - b).
        /// </summary>
        private static double? ResolveInvariantExpr(OdinDocument doc, string path, string expr)
        {
            expr = expr.Trim();

            // Try literal number
            if (double.TryParse(expr, NumberStyles.Float, CultureInfo.InvariantCulture, out var literal))
                return literal;

            // Try arithmetic: a + b or a - b
            int plusPos = expr.IndexOf(" + ", StringComparison.Ordinal);
            if (plusPos >= 0)
            {
                var left = ResolveInvariantExpr(doc, path, expr.Substring(0, plusPos));
                var right = ResolveInvariantExpr(doc, path, expr.Substring(plusPos + 3));
                if (left.HasValue && right.HasValue)
                    return left.Value + right.Value;
                return null;
            }
            int minusPos = expr.IndexOf(" - ", StringComparison.Ordinal);
            if (minusPos >= 0)
            {
                var left = ResolveInvariantExpr(doc, path, expr.Substring(0, minusPos));
                var right = ResolveInvariantExpr(doc, path, expr.Substring(minusPos + 3));
                if (left.HasValue && right.HasValue)
                    return left.Value - right.Value;
                return null;
            }

            // Field reference
            var fullPath = path.Length == 0 ? expr : path + "." + expr;
            var val = doc.Get(fullPath);
            return val?.AsDouble();
        }

        private static bool EvaluateComparison(OdinValue value, string op, string compare)
        {
            // Try numeric comparison
            var num = value.AsDouble();
            if (num.HasValue &&
                double.TryParse(compare, NumberStyles.Float, CultureInfo.InvariantCulture, out var cmpNum))
            {
                switch (op)
                {
                    case ">": return num.Value > cmpNum;
                    case "<": return num.Value < cmpNum;
                    case ">=": return num.Value >= cmpNum;
                    case "<=": return num.Value <= cmpNum;
                    case "==": return Math.Abs(num.Value - cmpNum) < double.Epsilon;
                    case "!=": return Math.Abs(num.Value - cmpNum) >= double.Epsilon;
                    default: return true;
                }
            }

            // Try string comparison
            var str = value.AsString();
            if (str != null)
            {
                var cmp = compare.Trim('"');
                int result = string.Compare(str, cmp, StringComparison.Ordinal);
                switch (op)
                {
                    case "==": return result == 0;
                    case "!=": return result != 0;
                    case ">": return result > 0;
                    case "<": return result < 0;
                    case ">=": return result >= 0;
                    case "<=": return result <= 0;
                    default: return true;
                }
            }

            return true; // Can't evaluate — skip
        }

        private static void ValidateCardinality(
            OdinDocument doc,
            string path,
            CardinalityConstraint card,
            List<ValidationError> errors)
        {
            int presentCount = 0;
            foreach (var f in card.Fields)
            {
                var fullPath = path.Length == 0 ? f : path + "." + f;
                if (doc.Has(fullPath))
                    presentCount++;
            }

            if (card.Min.HasValue && presentCount < card.Min.Value)
            {
                errors.Add(new ValidationError(
                    ValidationErrorCode.CardinalityConstraintViolation,
                    path,
                    string.Format(CultureInfo.InvariantCulture,
                        "At least {0} of [{1}] must be present, found {2}",
                        card.Min.Value, string.Join(", ", card.Fields), presentCount)));
            }

            if (card.Max.HasValue && presentCount > card.Max.Value)
            {
                errors.Add(new ValidationError(
                    ValidationErrorCode.CardinalityConstraintViolation,
                    path,
                    string.Format(CultureInfo.InvariantCulture,
                        "At most {0} of [{1}] may be present, found {2}",
                        card.Max.Value, string.Join(", ", card.Fields), presentCount)));
            }
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Type Composition Expansion
        // ─────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Expand type composition by merging parent type fields into derived types.
        /// For @Child : @Parent, the child type inherits all fields from the parent
        /// that it does not already define (child fields take precedence).
        /// </summary>
        internal static OdinSchemaDefinition ExpandTypeComposition(OdinSchemaDefinition schema)
        {
            var expandedTypes = new Dictionary<string, SchemaType>();
            foreach (var kvp in schema.Types)
            {
                expandedTypes[kvp.Key] = new SchemaType
                {
                    Name = kvp.Value.Name,
                    Description = kvp.Value.Description,
                    SchemaFields = new List<SchemaField>(kvp.Value.SchemaFields),
                    Parents = new List<string>(kvp.Value.Parents),
                };
            }

            foreach (var typeName in new List<string>(expandedTypes.Keys))
            {
                var schemaType = expandedTypes[typeName];
                if (schemaType.Parents.Count == 0)
                    continue;

                var inheritedFields = new List<SchemaField>();
                foreach (var parentName in schemaType.Parents)
                {
                    // Strip leading @ if present
                    var cleanName = parentName.Length > 0 && parentName[0] == '@'
                        ? parentName.Substring(1)
                        : parentName;

                    if (schema.Types.TryGetValue(cleanName, out var parentType))
                    {
                        foreach (var field in parentType.SchemaFields)
                            inheritedFields.Add(field);
                    }
                }

                // Merge: add inherited fields that don't conflict with existing child fields
                var existingNames = new HashSet<string>();
                foreach (var f in schemaType.SchemaFields)
                    existingNames.Add(f.Name);

                foreach (var field in inheritedFields)
                {
                    if (!existingNames.Contains(field.Name))
                        schemaType.SchemaFields.Add(field);
                }
            }

            return new OdinSchemaDefinition
            {
                Metadata = schema.Metadata,
                Imports = schema.Imports,
                Types = expandedTypes,
                Fields = schema.Fields,
                Arrays = schema.Arrays,
                Constraints = schema.Constraints,
            };
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Conditional Evaluation
        // ─────────────────────────────────────────────────────────────────────────

        private static bool MatchesConditionValue(
            OdinValue value,
            ConditionalOperator op,
            ConditionalValue expected)
        {
            if (value == null) return false;

            var expectedBool = expected.AsBool();
            if (expectedBool.HasValue)
            {
                var actual = value.AsBool();
                if (!actual.HasValue) return false;
                switch (op)
                {
                    case ConditionalOperator.Eq: return actual.Value == expectedBool.Value;
                    case ConditionalOperator.NotEq: return actual.Value != expectedBool.Value;
                    default: return false;
                }
            }

            var expectedNum = expected.AsNumber();
            if (expectedNum.HasValue)
            {
                var actual = value.AsDouble();
                if (!actual.HasValue) return false;
                switch (op)
                {
                    case ConditionalOperator.Eq: return Math.Abs(actual.Value - expectedNum.Value) < double.Epsilon;
                    case ConditionalOperator.NotEq: return Math.Abs(actual.Value - expectedNum.Value) >= double.Epsilon;
                    case ConditionalOperator.Gt: return actual.Value > expectedNum.Value;
                    case ConditionalOperator.Lt: return actual.Value < expectedNum.Value;
                    case ConditionalOperator.Gte: return actual.Value >= expectedNum.Value;
                    case ConditionalOperator.Lte: return actual.Value <= expectedNum.Value;
                    default: return false;
                }
            }

            var expectedStr = expected.AsString();
            if (expectedStr != null)
            {
                var actual = value.AsString();
                if (actual == null) return false;
                int cmp = string.Compare(actual, expectedStr, StringComparison.Ordinal);
                switch (op)
                {
                    case ConditionalOperator.Eq: return cmp == 0;
                    case ConditionalOperator.NotEq: return cmp != 0;
                    case ConditionalOperator.Gt: return cmp > 0;
                    case ConditionalOperator.Lt: return cmp < 0;
                    case ConditionalOperator.Gte: return cmp >= 0;
                    case ConditionalOperator.Lte: return cmp <= 0;
                    default: return false;
                }
            }

            return false;
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Reference Validation (V012/V013)
        // ─────────────────────────────────────────────────────────────────────────

        private static void ValidateReferences(
            OdinDocument doc,
            List<ValidationError> errors)
        {
            foreach (var kvp in doc.Assignments)
            {
                var path = kvp.Key;
                var value = kvp.Value;

                if (value is OdinReference refVal)
                {
                    // V013: Check target exists
                    if (doc.Get(refVal.Path) == null)
                    {
                        errors.Add(new ValidationError(
                            ValidationErrorCode.UnresolvedReference,
                            path,
                            string.Format(CultureInfo.InvariantCulture,
                                "Unresolved reference: @{0}", refVal.Path)));
                        continue;
                    }

                    // V012: Check for circular references
                    var visited = new HashSet<string> { path };
                    if (DetectCircularRef(doc, refVal.Path, visited, 0))
                    {
                        errors.Add(new ValidationError(
                            ValidationErrorCode.CircularReference,
                            path,
                            string.Format(CultureInfo.InvariantCulture,
                                "Circular reference detected: @{0}", refVal.Path)));
                    }
                }
            }
        }

        private static bool DetectCircularRef(
            OdinDocument doc,
            string targetPath,
            HashSet<string> visited,
            int depth)
        {
            if (depth > MaxCircularRefDepth)
                return true; // Treat deep chains as circular

            if (visited.Contains(targetPath))
                return true;

            visited.Add(targetPath);

            var targetValue = doc.Get(targetPath);
            if (targetValue is OdinReference nextRef)
                return DetectCircularRef(doc, nextRef.Path, visited, depth + 1);

            return false;
        }

        /// <summary>
        /// Validate type references in schema (V012 circular, V013 unresolved).
        /// </summary>
        private static void ValidateSchemaReferences(
            OdinSchemaDefinition schema,
            List<ValidationError> errors)
        {
            var typeNames = new HashSet<string>(schema.Types.Keys);

            // Check each type's fields for @TypeRef references
            foreach (var typeKvp in schema.Types)
            {
                var typeName = typeKvp.Key;
                foreach (var field in typeKvp.Value.SchemaFields)
                {
                    if (field.FieldType is TypeRefFieldType typeRef)
                    {
                        var refName = typeRef.Name;
                        if (!typeNames.Contains(refName))
                        {
                            // V013: Unresolved reference
                            errors.Add(new ValidationError(
                                ValidationErrorCode.UnresolvedReference,
                                typeName + "." + field.Name,
                                string.Format(CultureInfo.InvariantCulture,
                                    "Unresolved type reference: @{0}", refName)));
                        }
                    }
                }
            }

            // Check for top-level fields with @TypeRef
            foreach (var fieldKvp in schema.Fields)
            {
                if (fieldKvp.Value.FieldType is TypeRefFieldType typeRef)
                {
                    var refName = typeRef.Name;
                    if (!typeNames.Contains(refName))
                    {
                        errors.Add(new ValidationError(
                            ValidationErrorCode.UnresolvedReference,
                            fieldKvp.Key,
                            string.Format(CultureInfo.InvariantCulture,
                                "Unresolved type reference: @{0}", refName)));
                    }
                }
            }

            // V012: Detect circular type references
            foreach (var typeName in typeNames)
            {
                var visited = new HashSet<string>();
                if (DetectCircularTypeRef(schema, typeName, visited))
                {
                    errors.Add(new ValidationError(
                        ValidationErrorCode.CircularReference,
                        "@" + typeName,
                        string.Format(CultureInfo.InvariantCulture,
                            "Circular type reference detected: @{0}", typeName)));
                    break; // Report once
                }
            }
        }

        private static bool DetectCircularTypeRef(
            OdinSchemaDefinition schema,
            string typeName,
            HashSet<string> visited)
        {
            if (visited.Contains(typeName))
                return true;
            visited.Add(typeName);

            if (!schema.Types.TryGetValue(typeName, out var schemaType))
                return false;

            foreach (var field in schemaType.SchemaFields)
            {
                if (field.FieldType is TypeRefFieldType typeRef)
                {
                    if (DetectCircularTypeRef(schema, typeRef.Name, new HashSet<string>(visited)))
                        return true;
                }
            }

            return false;
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Strict Mode
        // ─────────────────────────────────────────────────────────────────────────

        private static void ValidateStrict(
            OdinDocument doc,
            OdinSchemaDefinition schema,
            List<ValidationError> errors)
        {
            foreach (var path in doc.Assignments.Keys)
            {
                // Check if path is known in schema fields
                if (schema.Fields.ContainsKey(path))
                    continue;

                // Check if path is part of an array
                bool isArrayItem = false;
                foreach (var arrPath in schema.Arrays.Keys)
                {
                    if (path.StartsWith(arrPath + "[", StringComparison.Ordinal))
                    {
                        isArrayItem = true;
                        break;
                    }
                }
                if (isArrayItem) continue;

                // Check if path matches a type field
                bool isTypeField = false;
                foreach (var schemaType in schema.Types.Values)
                {
                    foreach (var field in schemaType.SchemaFields)
                    {
                        if (path.EndsWith("." + field.Name, StringComparison.Ordinal))
                        {
                            isTypeField = true;
                            break;
                        }
                    }
                    if (isTypeField) break;
                }
                if (isTypeField) continue;

                errors.Add(new ValidationError(
                    ValidationErrorCode.UnknownField,
                    path,
                    string.Format(CultureInfo.InvariantCulture,
                        "Unknown field '{0}' not defined in schema", path)));
            }
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Format Schema Validation (convenience)
        // ─────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Validate a document against a simple format schema.
        /// Schema format: fieldName = :format formatName per line.
        /// </summary>
        /// <param name="doc">The document to validate.</param>
        /// <param name="schemaText">Simple format schema text.</param>
        /// <returns>A list of (fieldPath, errorMessage) tuples for validation failures.</returns>
        public static List<KeyValuePair<string, string>> ValidateFormats(OdinDocument doc, string schemaText)
        {
            var constraints = ParseFormatSchema(schemaText);
            var formatErrors = new List<KeyValuePair<string, string>>();

            foreach (var constraint in constraints)
            {
                OdinValue? value;
                if (!doc.Assignments.TryGetValue(constraint.Key, out value) || value == null)
                    continue;

                if (value is OdinString strVal)
                {
                    var result = FormatValidators.ValidateFormat(strVal.Value, constraint.Value);
                    if (result != null && !result.IsValid)
                    {
                        formatErrors.Add(new KeyValuePair<string, string>(constraint.Key, result.ErrorMessage));
                    }
                }
                else if (value is OdinDate && constraint.Value == "date-iso")
                {
                    // Date values are inherently valid for date-iso format
                }
            }

            return formatErrors;
        }

        private static List<KeyValuePair<string, string>> ParseFormatSchema(string schemaText)
        {
            var constraints = new List<KeyValuePair<string, string>>();
            foreach (var line in schemaText.Split(LineSeparators, StringSplitOptions.None))
            {
                var trimmed = line.Trim();
                if (trimmed.Length == 0 || trimmed[0] == ';')
                    continue;

                int formatPos = trimmed.IndexOf(":format ", StringComparison.Ordinal);
                if (formatPos >= 0)
                {
                    int eqPos = trimmed.IndexOf('=');
                    if (eqPos >= 0)
                    {
                        var field = trimmed.Substring(0, eqPos).Trim();
                        var format = trimmed.Substring(formatPos + 8).Trim();
                        if (field.Length > 0 && format.Length > 0)
                            constraints.Add(new KeyValuePair<string, string>(field, format));
                    }
                }
            }
            return constraints;
        }
    }
}
