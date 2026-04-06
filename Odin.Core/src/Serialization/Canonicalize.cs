using System;
using System.Collections.Generic;
using System.Text;
using Odin.Core.Types;

namespace Odin.Core.Serialization;

/// <summary>
/// Produces canonical (deterministic, byte-identical) output for an <see cref="OdinDocument"/>.
/// Canonical form is used for hashing, signatures, and deduplication. It guarantees that
/// semantically equivalent documents produce identical bytes.
/// </summary>
public static class Canonicalize
{
    /// <summary>
    /// Produce canonical UTF-8 bytes for a document.
    /// </summary>
    /// <remarks>
    /// Canonical rules:
    /// <list type="bullet">
    ///   <item>All keys sorted alphabetically.</item>
    ///   <item>No trailing whitespace.</item>
    ///   <item>Consistent value formatting.</item>
    ///   <item>UTF-8 encoded.</item>
    ///   <item>Metadata section included.</item>
    /// </list>
    /// </remarks>
    /// <param name="doc">The document to canonicalize.</param>
    /// <returns>UTF-8 encoded bytes of the canonical form.</returns>
    public static byte[] Serialize(OdinDocument doc)
    {
        var opts = new StringifyOptions
        {
            IncludeMetadata = true,
            PreserveOrder = false, // false = sort keys alphabetically
            Canonical = true,
        };
        var text = Stringify.Serialize(doc, opts);
        return Encoding.UTF8.GetBytes(text);
    }
}
