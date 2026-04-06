using System;
using System.Collections.Generic;

namespace Odin.Core.Types
{
    // ─────────────────────────────────────────────────────────────────────────────
    // Enums
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// How confidential fields are protected during transform execution.
    /// </summary>
    public enum ConfidentialMode
    {
        /// <summary>Confidential values are replaced with null (~).</summary>
        Redact,

        /// <summary>Strings are replaced with asterisks; numbers and booleans become null.</summary>
        Mask
    }

    /// <summary>
    /// The kind of discriminator used for multi-record source detection.
    /// </summary>
    public enum DiscriminatorType
    {
        /// <summary>Discriminate by character position in fixed-width records.</summary>
        Position,

        /// <summary>Discriminate by field index (e.g., CSV column).</summary>
        Field,

        /// <summary>Discriminate by a JSON/ODIN path expression.</summary>
        Path
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Transform Specification Types
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// A fully parsed ODIN transform specification. Contains metadata, source/target
    /// configuration, constants, accumulators, lookup tables, and the segment tree
    /// that defines field mappings.
    /// </summary>
    public sealed class OdinTransform
    {
        /// <summary>Transform header metadata (versions, direction, name).</summary>
        public TransformMetadata Metadata { get; set; } = new TransformMetadata();

        /// <summary>Source format configuration. Null when not explicitly declared.</summary>
        public SourceConfig? Source { get; set; }

        /// <summary>Target format configuration.</summary>
        public TargetConfig Target { get; set; } = new TargetConfig();

        /// <summary>Named constants declared in the {const} section.</summary>
        public Dictionary<string, OdinValue> Constants { get; set; } = new Dictionary<string, OdinValue>();

        /// <summary>Named accumulators declared in the {accum} section.</summary>
        public Dictionary<string, AccumulatorDef> Accumulators { get; set; } = new Dictionary<string, AccumulatorDef>();

        /// <summary>Lookup tables declared in the {table.*} sections.</summary>
        public Dictionary<string, LookupTable> Tables { get; set; } = new Dictionary<string, LookupTable>();

        /// <summary>Ordered list of transform segments (top-level mapping groups).</summary>
        public List<TransformSegment> Segments { get; set; } = new List<TransformSegment>();

        /// <summary>External transform files imported via the {import} section.</summary>
        public List<ImportRef> Imports { get; set; } = new List<ImportRef>();

        /// <summary>Multi-pass execution order. Each entry is a 1-based pass number.</summary>
        public List<int> Passes { get; set; } = new List<int>();

        /// <summary>
        /// How confidential fields are enforced. Null means no enforcement
        /// (confidential values pass through unchanged).
        /// </summary>
        public ConfidentialMode? EnforceConfidential { get; set; }

        /// <summary>
        /// When <c>true</c>, type mismatches during transform execution produce errors
        /// instead of best-effort coercion.
        /// </summary>
        public bool StrictTypes { get; set; }
    }

    /// <summary>
    /// Header metadata from the {$} section of a transform specification.
    /// </summary>
    public sealed class TransformMetadata
    {
        /// <summary>ODIN specification version (e.g., "1.0.0").</summary>
        public string? OdinVersion { get; set; }

        /// <summary>Transform specification version (e.g., "1.0.0").</summary>
        public string? TransformVersion { get; set; }

        /// <summary>
        /// Transform direction string (e.g., "json-&gt;odin", "odin-&gt;csv").
        /// Determines source and target format parsers.
        /// </summary>
        public string? Direction { get; set; }

        /// <summary>Optional human-readable name for the transform.</summary>
        public string? Name { get; set; }

        /// <summary>Optional description of what the transform does.</summary>
        public string? Description { get; set; }
    }

    /// <summary>
    /// Source format configuration from the {source} section.
    /// </summary>
    public sealed class SourceConfig
    {
        /// <summary>Source format identifier (e.g., "json", "xml", "csv", "fixed").</summary>
        public string Format { get; set; } = "";

        /// <summary>Format-specific options (e.g., delimiter, encoding, hasHeader).</summary>
        public Dictionary<string, string> Options { get; set; } = new Dictionary<string, string>();

        /// <summary>XML namespace prefix-to-URI mappings.</summary>
        public Dictionary<string, string> Namespaces { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// Multi-record discriminator configuration. When set, enables dispatching
        /// different record types to different segment handlers.
        /// </summary>
        public SourceDiscriminator? Discriminator { get; set; }
    }

