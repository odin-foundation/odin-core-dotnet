using System;

namespace Odin.Core.Types;

/// <summary>
/// Trailing directive that follows an ODIN value (e.g., :pos 3, :len 8, :format ssn, :trim).
/// </summary>
public sealed class OdinDirective
{
    /// <summary>Directive name (e.g., "pos", "len", "format", "trim").</summary>
    public string Name { get; }

    /// <summary>Optional directive value.</summary>
    public DirectiveValue? Value { get; }

    /// <summary>Creates a directive with a name and optional value.</summary>
    public OdinDirective(string name, DirectiveValue? value = null)
    {
        Name = name;
        Value = value;
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) =>
        obj is OdinDirective other &&
        Name == other.Name &&
        Equals(Value, other.Value);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Name, Value);
}

/// <summary>
/// Value of a directive — either a string or a number.
/// </summary>
public abstract class DirectiveValue
{
    /// <summary>Creates a string directive value.</summary>
    public static DirectiveValue FromString(string value) => new StringDirectiveValue(value);

    /// <summary>Creates a numeric directive value.</summary>
    public static DirectiveValue FromNumber(double value) => new NumberDirectiveValue(value);

    /// <summary>Gets the string value if this is a string directive.</summary>
    public virtual string? AsString() => null;

    /// <summary>Gets the numeric value if this is a number directive.</summary>
    public virtual double? AsNumber() => null;
}

/// <summary>String directive value.</summary>
public sealed class StringDirectiveValue : DirectiveValue
{
    /// <summary>The string value.</summary>
    public string Value { get; }

    /// <summary>Creates a string directive value.</summary>
    public StringDirectiveValue(string value) { Value = value; }

    /// <inheritdoc/>
    public override string? AsString() => Value;

    /// <inheritdoc/>
    public override bool Equals(object? obj) =>
        obj is StringDirectiveValue other && Value == other.Value;

    /// <inheritdoc/>
    public override int GetHashCode() => Value.GetHashCode();

    /// <inheritdoc/>
    public override string ToString() => Value;
}

/// <summary>Numeric directive value.</summary>
public sealed class NumberDirectiveValue : DirectiveValue
{
    /// <summary>The numeric value.</summary>
    public double Value { get; }

    /// <summary>Creates a numeric directive value.</summary>
    public NumberDirectiveValue(double value) { Value = value; }

    /// <inheritdoc/>
    public override double? AsNumber() => Value;

    /// <inheritdoc/>
    public override bool Equals(object? obj) =>
        obj is NumberDirectiveValue other && Value.Equals(other.Value);

    /// <inheritdoc/>
    public override int GetHashCode() => Value.GetHashCode();

    /// <inheritdoc/>
    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
