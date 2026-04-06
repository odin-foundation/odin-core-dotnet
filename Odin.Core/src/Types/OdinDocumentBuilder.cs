using System;
using System.Collections.Generic;

namespace Odin.Core.Types;

/// <summary>
/// Fluent builder for constructing OdinDocument instances.
/// </summary>
public sealed class OdinDocumentBuilder
{
    private readonly OrderedMap<string, OdinValue> _metadata = new();
    private readonly OrderedMap<string, OdinValue> _assignments = new();
    private readonly OrderedMap<string, OdinModifiers> _modifiers = new();
    private readonly List<OdinImport> _imports = new();
    private readonly List<OdinSchemaRef> _schemas = new();
    private readonly List<OdinConditional> _conditionals = new();
    private readonly List<OdinComment> _comments = new();

    /// <summary>Set a metadata value in the {$} section.</summary>
    public OdinDocumentBuilder Metadata(string key, OdinValue value)
    {
        _metadata.Set(key, value);
        return this;
    }

    /// <summary>Set a string metadata value.</summary>
    public OdinDocumentBuilder Metadata(string key, string value)
    {
        _metadata.Set(key, OdinValues.String(value));
        return this;
    }

    /// <summary>Set a field assignment.</summary>
    public OdinDocumentBuilder Set(string path, OdinValue value)
    {
        _assignments.Set(path, value);
        return this;
    }

    /// <summary>Set a string field.</summary>
    public OdinDocumentBuilder SetString(string path, string value)
    {
        _assignments.Set(path, OdinValues.String(value));
        return this;
    }

    /// <summary>Set an integer field.</summary>
    public OdinDocumentBuilder SetInteger(string path, long value)
    {
        _assignments.Set(path, OdinValues.Integer(value));
        return this;
    }

    /// <summary>Set a number field.</summary>
    public OdinDocumentBuilder SetNumber(string path, double value)
    {
        _assignments.Set(path, OdinValues.Number(value));
        return this;
    }

    /// <summary>Set a boolean field.</summary>
    public OdinDocumentBuilder SetBoolean(string path, bool value)
    {
        _assignments.Set(path, OdinValues.Boolean(value));
        return this;
    }

    /// <summary>Set a null field.</summary>
    public OdinDocumentBuilder SetNull(string path)
    {
        _assignments.Set(path, OdinValues.Null());
        return this;
    }

    /// <summary>Set a currency field.</summary>
    public OdinDocumentBuilder SetCurrency(string path, double value, byte decimalPlaces = 2, string? currencyCode = null)
    {
        var cv = currencyCode != null
            ? OdinValues.CurrencyWithCode(value, decimalPlaces, currencyCode)
            : OdinValues.Currency(value, decimalPlaces);
        _assignments.Set(path, cv);
        return this;
    }

    /// <summary>Set modifiers for a path.</summary>
    public OdinDocumentBuilder WithModifiers(string path, OdinModifiers modifiers)
    {
        _modifiers.Set(path, modifiers);
        return this;
    }

    /// <summary>Add an import directive.</summary>
    public OdinDocumentBuilder AddImport(string importPath, string? alias = null)
    {
        _imports.Add(new OdinImport(importPath) { Alias = alias });
        return this;
    }

    /// <summary>Add a schema directive.</summary>
    public OdinDocumentBuilder AddSchema(string url)
    {
        _schemas.Add(new OdinSchemaRef(url));
        return this;
    }

    /// <summary>Add a comment.</summary>
    public OdinDocumentBuilder AddComment(string text, string? associatedPath = null)
    {
        _comments.Add(new OdinComment(text) { AssociatedPath = associatedPath });
        return this;
    }

    /// <summary>Build the document.</summary>
    public OdinDocument Build()
    {
        return new OdinDocument(
            metadata: _metadata,
            assignments: _assignments,
            modifiers: _modifiers,
            imports: _imports,
            schemas: _schemas,
            conditionals: _conditionals,
            comments: _comments);
    }
}