    /// <summary>
    /// Target format configuration from the {target} or {$} section.
    /// </summary>
    public sealed class TargetConfig
    {
        /// <summary>Target format identifier (e.g., "odin", "json", "csv", "xml").</summary>
        public string Format { get; set; } = "";

        /// <summary>Format-specific options (e.g., indent, rootElement, includeHeader).</summary>
        public Dictionary<string, string> Options { get; set; } = new Dictionary<string, string>();
    }

    /// <summary>
    /// Defines an accumulator variable that persists across records during
    /// multi-record transform execution.
    /// </summary>
    public sealed class AccumulatorDef
    {
        /// <summary>The accumulator name (used as a reference path, e.g., "$total").</summary>
        public string Name { get; set; } = "";

        /// <summary>Initial value before any records are processed. Defaults to null (~).</summary>
        public OdinValue Initial { get; set; } = new OdinNull();

        /// <summary>
        /// When <c>true</c>, the accumulator value is carried across transform invocations
        /// (e.g., across multiple files in a batch).
        /// </summary>
        public bool Persist { get; set; }
    }

    /// <summary>
    /// A lookup table declared in a {table.*} section. Provides static mapping
    /// data that verbs like <c>%lookup</c> can reference at runtime.
    /// </summary>
    public sealed class LookupTable
    {
        /// <summary>Table name (the part after "table." in the section header).</summary>
        public string Name { get; set; } = "";

        /// <summary>Ordered column names (first column is typically the key).</summary>
        public List<string> Columns { get; set; } = new List<string>();

        /// <summary>Table data rows. Each row is a list of values aligned with <see cref="Columns"/>.</summary>
        public List<List<DynValue>> Rows { get; set; } = new List<List<DynValue>>();

        /// <summary>Default value returned when a lookup key is not found. Null means no default.</summary>
        public DynValue? Default { get; set; }
    }

    /// <summary>
    /// A named segment in the transform specification. Segments define a mapping
    /// scope (typically corresponding to an output object or array) and contain
    /// field mappings and nested child segments.
    /// </summary>
    public sealed class TransformSegment
    {
        /// <summary>Segment name from the section header (e.g., "Customer", "Address").</summary>
        public string Name { get; set; } = "";

        /// <summary>Output path where this segment's results are placed.</summary>
        public string Path { get; set; } = "";

        /// <summary>Source path override. When set, mappings are relative to this path.</summary>
        public string? SourcePath { get; set; }

        /// <summary>
        /// Discriminator for conditional segment activation. When set, this segment
        /// only executes if the discriminator condition is satisfied.
        /// </summary>
        public Discriminator? SegmentDiscriminator { get; set; }

        /// <summary>When <c>true</c>, this segment produces an array of objects.</summary>
        public bool IsArray { get; set; }

        /// <summary>Segment-level directives (e.g., :flatten, :sort).</summary>
        public List<SegmentDirective> Directives { get; set; } = new List<SegmentDirective>();

        /// <summary>Field mappings within this segment.</summary>
        public List<FieldMapping> Mappings { get; set; } = new List<FieldMapping>();

        /// <summary>Nested child segments.</summary>
        public List<TransformSegment> Children { get; set; } = new List<TransformSegment>();

        /// <summary>
        /// Ordered items (mappings and children interleaved) preserving source order.
        /// Used when output order matters (e.g., fixed-width formats).
        /// </summary>
        public List<SegmentItem> Items { get; set; } = new List<SegmentItem>();

        /// <summary>Multi-pass number. Null means this segment runs in the default (first) pass.</summary>
        public int? Pass { get; set; }

        /// <summary>Guard condition expression. When set, the segment is skipped if the condition is false.</summary>
        public string? Condition { get; set; }
    }

    /// <summary>
    /// A directive applied at the segment level (e.g., :flatten, :sort, :distinct).
    /// </summary>
    public sealed class SegmentDirective
    {
        /// <summary>Directive type name (e.g., "flatten", "sort", "distinct").</summary>
        public string DirectiveType { get; set; } = "";

        /// <summary>Optional directive value or argument.</summary>
        public string? Value { get; set; }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // SegmentItem — discriminated union (Mapping or Child)
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// A discriminated union representing an item within a segment — either a
    /// <see cref="FieldMapping"/> or a nested <see cref="TransformSegment"/>.
    /// </summary>
    public abstract class SegmentItem
    {
        /// <summary>Creates a <see cref="SegmentItem"/> wrapping a field mapping.</summary>
        /// <param name="mapping">The field mapping.</param>
        /// <returns>A <see cref="MappingItem"/> instance.</returns>
        public static SegmentItem FromMapping(FieldMapping mapping) => new MappingItem(mapping);

