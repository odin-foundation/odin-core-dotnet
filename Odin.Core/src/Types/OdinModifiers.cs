using System;

namespace Odin.Core.Types;

/// <summary>
/// Modifiers that can be applied to any ODIN value.
/// In ODIN notation: ! = required, * = confidential, - = deprecated, :attr = XML attribute.
/// </summary>
public sealed class OdinModifiers
{
    /// <summary>Field is required (! modifier).</summary>
    public bool Required { get; init; }

    /// <summary>Value should be masked/redacted (* modifier).</summary>
    public bool Confidential { get; init; }

    /// <summary>Field is deprecated (- modifier).</summary>
    public bool Deprecated { get; init; }

    /// <summary>Emit as XML attribute instead of child element (:attr modifier).</summary>
    public bool Attr { get; init; }

    /// <summary>Returns true if no modifiers are set.</summary>
    public bool IsEmpty => !Required && !Confidential && !Deprecated && !Attr;

    /// <summary>Returns true if any modifier is set.</summary>
    public bool HasAny => !IsEmpty;

    /// <summary>A shared empty modifiers instance.</summary>
    public static readonly OdinModifiers Empty = new();

    /// <inheritdoc/>
    public override bool Equals(object? obj) =>
        obj is OdinModifiers other &&
        Required == other.Required &&
        Confidential == other.Confidential &&
        Deprecated == other.Deprecated &&
        Attr == other.Attr;

    /// <inheritdoc/>
    public override int GetHashCode() =>
        HashCode.Combine(Required, Confidential, Deprecated, Attr);
}
