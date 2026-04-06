using System.Collections.Generic;

namespace Odin.Core.Types;

/// <summary>A parsed ODIN schema definition.</summary>
public sealed class OdinSchemaDefinition
{
    /// <summary>Schema metadata.</summary>
    public SchemaMetadata Metadata { get; set; } = new();

    /// <summary>Import directives.</summary>
    public List<SchemaImport> Imports { get; set; } = new();

    /// <summary>Named type definitions.</summary>
    public Dictionary<string, SchemaType> Types { get; set; } = new();

    /// <summary>Top-level field definitions.</summary>
    public Dictionary<string, SchemaField> Fields { get; set; } = new();

    /// <summary>Array definitions.</summary>
    public Dictionary<string, SchemaArray> Arrays { get; set; } = new();

    /// <summary>Object-level constraints.</summary>
    public Dictionary<string, List<SchemaObjectConstraint>> Constraints { get; set; } = new();
}

/// <summary>Schema metadata from the {$} header.</summary>
public sealed class SchemaMetadata
{
    /// <summary>Schema identifier.</summary>
    public string? Id { get; set; }

    /// <summary>Human-readable title.</summary>
    public string? Title { get; set; }

    /// <summary>Schema description.</summary>
    public string? Description { get; set; }

    /// <summary>Schema version.</summary>
    public string? Version { get; set; }
}

/// <summary>An import in a schema file.</summary>
public sealed class SchemaImport
{
    /// <summary>Import file path.</summary>
    public string Path { get; set; } = "";

    /// <summary>Optional alias.</summary>
    public string? Alias { get; set; }
}

/// <summary>A named type definition in a schema.</summary>
public sealed class SchemaType
{
    /// <summary>Type name.</summary>
    public string Name { get; set; } = "";

    /// <summary>Optional description.</summary>
    public string? Description { get; set; }

    /// <summary>Fields defined in this type.</summary>
    public List<SchemaField> SchemaFields { get; set; } = new();

    /// <summary>Parent types for composition.</summary>
    public List<string> Parents { get; set; } = new();
}

/// <summary>A field definition in a schema.</summary>
public sealed class SchemaField
{
    /// <summary>Field name.</summary>
    public string Name { get; set; } = "";

    /// <summary>Field type definition.</summary>
    public SchemaFieldType FieldType { get; set; } = SchemaFieldType.String();

    /// <summary>Whether this field is required.</summary>
    public bool Required { get; set; }

    /// <summary>Whether this field is confidential.</summary>
    public bool Confidential { get; set; }

    /// <summary>Whether this field is deprecated.</summary>
    public bool Deprecated { get; set; }

    /// <summary>Optional description.</summary>
    public string? Description { get; set; }

    /// <summary>Validation constraints.</summary>
    public List<SchemaConstraint> Constraints { get; set; } = new();

    /// <summary>Default value if not provided.</summary>
    public string? DefaultValue { get; set; }

    /// <summary>Conditional requirements.</summary>
    public List<SchemaConditional> Conditionals { get; set; } = new();
}

/// <summary>A conditional requirement on a field.</summary>
public sealed class SchemaConditional
{
    /// <summary>The field path to evaluate.</summary>
    public string Field { get; set; } = "";

    /// <summary>The comparison operator.</summary>
    public ConditionalOperator Operator { get; set; }

    /// <summary>The expected value.</summary>
    public ConditionalValue CondValue { get; set; } = ConditionalValue.FromString("");

    /// <summary>If true, this is an :unless condition (negated).</summary>
    public bool Unless { get; set; }
}

/// <summary>Conditional comparison operator.</summary>
public enum ConditionalOperator
{
    /// <summary>Equal (=).</summary>
    Eq,
    /// <summary>Not equal (!=).</summary>
    NotEq,
    /// <summary>Greater than (&gt;).</summary>
    Gt,
    /// <summary>Less than (&lt;).</summary>
    Lt,
    /// <summary>Greater than or equal (&gt;=).</summary>
    Gte,
    /// <summary>Less than or equal (&lt;=).</summary>
    Lte,
}