        /// <summary>Creates a <see cref="SegmentItem"/> wrapping a child segment.</summary>
        /// <param name="child">The child segment.</param>
        /// <returns>A <see cref="ChildItem"/> instance.</returns>
        public static SegmentItem FromChild(TransformSegment child) => new ChildItem(child);

        /// <summary>Attempts to retrieve this item as a field mapping.</summary>
        /// <returns>The field mapping, or <c>null</c> if this is a child segment.</returns>
        public virtual FieldMapping? AsMapping() => null;

        /// <summary>Attempts to retrieve this item as a child segment.</summary>
        /// <returns>The child segment, or <c>null</c> if this is a field mapping.</returns>
        public virtual TransformSegment? AsChild() => null;
    }

    /// <summary>A <see cref="SegmentItem"/> that wraps a <see cref="FieldMapping"/>.</summary>
    public sealed class MappingItem : SegmentItem
    {
        /// <summary>The contained field mapping.</summary>
        public FieldMapping Mapping { get; }

        /// <summary>Creates a mapping segment item.</summary>
        /// <param name="mapping">The field mapping to wrap.</param>
        public MappingItem(FieldMapping mapping) { Mapping = mapping; }

        /// <inheritdoc/>
        public override FieldMapping? AsMapping() => Mapping;
    }

    /// <summary>A <see cref="SegmentItem"/> that wraps a nested <see cref="TransformSegment"/>.</summary>
    public sealed class ChildItem : SegmentItem
    {
        /// <summary>The contained child segment.</summary>
        public TransformSegment Child { get; }

        /// <summary>Creates a child segment item.</summary>
        /// <param name="child">The child segment to wrap.</param>
        public ChildItem(TransformSegment child) { Child = child; }

        /// <inheritdoc/>
        public override TransformSegment? AsChild() => Child;
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Discriminators
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// A segment-level discriminator that matches a path to a value. Used to
    /// conditionally activate segments based on input data.
    /// </summary>
    public sealed class Discriminator
    {
        /// <summary>The source path to evaluate.</summary>
        public string Path { get; set; } = "";

        /// <summary>The expected value to match against.</summary>
        public string Value { get; set; } = "";
    }

    /// <summary>
    /// Source-level discriminator for multi-record input formats. Determines
    /// how to identify which record type each input record belongs to.
    /// </summary>
    public sealed class SourceDiscriminator
    {
        /// <summary>The kind of discriminator (position, field, or path).</summary>
        public DiscriminatorType Type { get; set; }

        /// <summary>Start position for <see cref="DiscriminatorType.Position"/> discriminators (0-based).</summary>
        public int? Pos { get; set; }

        /// <summary>Character length for <see cref="DiscriminatorType.Position"/> discriminators.</summary>
        public int? Len { get; set; }

        /// <summary>Field index for <see cref="DiscriminatorType.Field"/> discriminators (0-based).</summary>
        public int? Field { get; set; }

        /// <summary>JSON/ODIN path for <see cref="DiscriminatorType.Path"/> discriminators.</summary>
        public string? Path { get; set; }
    }

    /// <summary>
    /// Multi-record input container. Holds pre-split record strings for
    /// multi-record transform execution.
    /// </summary>
    public sealed class MultiRecordInput
    {
        /// <summary>Individual record strings (one per input record).</summary>
        public List<string> Records { get; set; } = new List<string>();

        /// <summary>Record delimiter used to split the original input. Null means newline.</summary>
        public string? Delimiter { get; set; }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Field Mappings
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// A single field mapping within a transform segment. Maps a target field
    /// to a source expression, optionally applying modifiers and directives.
    /// </summary>
    public sealed class FieldMapping
    {
        /// <summary>Target field name or path in the output.</summary>
        public string Target { get; set; } = "";

        /// <summary>
        /// The source expression that produces the value for this field.
        /// Can be a copy, verb call, literal, or inline object.
        /// </summary>
        public FieldExpression Expression { get; set; } = null!;

        /// <summary>Output modifiers to apply (required, confidential, deprecated).</summary>
        public OdinModifiers? Modifiers { get; set; }

