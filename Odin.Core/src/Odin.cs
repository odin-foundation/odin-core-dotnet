using System;
using System.Collections.Generic;
using Odin.Core.Diff;
using Odin.Core.Export;
using Odin.Core.Forms;
using Odin.Core.Parsing;
using Odin.Core.Resolver;
using Odin.Core.Serialization;
using Odin.Core.Transform;
using Odin.Core.Types;
using Odin.Core.Utils;
using Odin.Core.Validation;

namespace Odin.Core;

/// <summary>
/// Static entry point for the ODIN SDK. Provides parsing, serialization,
/// validation, transformation, diff, and export functionality.
/// </summary>
public static class Odin
{
    /// <summary>ODIN specification version.</summary>
    public const string Version = "1.0.0";

    /// <summary>
    /// Static constructor wires source parsers and output formatters into the transform engine.
    /// </summary>
    static Odin()
    {
        // Wire source parsers
        TransformEngine.SourceParser = ParseSourceFormat;

        // Wire verb registry
        TransformEngine.VerbRegistry = Transform.Verbs.VerbRegistry.Instance.ToDictionary();
    }

    private static DynValue? ParseSourceFormat(string input, string format)
    {
        try
        {
            switch (format)
            {
                case "json":
                    return JsonSourceParser.Parse(input);
                case "xml":
                    return XmlSourceParser.Parse(input);
                case "csv":
                    return CsvSourceParser.Parse(input, null);
                case "flat":
                case "properties":
                case "flat-kvp":
                    return FlatSourceParser.Parse(input);
                case "yaml":
                case "flat-yaml":
                    return YamlSourceParser.Parse(input);
                case "fixed-width":
                    // Fixed-width requires field definitions; cannot parse from format string alone
                    return null;
                default:
                    return null;
            }
        }
        catch
        {
            return null;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Parse
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Parse ODIN text into a document.</summary>
    public static OdinDocument Parse(string input)
    {
        return OdinParser.Parse(input, ParseOptions.Default);
    }

    /// <summary>Parse ODIN text into a document with options.</summary>
    public static OdinDocument Parse(string input, ParseOptions options)
    {
        return OdinParser.Parse(input, options);
    }

    /// <summary>Parse ODIN text containing multiple documents (separated by ---).</summary>
    public static IReadOnlyList<OdinDocument> ParseDocuments(string input)
    {
        return OdinParser.ParseMulti(input, ParseOptions.Default);
    }

    /// <summary>
    /// Collapse a chained ODIN document into its computed current state.
    /// Later documents overlay earlier ones, a repeated path replaces the earlier
    /// value, <c>field = ~</c> removes the field and its descendants, and
    /// <c>field[] = ~</c> clears the array. The result carries the final document's metadata.
    /// </summary>
    public static OdinDocument CollapseChain(string input)
    {
        return CollapseChain(ParseDocuments(input));
    }

    /// <summary>
    /// Collapse a parsed chain of documents into its computed current state.
    /// </summary>
    public static OdinDocument CollapseChain(IReadOnlyList<OdinDocument> docs)
    {
        var assignments = new OrderedMap<string, OdinValue>();
        var modifiers = new OrderedMap<string, OdinModifiers>();
        OrderedMap<string, OdinValue> metadata = new();

        foreach (var doc in docs)
        {
            metadata = doc.Metadata.Clone();

            foreach (var path in doc.Paths())
            {
                if (path.StartsWith("$.", StringComparison.Ordinal)) continue;

                var value = doc.Get(path);
                if (value == null) continue;

                if (value.Type == OdinValueType.Null)
                {
                    if (path.EndsWith("[]", StringComparison.Ordinal))
                    {
                        ClearArray(assignments, modifiers, path.Substring(0, path.Length - 2));
                    }
                    else
                    {
                        RemovePath(assignments, modifiers, path);
                    }
                    continue;
                }

                assignments.Set(path, value);
                if (doc.PathModifiers.TryGetValue(path, out var mods))
                {
                    modifiers.Set(path, mods);
                }
                else
                {
                    modifiers.Remove(path);
                }
            }
        }

        return new OdinDocument(metadata: metadata, assignments: assignments, modifiers: modifiers);
    }

    /// <summary>Remove a path and any nested descendants from the working maps.</summary>
    private static void RemovePath(
        OrderedMap<string, OdinValue> assignments,
        OrderedMap<string, OdinModifiers> modifiers,
        string path)
    {
        var dotPrefix = path + ".";
        var bracketPrefix = path + "[";
        foreach (var key in new List<string>(assignments.Keys))
        {
            if (key == path
                || key.StartsWith(dotPrefix, StringComparison.Ordinal)
                || key.StartsWith(bracketPrefix, StringComparison.Ordinal))
            {
                assignments.Remove(key);
                modifiers.Remove(key);
            }
        }
    }

    /// <summary>Clear all indexed elements of an array path from the working maps.</summary>
    private static void ClearArray(
        OrderedMap<string, OdinValue> assignments,
        OrderedMap<string, OdinModifiers> modifiers,
        string arrayPath)
    {
        var prefix = arrayPath + "[";
        foreach (var key in new List<string>(assignments.Keys))
        {
            if (key.StartsWith(prefix, StringComparison.Ordinal))
            {
                assignments.Remove(key);
                modifiers.Remove(key);
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Serialize
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Serialize an OdinDocument to ODIN text.</summary>
    public static string Stringify(OdinDocument doc, StringifyOptions? options = null)
    {
        return Serialization.Stringify.Serialize(doc, options);
    }

    /// <summary>Produce deterministic, byte-identical canonical form.</summary>
    public static byte[] Canonicalize(OdinDocument doc)
    {
        return Serialization.Canonicalize.Serialize(doc);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Schema + Validate
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Parse a schema definition from ODIN text.</summary>
    public static OdinSchemaDefinition ParseSchema(string input)
    {
        return SchemaParser.Parse(input);
    }

    /// <summary>Validate a document against a schema.</summary>
    public static ValidationResult Validate(OdinDocument doc, OdinSchemaDefinition schema, ValidateOptions? options = null)
    {
        return ValidationEngine.Validate(doc, schema, options);
    }

    /// <summary>Validate a document against a schema, resolving imported type references via a registry.</summary>
    public static ValidationResult Validate(OdinDocument doc, OdinSchemaDefinition schema, ValidateOptions? options, TypeRegistry? typeRegistry)
    {
        return ValidationEngine.Validate(doc, schema, options, typeRegistry);
    }

    /// <summary>Resolve a schema file's imports, then validate the document against it with the merged type registry.</summary>
    public static ValidationResult ValidateWithImports(
        OdinDocument doc,
        string schemaPath,
        IFileReader reader,
        ValidateOptions? options = null,
        ResolverOptions? resolverOptions = null)
    {
        var resolver = new ImportResolver(reader, resolverOptions);
        resolver.DocumentParser = Parse;
        resolver.SchemaParser = ParseSchema;
        var resolved = resolver.ResolveSchema(schemaPath);
        return ValidationEngine.Validate(doc, resolved.Schema, options, resolved.TypeRegistry);
    }

    /// <summary>Serialize a schema definition back to ODIN schema text.</summary>
    public static string SerializeSchema(OdinSchemaDefinition schema)
    {
        return SchemaSerializer.Serialize(schema);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Resolver
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Create an import resolver for resolving @import directives.</summary>
    public static ImportResolver CreateImportResolver(IFileReader reader, ResolverOptions? options = null)
    {
        return new ImportResolver(reader, options);
    }

    /// <summary>Create a schema flattener for merging imported schemas.</summary>
    public static SchemaFlattener CreateSchemaFlattener(FlattenerOptions? options = null)
    {
        return new SchemaFlattener(options);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Transform
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Parse a transform specification from ODIN text.</summary>
    public static OdinTransform ParseTransform(string input)
    {
        return Transform.TransformParser.Parse(input);
    }

    /// <summary>Execute a transform on source data.</summary>
    public static TransformResult ExecuteTransform(string transformText, object source)
    {
        var transform = ParseTransform(transformText);
        return ExecuteTransform(transform, source);
    }

    /// <summary>Execute a parsed transform on source data.</summary>
    public static TransformResult ExecuteTransform(OdinTransform transform, object source)
    {
        DynValue dynSource;
        if (source is DynValue dv)
            dynSource = dv;
        else if (source is string s)
            dynSource = DynValue.String(s);
        else
            dynSource = DynValue.Null();
        return Transform.TransformEngine.Execute(transform, dynSource);
    }

    /// <summary>Execute a transform on an OdinDocument.</summary>
    public static TransformResult TransformDocument(string transformText, OdinDocument doc)
    {
        var transform = ParseTransform(transformText);
        // Convert OdinDocument to DynValue for transform execution
        var entries = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, DynValue>>();
        foreach (var entry in doc.Assignments)
            entries.Add(new System.Collections.Generic.KeyValuePair<string, DynValue>(entry.Key, Transform.TransformEngine.OdinValueToDyn(entry.Value)));
        return Transform.TransformEngine.Execute(transform, DynValue.Object(entries));
    }

    /// <summary>Execute a multi-record transform.</summary>
    public static TransformResult TransformMultiRecord(string transformText, MultiRecordInput input)
    {
        var transform = ParseTransform(transformText);
        // Join records into raw input for multi-record processing
        var rawInput = string.Join("\n", input.Records);
        return Transform.TransformEngine.Execute(transform, DynValue.String(rawInput));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Diff & Patch
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Compute the difference between two documents.</summary>
    public static OdinDiff Diff(OdinDocument a, OdinDocument b)
    {
        return Differ.ComputeDiff(a, b);
    }

    /// <summary>Apply a diff to a document.</summary>
    public static OdinDocument Patch(OdinDocument doc, OdinDiff diff)
    {
        return Patcher.Apply(doc, diff);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Export
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Export a document to JSON.</summary>
    public static string ToJson(OdinDocument doc, bool preserveTypes = false, bool preserveModifiers = false)
    {
        return JsonExport.ToJson(doc, preserveTypes, preserveModifiers);
    }

    /// <summary>Export a document to XML.</summary>
    public static string ToXml(OdinDocument doc, bool preserveTypes = false, bool preserveModifiers = false)
    {
        return XmlExport.ToXml(doc, preserveTypes, preserveModifiers);
    }

    /// <summary>Export a document to CSV.</summary>
    public static string ToCsv(OdinDocument doc, CsvExportOptions? options = null)
    {
        return CsvExport.ToCsv(doc, options);
    }

    /// <summary>Export a document to fixed-width format.</summary>
    public static string ToFixedWidth(OdinDocument doc, FixedWidthExportOptions options)
    {
        return FixedWidthExport.ToFixedWidth(doc, options);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Factory
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Create a new document builder.</summary>
    public static OdinDocumentBuilder Builder() => new();

    /// <summary>Create an empty document.</summary>
    public static OdinDocument Empty() => OdinDocument.Empty();

    // ─────────────────────────────────────────────────────────────────────────
    // Path Utilities
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Build a dotted path from segments.</summary>
    public static string Path(params string[] segments) => PathUtils.BuildPath(segments);

    /// <summary>Build a path with optional array indices.</summary>
    public static string PathWithIndices(params (string name, int? index)[] segments) => PathUtils.BuildPathWithIndices(segments);

    // ─────────────────────────────────────────────────────────────────────────
    // Streaming
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Create a streaming parser with the given handler.</summary>
    public static StreamingParser CreateStreamingParser(IParseHandler handler) => new(handler);

    // ─────────────────────────────────────────────────────────────────────────
    // Forms
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Parse an ODIN forms document text into a typed OdinForm.</summary>
    public static OdinForm ParseForm(string text) => FormParser.ParseForm(text);

    /// <summary>Render an OdinForm to a complete HTML string.</summary>
    public static string RenderForm(OdinForm form, OdinDocument? data = null, RenderFormOptions? options = null)
        => FormRenderer.RenderForm(form, data, options);
}