/// <summary>A conditional value (string, number, or boolean).</summary>
public abstract class ConditionalValue
{
    /// <summary>Creates a string conditional value.</summary>
    public static ConditionalValue FromString(string value) => new StringConditionalValue(value);

    /// <summary>Creates a numeric conditional value.</summary>
    public static ConditionalValue FromNumber(double value) => new NumberConditionalValue(value);

    /// <summary>Creates a boolean conditional value.</summary>
    public static ConditionalValue FromBool(bool value) => new BoolConditionalValue(value);

    /// <summary>Gets the string value if applicable.</summary>
    public virtual string? AsString() => null;

    /// <summary>Gets the numeric value if applicable.</summary>
    public virtual double? AsNumber() => null;

    /// <summary>Gets the boolean value if applicable.</summary>
    public virtual bool? AsBool() => null;
}

/// <summary>String conditional value.</summary>
public sealed class StringConditionalValue : ConditionalValue
{
    /// <summary>The value.</summary>
    public string Value { get; }

    /// <summary>Creates a string conditional value.</summary>
    public StringConditionalValue(string value) { Value = value; }

    /// <inheritdoc/>
    public override string? AsString() => Value;
}

/// <summary>Numeric conditional value.</summary>
public sealed class NumberConditionalValue : ConditionalValue
{
    /// <summary>The value.</summary>
    public double Value { get; }

    /// <summary>Creates a numeric conditional value.</summary>
    public NumberConditionalValue(double value) { Value = value; }

    /// <inheritdoc/>
    public override double? AsNumber() => Value;
}

/// <summary>Boolean conditional value.</summary>
public sealed class BoolConditionalValue : ConditionalValue
{
    /// <summary>The value.</summary>
    public bool Value { get; }

    /// <summary>Creates a boolean conditional value.</summary>
    public BoolConditionalValue(bool value) { Value = value; }

    /// <inheritdoc/>
    public override bool? AsBool() => Value;
}

/// <summary>The type of a schema field.</summary>
public abstract class SchemaFieldType
{
    /// <summary>Creates a string field type.</summary>
    public static SchemaFieldType String() => new StringFieldType();

    /// <summary>Creates a boolean field type.</summary>
    public static SchemaFieldType Boolean() => new BooleanFieldType();

    /// <summary>Creates a null field type.</summary>
    public static SchemaFieldType Null() => new NullFieldType();

    /// <summary>Creates a number field type.</summary>
    public static SchemaFieldType Number(byte? decimalPlaces = null) => new NumberFieldType { DecimalPlaces = decimalPlaces };

    /// <summary>Creates an integer field type.</summary>
    public static SchemaFieldType Integer() => new IntegerFieldType();

    /// <summary>Creates a decimal field type.</summary>
    public static SchemaFieldType Decimal(byte? decimalPlaces = null) => new DecimalFieldType { DecimalPlaces = decimalPlaces };

    /// <summary>Creates a currency field type.</summary>
    public static SchemaFieldType Currency(byte? decimalPlaces = null) => new CurrencyFieldType { DecimalPlaces = decimalPlaces };

    /// <summary>Creates a date field type.</summary>
    public static SchemaFieldType Date() => new DateFieldType();

    /// <summary>Creates a timestamp field type.</summary>
    public static SchemaFieldType Timestamp() => new TimestampFieldType();

    /// <summary>Creates a time field type.</summary>
    public static SchemaFieldType Time() => new TimeFieldType();

    /// <summary>Creates a duration field type.</summary>
    public static SchemaFieldType Duration() => new DurationFieldType();

    /// <summary>Creates a percent field type.</summary>
    public static SchemaFieldType Percent() => new PercentFieldType();

    /// <summary>Creates an enum field type.</summary>
    public static SchemaFieldType Enum(List<string> values) => new EnumFieldType(values);

    /// <summary>Creates a union field type.</summary>
    public static SchemaFieldType Union(List<SchemaFieldType> types) => new UnionFieldType(types);

    /// <summary>Creates a reference field type.</summary>
    public static SchemaFieldType Reference(string target) => new ReferenceFieldType(target);