        /// <summary>Field-level directives (e.g., :pos, :len, :format).</summary>
        public List<OdinDirective> Directives { get; set; } = new List<OdinDirective>();
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // FieldExpression — discriminated union
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// A discriminated union representing how a field value is sourced.
    /// Can be a direct copy from source, a verb transformation, a literal value,
    /// or an inline object construction.
    /// </summary>
    public abstract class FieldExpression
    {
        /// <summary>Creates a copy expression that reads a value from the given source path.</summary>
        /// <param name="path">The source path (e.g., "@.name", "@.address.city").</param>
        /// <returns>A <see cref="CopyExpression"/> instance.</returns>
        public static FieldExpression Copy(string path) => new CopyExpression(path);

        /// <summary>Creates a transform expression that invokes a verb.</summary>
        /// <param name="call">The verb call definition.</param>
        /// <returns>A <see cref="TransformExpression"/> instance.</returns>
        public static FieldExpression Transform(VerbCall call) => new TransformExpression(call);

        /// <summary>Creates a literal expression with a fixed ODIN value.</summary>
        /// <param name="value">The literal ODIN value.</param>
        /// <returns>A <see cref="LiteralExpression"/> instance.</returns>
        public static FieldExpression Literal(OdinValue value) => new LiteralExpression(value);

        /// <summary>Creates an inline object expression built from nested field mappings.</summary>
        /// <param name="fields">The nested field mappings.</param>
        /// <returns>An <see cref="ObjectExpression"/> instance.</returns>
        public static FieldExpression Object(List<FieldMapping> fields) => new ObjectExpression(fields);
    }

    /// <summary>
    /// A field expression that copies a value directly from the source data
    /// at the specified path.
    /// </summary>
    public sealed class CopyExpression : FieldExpression
    {
        /// <summary>The source path to copy from (e.g., "@.name").</summary>
        public string Path { get; }

        /// <summary>Creates a copy expression.</summary>
        /// <param name="path">The source path.</param>
        public CopyExpression(string path) { Path = path; }
    }

    /// <summary>
    /// A field expression that transforms a value by invoking a verb with arguments.
    /// </summary>
    public sealed class TransformExpression : FieldExpression
    {
        /// <summary>The verb call to execute.</summary>
        public VerbCall Call { get; }

        /// <summary>Creates a transform expression.</summary>
        /// <param name="call">The verb call definition.</param>
        public TransformExpression(VerbCall call) { Call = call; }
    }

    /// <summary>
    /// A field expression that provides a fixed literal value.
    /// </summary>
    public sealed class LiteralExpression : FieldExpression
    {
        /// <summary>The literal ODIN value.</summary>
        public OdinValue Value { get; }

        /// <summary>Creates a literal expression.</summary>
        /// <param name="value">The literal value.</param>
        public LiteralExpression(OdinValue value) { Value = value; }
    }

    /// <summary>
    /// A field expression that constructs an inline object from nested field mappings.
    /// </summary>
    public sealed class ObjectExpression : FieldExpression
    {
        /// <summary>The nested field mappings that form the object's properties.</summary>
        public List<FieldMapping> Fields { get; }

        /// <summary>Creates an object expression.</summary>
        /// <param name="fields">The nested field mappings.</param>
        public ObjectExpression(List<FieldMapping> fields) { Fields = fields; }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Verb Calls and Arguments
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// A verb invocation with its name and arguments. Represents expressions
    /// like <c>%upper @.name</c> or <c>%concat @.first " " @.last</c>.
    /// </summary>
    public sealed class VerbCall
    {
        /// <summary>Verb name (e.g., "upper", "concat", "lookup").</summary>
        public string Verb { get; set; } = "";

        /// <summary>
        /// When <c>true</c>, this is a custom (user-defined) verb prefixed with
        /// <c>%&amp;</c> in ODIN notation.
        /// </summary>
        public bool IsCustom { get; set; }

        /// <summary>Ordered list of arguments passed to the verb.</summary>
        public List<VerbArg> Args { get; set; } = new List<VerbArg>();
    }

    /// <summary>
    /// A discriminated union representing a single argument to a verb call.
    /// Can be a source path reference, a literal value, or a nested verb call.
    /// </summary>
    public abstract class VerbArg
    {
        /// <summary>Creates a reference argument pointing to a source path.</summary>
        /// <param name="path">The source path (e.g., "@.name").</param>
        /// <param name="directives">Optional directives applied to this reference.</param>
        /// <returns>A <see cref="ReferenceArg"/> instance.</returns>
        public static VerbArg Ref(string path, List<OdinDirective>? directives = null) =>
            new ReferenceArg(path, directives ?? new List<OdinDirective>());

        /// <summary>Creates a literal value argument.</summary>
        /// <param name="value">The literal ODIN value.</param>
        /// <returns>A <see cref="LiteralArg"/> instance.</returns>
        public static VerbArg Lit(OdinValue value) => new LiteralArg(value);

        /// <summary>Creates a nested verb call argument.</summary>
        /// <param name="call">The nested verb call.</param>
        /// <returns>A <see cref="VerbCallArg"/> instance.</returns>
        public static VerbArg NestedCall(VerbCall call) => new VerbCallArg(call);
    }

    /// <summary>A verb argument that references a source path.</summary>
    public sealed class ReferenceArg : VerbArg
    {
        /// <summary>The source path being referenced.</summary>
        public string Path { get; }

        /// <summary>Directives applied to this reference (e.g., :format, :default).</summary>
        public List<OdinDirective> Directives { get; }

        /// <summary>Creates a reference argument.</summary>
        /// <param name="path">The source path.</param>
        /// <param name="directives">Directives applied to the reference.</param>
        public ReferenceArg(string path, List<OdinDirective> directives)
        {
            Path = path;
            Directives = directives;
        }
    }

    /// <summary>A verb argument that provides a literal value.</summary>
    public sealed class LiteralArg : VerbArg
    {
        /// <summary>The literal ODIN value.</summary>
        public OdinValue Value { get; }

        /// <summary>Creates a literal argument.</summary>
        /// <param name="value">The literal value.</param>
        public LiteralArg(OdinValue value) { Value = value; }
    }

    /// <summary>A verb argument that is itself a nested verb call.</summary>
    public sealed class VerbCallArg : VerbArg
    {
        /// <summary>The nested verb call.</summary>
        public new VerbCall NestedCall { get; }

        /// <summary>Creates a nested verb call argument.</summary>
        /// <param name="call">The verb call.</param>
        public VerbCallArg(VerbCall call) { NestedCall = call; }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Imports
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// A reference to an external transform file imported via the {import} section.
    /// </summary>
    public sealed class ImportRef
    {
        /// <summary>File path or URI of the imported transform.</summary>
        public string Path { get; set; } = "";

        /// <summary>Optional alias for referencing the imported transform's segments.</summary>
        public string? Alias { get; set; }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Transform Result
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The result of executing a transform. Contains the output data, optional
    /// formatted string representation, and any errors or warnings produced
    /// during execution.
    /// </summary>
    public sealed class TransformResult
    {
        /// <summary>Whether the transform completed without errors.</summary>
        public bool Success { get; set; }

        /// <summary>The transform output as a dynamic value tree. Null on failure.</summary>
        public DynValue? Output { get; set; }

        /// <summary>
        /// The output serialized to the target format string (e.g., JSON, ODIN text, CSV).
        /// Null if formatting was not requested or the transform failed.
        /// </summary>
        public string? Formatted { get; set; }

        /// <summary>Errors encountered during transform execution.</summary>
        public List<TransformError> Errors { get; set; } = new List<TransformError>();

        /// <summary>Non-fatal warnings produced during transform execution.</summary>
        public List<TransformWarning> Warnings { get; set; } = new List<TransformWarning>();

        /// <summary>
        /// Modifier metadata for output fields, keyed by dotted output path.
        /// Tracks which fields are required, confidential, or deprecated.
        /// </summary>
        public Dictionary<string, OdinModifiers> OutputModifiers { get; set; } = new Dictionary<string, OdinModifiers>();
    }

    /// <summary>
    /// An error produced during transform execution.
    /// </summary>
    public sealed class TransformError
    {
        /// <summary>Human-readable error description.</summary>
        public string Message { get; set; } = "";

        /// <summary>Dotted path where the error occurred. Null if not path-specific.</summary>
        public string? Path { get; set; }

        /// <summary>Machine-readable error code (e.g., "T001"). Null if not categorized.</summary>
        public string? Code { get; set; }
    }

    /// <summary>
    /// A non-fatal warning produced during transform execution.
    /// </summary>
    public sealed class TransformWarning
    {
        /// <summary>Human-readable warning description.</summary>
        public string Message { get; set; } = "";

        /// <summary>Dotted path where the warning originated. Null if not path-specific.</summary>
        public string? Path { get; set; }
    }
}