    /// <summary>Creates a binary field type.</summary>
    public static SchemaFieldType Binary() => new BinaryFieldType();

    /// <summary>Creates a type reference field type.</summary>
    public static SchemaFieldType TypeRef(string name) => new TypeRefFieldType(name);
}

/// <summary>String field type.</summary>
public sealed class StringFieldType : SchemaFieldType { }

/// <summary>Boolean field type.</summary>
public sealed class BooleanFieldType : SchemaFieldType { }

/// <summary>Null field type.</summary>
public sealed class NullFieldType : SchemaFieldType { }

/// <summary>Number field type.</summary>
public sealed class NumberFieldType : SchemaFieldType
{
    /// <summary>Fixed decimal places.</summary>
    public byte? DecimalPlaces { get; init; }
}

/// <summary>Integer field type.</summary>
public sealed class IntegerFieldType : SchemaFieldType { }

/// <summary>Decimal field type.</summary>
public sealed class DecimalFieldType : SchemaFieldType
{
    /// <summary>Fixed decimal places.</summary>
    public byte? DecimalPlaces { get; init; }
}

/// <summary>Currency field type.</summary>
public sealed class CurrencyFieldType : SchemaFieldType
{
    /// <summary>Fixed decimal places.</summary>
    public byte? DecimalPlaces { get; init; }
}

/// <summary>Date field type.</summary>
public sealed class DateFieldType : SchemaFieldType { }

/// <summary>Timestamp field type.</summary>
public sealed class TimestampFieldType : SchemaFieldType { }

/// <summary>Time field type.</summary>
public sealed class TimeFieldType : SchemaFieldType { }

/// <summary>Duration field type.</summary>
public sealed class DurationFieldType : SchemaFieldType { }

/// <summary>Percent field type.</summary>
public sealed class PercentFieldType : SchemaFieldType { }

/// <summary>Enum field type.</summary>
public sealed class EnumFieldType : SchemaFieldType
{
    /// <summary>Allowed values.</summary>
    public List<string> Values { get; }

    /// <summary>Creates an enum field type.</summary>
    public EnumFieldType(List<string> values) { Values = values; }
}

/// <summary>Union field type.</summary>
public sealed class UnionFieldType : SchemaFieldType
{
    /// <summary>Possible types.</summary>
    public List<SchemaFieldType> Types { get; }

    /// <summary>Creates a union field type.</summary>
    public UnionFieldType(List<SchemaFieldType> types) { Types = types; }
}

/// <summary>Reference field type.</summary>
public sealed class ReferenceFieldType : SchemaFieldType
{
    /// <summary>Target path.</summary>
    public string Target { get; }

    /// <summary>Creates a reference field type.</summary>
    public ReferenceFieldType(string target) { Target = target; }
}

/// <summary>Binary field type.</summary>
public sealed class BinaryFieldType : SchemaFieldType { }

/// <summary>Type reference field type (references a named type definition).</summary>
public sealed class TypeRefFieldType : SchemaFieldType
{
    /// <summary>Referenced type name.</summary>
    public string Name { get; }

    /// <summary>Creates a type reference field type.</summary>
    public TypeRefFieldType(string name) { Name = name; }
}

/// <summary>A constraint on a field.</summary>
public abstract class SchemaConstraint
{
    /// <summary>Creates a bounds constraint.</summary>
    public static SchemaConstraint Bounds(string? min = null, string? max = null, bool minExclusive = false, bool maxExclusive = false) =>
        new BoundsConstraint { Min = min, Max = max, MinExclusive = minExclusive, MaxExclusive = maxExclusive };

    /// <summary>Creates a pattern constraint.</summary>
    public static SchemaConstraint Pattern(string pattern) => new PatternConstraint(pattern);

    /// <summary>Creates an enum constraint.</summary>
    public static SchemaConstraint Enum(List<string> values) => new EnumConstraint(values);

    /// <summary>Creates a unique constraint.</summary>
    public static SchemaConstraint Unique() => new UniqueConstraint();

    /// <summary>Creates a size constraint.</summary>
    public static SchemaConstraint Size(long? min = null, long? max = null) =>
        new SizeConstraint { Min = min, Max = max };

    /// <summary>Creates a format constraint.</summary>
    public static SchemaConstraint Format(string format) => new FormatConstraint(format);
}

/// <summary>Bounds constraint (min/max).</summary>
public sealed class BoundsConstraint : SchemaConstraint
{
    /// <summary>Minimum bound.</summary>
    public string? Min { get; init; }

    /// <summary>Maximum bound.</summary>
    public string? Max { get; init; }

    /// <summary>Whether minimum is exclusive.</summary>
    public bool MinExclusive { get; init; }

    /// <summary>Whether maximum is exclusive.</summary>
    public bool MaxExclusive { get; init; }
}

/// <summary>Pattern (regex) constraint.</summary>
public sealed class PatternConstraint : SchemaConstraint
{
    /// <summary>The regex pattern.</summary>
    public string PatternValue { get; }

    /// <summary>Creates a pattern constraint.</summary>
    public PatternConstraint(string pattern) { PatternValue = pattern; }
}

/// <summary>Enum constraint (allowed values).</summary>
public sealed class EnumConstraint : SchemaConstraint
{
    /// <summary>Allowed values.</summary>
    public List<string> Values { get; }

    /// <summary>Creates an enum constraint.</summary>
    public EnumConstraint(List<string> values) { Values = values; }
}

/// <summary>Unique constraint.</summary>
public sealed class UniqueConstraint : SchemaConstraint { }

/// <summary>Size constraint for binary data.</summary>
public sealed class SizeConstraint : SchemaConstraint
{
    /// <summary>Minimum size in bytes.</summary>
    public long? Min { get; init; }

    /// <summary>Maximum size in bytes.</summary>
    public long? Max { get; init; }
}

/// <summary>Format constraint (email, url, uuid, etc.).</summary>
public sealed class FormatConstraint : SchemaConstraint
{
    /// <summary>Format name.</summary>
    public string FormatName { get; }

    /// <summary>Creates a format constraint.</summary>
    public FormatConstraint(string format) { FormatName = format; }
}

/// <summary>An array definition in a schema.</summary>
public sealed class SchemaArray
{
    /// <summary>Array name.</summary>
    public string Name { get; set; } = "";

    /// <summary>Type of each array item.</summary>
    public SchemaFieldType ItemType { get; set; } = SchemaFieldType.String();

    /// <summary>Minimum number of items.</summary>
    public int? MinItems { get; set; }

    /// <summary>Maximum number of items.</summary>
    public int? MaxItems { get; set; }

    /// <summary>Whether items must be unique.</summary>
    public bool IsUnique { get; set; }
}

/// <summary>An object-level constraint.</summary>
public abstract class SchemaObjectConstraint
{
    /// <summary>Creates an invariant constraint.</summary>
    public static SchemaObjectConstraint Invariant(string expression) => new InvariantConstraint(expression);

    /// <summary>Creates a cardinality constraint.</summary>
    public static SchemaObjectConstraint Cardinality(List<string> fields, int? min = null, int? max = null) =>
        new CardinalityConstraint(fields) { Min = min, Max = max };
}

/// <summary>Invariant expression constraint.</summary>
public sealed class InvariantConstraint : SchemaObjectConstraint
{
    /// <summary>The invariant expression.</summary>
    public string Expression { get; }

    /// <summary>Creates an invariant constraint.</summary>
    public InvariantConstraint(string expression) { Expression = expression; }
}

/// <summary>Cardinality constraint.</summary>
public sealed class CardinalityConstraint : SchemaObjectConstraint
{
    /// <summary>Fields in the cardinality group.</summary>
    public List<string> Fields { get; }

    /// <summary>Minimum number of fields that must be present.</summary>
    public int? Min { get; init; }

    /// <summary>Maximum number of fields that may be present.</summary>
    public int? Max { get; init; }

    /// <summary>Creates a cardinality constraint.</summary>
    public CardinalityConstraint(List<string> fields) { Fields = fields; }
}
