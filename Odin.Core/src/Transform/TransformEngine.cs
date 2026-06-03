#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Odin.Core.Types;

namespace Odin.Core.Transform
{
    // ─────────────────────────────────────────────────────────────────────────
    // Verb Context and Delegates
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Context passed to verb functions during transform execution. Provides access
    /// to the current source data, loop variables, accumulators, lookup tables,
    /// and the global output tree.
    /// </summary>
    public sealed class VerbContext
    {
        /// <summary>The current source data being transformed.</summary>
        public DynValue Source { get; set; } = DynValue.Null();

        /// <summary>Loop variables for the current iteration scope (_item, _index, _length).</summary>
        public Dictionary<string, DynValue> LoopVars { get; set; } = new Dictionary<string, DynValue>();

        /// <summary>Named accumulator values that persist across records.</summary>
        public Dictionary<string, DynValue> Accumulators { get; set; } = new Dictionary<string, DynValue>();

        /// <summary>Lookup tables available for verb access.</summary>
        public Dictionary<string, LookupTable> Tables { get; set; } = new Dictionary<string, LookupTable>();

        /// <summary>Snapshot of the global output tree (for cross-segment references).</summary>
        public DynValue GlobalOutput { get; set; } = DynValue.Null();

        /// <summary>Errors collected by verbs (T011, etc.) — merged into TransformResult.errors.</summary>
        public List<TransformError> Errors { get; set; } = new List<TransformError>();

        /// <summary>Warnings collected by verbs — merged into TransformResult.warnings.</summary>
        public List<TransformWarning> Warnings { get; set; } = new List<TransformWarning>();

        /// <summary>Missing-data policy for lookup misses (fail/warn/skip/default; default silent null).</summary>
        public string? OnMissing { get; set; }

        /// <summary>Named sequence counters, shared across verb calls for the run.</summary>
        public Dictionary<string, long> SequenceCounters { get; set; } = new Dictionary<string, long>();
    }

    /// <summary>
    /// Options controlling transform execution.
    /// </summary>
    public sealed class TransformOptions
    {
        /// <summary>
        /// Resolves an @import path to ODIN transform text. Imported lookup tables,
        /// constants, accumulators, and named segments are merged into the transform
        /// before execution. Returning null leaves that import unresolved.
        /// </summary>
        public Func<string, string?>? ImportResolver { get; set; }
    }

    /// <summary>
    /// Carries a stable transform error code thrown during expression or loop
    /// evaluation. The mapping/segment handlers read <see cref="Error"/> to preserve
    /// the code instead of collapsing it to a generic transform error.
    /// </summary>
    internal sealed class CodedTransformException : Exception
    {
        /// <summary>The coded transform error.</summary>
        public TransformError Error { get; }

        /// <summary>Creates a coded transform exception.</summary>
        public CodedTransformException(TransformError error) : base(error.Message)
        {
            Error = error;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Execution Context (internal)
    // ─────────────────────────────────────────────────────────────────────────

    internal sealed class ExecContext
    {
        /// <summary>Root source data.</summary>
        public DynValue Source;

        /// <summary>Named constants converted to DynValue.</summary>
        public Dictionary<string, DynValue> Constants;

        /// <summary>Accumulator values.</summary>
        public Dictionary<string, DynValue> Accumulators;

        /// <summary>Lookup tables.</summary>
        public Dictionary<string, LookupTable> Tables;

        /// <summary>Loop variables for the current iteration scope.</summary>
        public Dictionary<string, DynValue> LoopVars;

        /// <summary>Loop aliases bound via <c>:loop path :as alias</c>.</summary>
        public Dictionary<string, DynValue> Aliases = new Dictionary<string, DynValue>();

        /// <summary>Verb registry: verb name -> function.</summary>
        public Dictionary<string, Func<DynValue[], VerbContext, DynValue>> Verbs;

        /// <summary>Collected warnings.</summary>
        public List<TransformWarning> Warnings;

        /// <summary>Collected non-fatal errors.</summary>
        public List<TransformError> Errors;

        /// <summary>Confidential enforcement mode.</summary>
        public ConfidentialMode? EnforceConfidential;

        /// <summary>Snapshot of the global output.</summary>
        public DynValue GlobalOutput;

        /// <summary>Collected field modifiers (target path -> modifiers).</summary>
        public Dictionary<string, OdinModifiers> FieldModifiers;

        /// <summary>Source format string.</summary>
        public string SourceFormat;

        /// <summary>Target configuration (format, options).</summary>
        public TargetConfig? Target;

        /// <summary>Named sequence counters, persisted across all verb calls.</summary>
        public Dictionary<string, long> SequenceCounters;

        /// <summary>Whether strict verb-argument type checking is enabled.</summary>
        public bool StrictTypes;

        public ExecContext()
        {
            Source = DynValue.Null();
            Constants = new Dictionary<string, DynValue>();
            Accumulators = new Dictionary<string, DynValue>();
            Tables = new Dictionary<string, LookupTable>();
            LoopVars = new Dictionary<string, DynValue>();
            Aliases = new Dictionary<string, DynValue>();
            Verbs = new Dictionary<string, Func<DynValue[], VerbContext, DynValue>>();
            Warnings = new List<TransformWarning>();
            Errors = new List<TransformError>();
            GlobalOutput = DynValue.Object(new List<KeyValuePair<string, DynValue>>());
            FieldModifiers = new Dictionary<string, OdinModifiers>();
            SourceFormat = "";
            SequenceCounters = new Dictionary<string, long>();
        }
    }

    /// <summary>
    /// Executes an <see cref="OdinTransform"/> against source data (<see cref="DynValue"/>)
    /// to produce a <see cref="TransformResult"/>.
    /// </summary>
    public static class TransformEngine
    {
        /// <summary>
        /// Delegate type for source format parsers. Accepts raw text and a format name,
        /// returns a parsed <see cref="DynValue"/> or null on failure.
        /// </summary>
        public static Func<string, string, DynValue?>? SourceParser { get; set; }

        /// <summary>
        /// Parser for ODIN source text. Reconstructs the nested object/array tree
        /// from a parsed document's dotted/indexed assignment paths.
        /// </summary>
        public static Func<string, DynValue>? OdinSourceParser { get; set; }

        /// <summary>
        /// Delegate type for output formatters. Accepts a <see cref="DynValue"/>, format name,
        /// options, and field modifiers; returns the formatted string.
        /// </summary>
        public static Func<DynValue, string, Dictionary<string, string>, Dictionary<string, OdinModifiers>, string>? OutputFormatter { get; set; }

        /// <summary>
        /// Registry of verb functions. Verb name -> (args, context) -> result.
        /// Populated externally by VerbRegistry.
        /// </summary>
        public static Dictionary<string, Func<DynValue[], VerbContext, DynValue>> VerbRegistry { get; set; }
            = new Dictionary<string, Func<DynValue[], VerbContext, DynValue>>();

        // ─────────────────────────────────────────────────────────────────────
        // Public entry points
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Execute a parsed transform against source data provided as a CLR object.
        /// The source can be a <see cref="DynValue"/>, <see cref="JsonElement"/>,
        /// <see cref="string"/>, or any CLR object (which is serialized via JSON round-trip).
        /// </summary>
        /// <param name="transform">The parsed transform specification.</param>
        /// <param name="source">The source data (DynValue, JsonElement, string, or CLR object).</param>
        /// <returns>The transform result containing output, formatted string, and diagnostics.</returns>
        public static TransformResult Execute(OdinTransform transform, object source)
        {
            if (transform == null) throw new ArgumentNullException(nameof(transform));
            if (source == null) throw new ArgumentNullException(nameof(source));

            DynValue dynSource;
            if (source is DynValue dv)
            {
                dynSource = dv;
            }
            else if (source is JsonElement je)
            {
                dynSource = DynValue.FromJsonElement(je);
            }
            else if (source is string s)
            {
                dynSource = DynValue.String(s);
            }
            else
            {
                // Convert CLR object to DynValue via JSON round-trip
                var json = JsonSerializer.Serialize(source);
                using var doc = JsonDocument.Parse(json);
                dynSource = DynValue.FromJsonElement(doc.RootElement);
            }

            return Execute(transform, dynSource);
        }

        /// <summary>
        /// Execute a parsed transform against an <see cref="OdinDocument"/>.
        /// The document's assignments are converted to a <see cref="DynValue"/> object
        /// tree before transform execution.
        /// </summary>
        /// <param name="transform">The parsed transform specification.</param>
        /// <param name="doc">The source ODIN document.</param>
        /// <returns>The transform result.</returns>
        public static TransformResult ExecuteDocument(OdinTransform transform, OdinDocument doc)
            => ExecuteDocument(transform, doc, null);

        /// <summary>
        /// Execute a parsed transform against an <see cref="OdinDocument"/> with options.
        /// </summary>
        /// <param name="transform">The parsed transform specification.</param>
        /// <param name="doc">The source ODIN document.</param>
        /// <param name="options">Execution options (e.g., an import resolver). May be null.</param>
        /// <returns>The transform result.</returns>
        public static TransformResult ExecuteDocument(OdinTransform transform, OdinDocument doc, TransformOptions? options)
        {
            if (transform == null) throw new ArgumentNullException(nameof(transform));
            if (doc == null) throw new ArgumentNullException(nameof(doc));

            var dynSource = OdinDocumentToDynValue(doc);
            return Execute(transform, dynSource, options);
        }

        /// <summary>
        /// Execute a multi-record transform against pre-split record input.
        /// Each record is dispatched to the appropriate segment based on the
        /// discriminator configuration in the transform specification.
        /// </summary>
        /// <param name="transform">The parsed transform specification.</param>
        /// <param name="input">The multi-record input with pre-split records.</param>
        /// <returns>The transform result.</returns>
        public static TransformResult ExecuteMultiRecord(OdinTransform transform, MultiRecordInput input)
        {
            if (transform == null) throw new ArgumentNullException(nameof(transform));
            if (input == null) throw new ArgumentNullException(nameof(input));

            // Build discriminator config string from source config
            var discConfig = "";
            if (transform.Source?.Discriminator != null)
            {
                var disc = transform.Source.Discriminator;
                switch (disc.Type)
                {
                    case DiscriminatorType.Position:
                        discConfig = ":pos " + (disc.Pos ?? 0).ToString(CultureInfo.InvariantCulture)
                            + " :len " + (disc.Len ?? 1).ToString(CultureInfo.InvariantCulture);
                        break;
                    case DiscriminatorType.Field:
                        discConfig = ":field " + (disc.Field ?? 0).ToString(CultureInfo.InvariantCulture);
                        break;
                    case DiscriminatorType.Path:
                        discConfig = ":path " + (disc.Path ?? "");
                        break;
                }
            }
            else if (transform.Source != null &&
                     transform.Source.Options.TryGetValue("discriminator", out var configStr))
            {
                discConfig = configStr;
            }

            // Join records into a single string for processing
            var delimiter = input.Delimiter ?? "\n";
            var rawInput = string.Join(delimiter, input.Records);
            var sourceFormat = transform.Source?.Format ?? "";

            return ExecuteMultiRecord(transform, rawInput, discConfig, sourceFormat);
        }

        /// <summary>
        /// Execute a parsed transform against source data and return a <see cref="TransformResult"/>.
        /// </summary>
        /// <param name="transform">The parsed transform specification.</param>
        /// <param name="source">The source data as a <see cref="DynValue"/>.</param>
        /// <returns>The transform result containing output, formatted string, and diagnostics.</returns>
        public static TransformResult Execute(OdinTransform transform, DynValue source)
            => Execute(transform, source, null);

        /// <summary>
        /// Execute a parsed transform against source data with execution options.
        /// </summary>
        /// <param name="transform">The parsed transform specification.</param>
        /// <param name="source">The source data as a <see cref="DynValue"/>.</param>
        /// <param name="options">Execution options (e.g., an import resolver). May be null.</param>
        /// <returns>The transform result containing output, formatted string, and diagnostics.</returns>
        public static TransformResult Execute(OdinTransform transform, DynValue source, TransformOptions? options)
        {
            if (options?.ImportResolver != null && transform.Imports.Count > 0)
                ResolveImports(transform, options.ImportResolver);

            // Check for multi-record mode
            if (transform.Source != null)
            {
                string? discConfig = null;
                if (transform.Source.Discriminator != null)
                {
                    // Build config string from structured discriminator
                    var disc = transform.Source.Discriminator;
                    if (disc.Type == DiscriminatorType.Position && disc.Pos.HasValue && disc.Len.HasValue)
                        discConfig = $":pos {disc.Pos.Value} :len {disc.Len.Value}";
                    else if (disc.Type == DiscriminatorType.Field && disc.Field.HasValue)
                        discConfig = $":field {disc.Field.Value}";
                    else if (disc.Type == DiscriminatorType.Path && disc.Path != null)
                        discConfig = $":path {disc.Path}";
                }
                if (discConfig == null && transform.Source.Options != null)
                    transform.Source.Options.TryGetValue("discriminator", out discConfig);
                if (discConfig != null && source.Type == DynValueType.String)
                {
                    return ExecuteMultiRecord(transform, source.AsString()!, discConfig, transform.Source.Format);
                }
            }

            // If source is raw string, try to parse it
            if (source.Type == DynValueType.String)
            {
                string? srcFmt = null;
                if (transform.Source != null && !string.IsNullOrEmpty(transform.Source.Format))
                    srcFmt = transform.Source.Format;
                else if (transform.Metadata.Direction != null)
                {
                    var parts = transform.Metadata.Direction.Split(new[] { "->" }, StringSplitOptions.None);
                    if (parts.Length > 0) srcFmt = parts[0];
                }

                if (srcFmt == "odin" && OdinSourceParser != null)
                {
                    var parsed = OdinSourceParser(source.AsString()!);
                    return Execute(transform, parsed);
                }

                if (srcFmt != null && IsParseableFormat(srcFmt) && SourceParser != null)
                {
                    var parsed = SourceParser(source.AsString()!, srcFmt);
                    if (parsed != null)
                        return Execute(transform, parsed);
                }
            }

            // 1. Build execution context
            var ctx = BuildContext(transform, source);

            // 2. Build output
            var output = DynValue.Object(new List<KeyValuePair<string, DynValue>>());

            // 3. Order segments by pass
            var ordered = OrderSegmentsByPass(transform.Segments);

            bool isFirstPass = true;
            int? currentPass = null;
            // Conditional chain state: "none" (no chain), "pending" (chain open, no branch taken), "taken".
            string branch = "none";
            foreach (var seg in ordered)
            {
                // Reset non-persist accumulators at pass transitions
                int? segPass = seg.Pass;
                if (!Equals(segPass, currentPass))
                {
                    if (!isFirstPass)
                    {
                        foreach (var kvp in transform.Accumulators)
                        {
                            if (!kvp.Value.Persist)
                            {
                                ctx.Accumulators[kvp.Key] = OdinValueToDyn(kvp.Value.Initial);
                            }
                        }
                    }
                    isFirstPass = false;
                    currentPass = segPass;
                    // Chains do not span pass boundaries.
                    branch = "none";
                }

                ProcessChainSegment(seg, ctx, ref output, ref branch);
                ctx.GlobalOutput = output;
            }

            // 4. Apply confidential enforcement
            if (ctx.EnforceConfidential.HasValue)
                ApplyConfidentialEnforcement(transform.Segments, ctx.EnforceConfidential.Value, ref output);

            // 5. Format the output
            string formatted = FormatOutput(output, transform.Target.Format, transform.Target.Options,
                transform.Segments, ctx.FieldModifiers, transform.Target.Namespaces, ctx.Errors.Add, ctx.Warnings.Add);

            return new TransformResult
            {
                Success = ctx.Errors.Count == 0,
                Output = output,
                Formatted = formatted,
                Errors = ctx.Errors,
                Warnings = ctx.Warnings,
                OutputModifiers = ctx.FieldModifiers,
            };
        }

        // ─────────────────────────────────────────────────────────────────────
        // Multi-record execution
        // ─────────────────────────────────────────────────────────────────────

        private enum DiscriminatorMode
        {
            Position,
            Field,
        }

        private static (DiscriminatorMode Mode, int Pos, int Len, int FieldIndex)? ParseDiscriminatorConfig(string config)
        {
            var parts = config.Split((char[])null!, StringSplitOptions.RemoveEmptyEntries);
            int? pos = null, len = null, field = null;

            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i] == ":pos" && i + 1 < parts.Length)
                {
                    if (int.TryParse(parts[i + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var v))
                        pos = v;
                    i++;
                }
                else if (parts[i] == ":len" && i + 1 < parts.Length)
                {
                    if (int.TryParse(parts[i + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var v))
                        len = v;
                    i++;
                }
                else if (parts[i] == ":field" && i + 1 < parts.Length)
                {
                    if (int.TryParse(parts[i + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var v))
                        field = v;
                    i++;
                }
            }

            if (field.HasValue)
                return (DiscriminatorMode.Field, 0, 0, field.Value);
            if (pos.HasValue && len.HasValue)
                return (DiscriminatorMode.Position, pos.Value, len.Value, 0);
            return null;
        }

        private static string ExtractDiscriminatorValue(string line, DiscriminatorMode mode, int pos, int len, int fieldIndex, string delimiter)
        {
            if (mode == DiscriminatorMode.Position)
            {
                if (pos + len <= line.Length)
                    return line.Substring(pos, len).Trim();
                if (pos < line.Length)
                    return line.Substring(pos).Trim();
                return "";
            }
            else
            {
                var fields = line.Split(new[] { delimiter }, StringSplitOptions.None);
                if (fieldIndex < fields.Length)
                    return fields[fieldIndex].Trim();
                return "";
            }
        }

        private static DynValue ParseRecord(string line, string format, string delimiter)
        {
            var entries = new List<KeyValuePair<string, DynValue>>
            {
                new KeyValuePair<string, DynValue>("_raw", DynValue.String(line)),
                new KeyValuePair<string, DynValue>("_line", DynValue.String(line)),
            };

            if (format == "csv" || format == "delimited")
            {
                var fields = line.Split(new[] { delimiter }, StringSplitOptions.None);
                for (int i = 0; i < fields.Length; i++)
                    entries.Add(new KeyValuePair<string, DynValue>(i.ToString(CultureInfo.InvariantCulture), DynValue.String(fields[i])));
            }

            return DynValue.Object(entries);
        }

        private static TransformResult ExecuteMultiRecord(
            OdinTransform transform, string rawInput, string discConfig, string sourceFormat)
        {
            var parsed = ParseDiscriminatorConfig(discConfig);
            if (!parsed.HasValue)
            {
                return new TransformResult
                {
                    Success = false,
                    Errors = new List<TransformError>
                    {
                        new TransformError { Message = "Invalid discriminator config: " + discConfig }
                    },
                };
            }

            var (mode, pos, len, fieldIndex) = parsed.Value;

            string delimiter = ",";
            if (transform.Source?.Options != null && transform.Source.Options.TryGetValue("delimiter", out var delimVal))
                delimiter = delimVal;

            // Build segment routing map
            var segmentMap = new Dictionary<string, TransformSegment>();
            foreach (var seg in transform.Segments)
            {
                foreach (var mapping in seg.Mappings)
                {
                    if (mapping.Target == "_type" && mapping.Expression is LiteralExpression litExpr)
                    {
                        var typeStr = litExpr.Value is OdinString s ? s.Value : null;
                        if (typeStr != null)
                        {
                            foreach (var typeVal in typeStr.Split(','))
                                segmentMap[typeVal.Trim()] = seg;
                        }
                    }
                }
            }

            var ctx = BuildContext(transform, DynValue.Null());
            ctx.SourceFormat = sourceFormat;

            var output = DynValue.Object(new List<KeyValuePair<string, DynValue>>());
            var arrayAccumulators = new Dictionary<string, List<DynValue>>();

            // Initialize array accumulators
            foreach (var seg in transform.Segments)
            {
                if (seg.Name.EndsWith("[]", StringComparison.Ordinal))
                {
                    var arrName = seg.Name.Substring(0, seg.Name.Length - 2);
                    arrayAccumulators[arrName] = new List<DynValue>();
                }
            }

            // Process each record
            var lines = rawInput.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                if (line.Trim().Length == 0) continue;

                var discValue = ExtractDiscriminatorValue(line, mode, pos, len, fieldIndex, delimiter);
                if (!segmentMap.TryGetValue(discValue, out var segment)) continue;

                var recordSource = ParseRecord(line, sourceFormat, delimiter);
                var recordOutput = DynValue.Object(new List<KeyValuePair<string, DynValue>>());

                foreach (var item in segment.Items)
                {
                    var m = item.AsMapping();
                    if (m != null)
                    {
                        if (m.Target == "_type") continue;
                        ProcessMapping(m, ctx, recordSource, ref recordOutput, "");
                    }
                    var child = item.AsChild();
                    if (child != null)
                    {
                        foreach (var cm in child.Mappings)
                        {
                            var fullTarget = child.Name + "." + cm.Target;
                            var wrapper = new FieldMapping
                            {
                                Target = fullTarget,
                                Expression = cm.Expression,
                                Directives = cm.Directives,
                                Modifiers = cm.Modifiers,
                            };
                            ProcessMapping(wrapper, ctx, recordSource, ref recordOutput, "");
                        }
                    }
                }

                // Merge into output
                var segName = segment.Name.EndsWith("[]", StringComparison.Ordinal)
                    ? segment.Name.Substring(0, segment.Name.Length - 2)
                    : segment.Name;

                if (segment.Name.EndsWith("[]", StringComparison.Ordinal))
                {
                    if (arrayAccumulators.TryGetValue(segName, out var accList))
                        accList.Add(recordOutput);
                }
                else
                {
                    MergeRecordIntoOutput(ref output, segName, recordOutput);
                }
            }

            // Merge array accumulators into output in segment order
            var outputEntries = output.AsObject();
            if (outputEntries != null)
            {
                foreach (var seg in transform.Segments)
                {
                    if (!seg.Name.EndsWith("[]", StringComparison.Ordinal)) continue;
                    var arrName = seg.Name.Substring(0, seg.Name.Length - 2);
                    if (arrayAccumulators.TryGetValue(arrName, out var items))
                    {
                        outputEntries.Add(new KeyValuePair<string, DynValue>(arrName, DynValue.Array(items)));
                    }
                }
            }

            string formatted = FormatOutput(output, transform.Target.Format, transform.Target.Options,
                transform.Segments, ctx.FieldModifiers, transform.Target.Namespaces, ctx.Errors.Add, ctx.Warnings.Add);

            return new TransformResult
            {
                Success = ctx.Errors.Count == 0,
                Output = output,
                Formatted = formatted,
                Errors = ctx.Errors,
                Warnings = ctx.Warnings,
                OutputModifiers = ctx.FieldModifiers,
            };
        }

        private static void MergeRecordIntoOutput(ref DynValue output, string segName, DynValue recordOutput)
        {
            var entries = output.AsObject();
            if (entries == null) return;
            var recEntries = recordOutput.AsObject();
            if (recEntries == null) return;

            int existingIdx = -1;
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].Key == segName) { existingIdx = i; break; }
            }

            if (existingIdx >= 0)
            {
                var existing = entries[existingIdx].Value.AsObject();
                if (existing != null)
                {
                    foreach (var kvp in recEntries)
                        existing.Add(kvp);
                }
            }
            else
            {
                entries.Add(new KeyValuePair<string, DynValue>(segName, DynValue.Object(recEntries)));
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Context building
        // ─────────────────────────────────────────────────────────────────────

        // Merge imported lookup tables, constants, accumulators, and named segments
        // into this transform. Local declarations win over imported ones; imported
        // segments are appended so their mappings remain referenceable. An import the
        // resolver cannot satisfy (null) is skipped.
        private static void ResolveImports(OdinTransform transform, Func<string, string?> resolver)
        {
            var seen = new HashSet<string>();
            foreach (var imp in transform.Imports)
            {
                if (!seen.Add(imp.Path)) continue;

                var text = resolver(imp.Path);
                if (text == null) continue;

                var imported = TransformParser.Parse(text);

                foreach (var kvp in imported.Tables)
                    if (!transform.Tables.ContainsKey(kvp.Key)) transform.Tables[kvp.Key] = kvp.Value;
                foreach (var kvp in imported.Constants)
                    if (!transform.Constants.ContainsKey(kvp.Key)) transform.Constants[kvp.Key] = kvp.Value;
                foreach (var kvp in imported.Accumulators)
                    if (!transform.Accumulators.ContainsKey(kvp.Key)) transform.Accumulators[kvp.Key] = kvp.Value;

                var existingPaths = new HashSet<string>();
                foreach (var s in transform.Segments) existingPaths.Add(s.Path);
                foreach (var segment in imported.Segments)
                {
                    if (string.IsNullOrEmpty(segment.Path) || existingPaths.Contains(segment.Path)) continue;
                    transform.Segments.Add(segment);
                }
            }
        }

        private static ExecContext BuildContext(OdinTransform transform, DynValue source)
        {
            var constants = new Dictionary<string, DynValue>();
            foreach (var kvp in transform.Constants)
                constants[kvp.Key] = OdinValueToDyn(kvp.Value);

            var accumulators = new Dictionary<string, DynValue>();
            foreach (var kvp in transform.Accumulators)
                accumulators[kvp.Key] = OdinValueToDyn(kvp.Value.Initial);

            var tables = new Dictionary<string, LookupTable>(transform.Tables);

            string sourceFormat = "";
            if (transform.Source != null && !string.IsNullOrEmpty(transform.Source.Format))
                sourceFormat = transform.Source.Format;
            else if (transform.Metadata.Direction != null)
            {
                var parts = transform.Metadata.Direction.Split(new[] { "->" }, StringSplitOptions.None);
                if (parts.Length > 0) sourceFormat = parts[0];
            }

            return new ExecContext
            {
                Source = source,
                Constants = constants,
                Accumulators = accumulators,
                Tables = tables,
                LoopVars = new Dictionary<string, DynValue>(),
                // Built-in verbs never change after startup and ctx.Verbs is read-only
                // during execution; share the static registry instead of copying it per call.
                Verbs = VerbRegistry,
                Warnings = new List<TransformWarning>(),
                Errors = new List<TransformError>(),
                EnforceConfidential = transform.EnforceConfidential,
                GlobalOutput = DynValue.Object(new List<KeyValuePair<string, DynValue>>()),
                FieldModifiers = new Dictionary<string, OdinModifiers>(),
                SourceFormat = sourceFormat,
                Target = transform.Target,
                StrictTypes = transform.StrictTypes,
            };
        }

        // ─────────────────────────────────────────────────────────────────────
        // Segment ordering
        // ─────────────────────────────────────────────────────────────────────

        private static List<TransformSegment> OrderSegmentsByPass(List<TransformSegment> segments)
        {
            var refs = new List<TransformSegment>(segments);
            refs.Sort((a, b) =>
            {
                int aKey = (a.Pass == null || a.Pass == 0) ? int.MaxValue : a.Pass.Value;
                int bKey = (b.Pass == null || b.Pass == 0) ? int.MaxValue : b.Pass.Value;
                return aKey.CompareTo(bKey);
            });
            return refs;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Segment processing
        // ─────────────────────────────────────────────────────────────────────

        // Applies a single segment within an if/elif/else chain, advancing the
        // shared branch state. A chain is a run of consecutive segments: one `if`,
        // then any `elif`, then an optional `else`. Only the first branch whose
        // condition holds is emitted; the rest are skipped. Any unconditional
        // segment breaks the chain.
        private static void ProcessChainSegment(TransformSegment segment, ExecContext ctx, ref DynValue output, ref string branch)
        {
            switch (segment.ConditionKind)
            {
                case "if":
                {
                    bool taken = EvaluateSegmentCondition(segment, ctx);
                    branch = taken ? "taken" : "pending";
                    if (taken) ProcessSegment(segment, ctx, ref output, "");
                    break;
                }
                case "elif":
                {
                    if (branch == "none")
                    {
                        ctx.Errors.Add(new TransformError
                        {
                            Code = TransformErrorCode.DanglingBranch.Code(),
                            Message = "'elif' segment has no preceding 'if'",
                            Path = segment.Name,
                        });
                        return;
                    }
                    if (branch == "taken") return;
                    bool taken = EvaluateSegmentCondition(segment, ctx);
                    branch = taken ? "taken" : "pending";
                    if (taken) ProcessSegment(segment, ctx, ref output, "");
                    break;
                }
                case "else":
                {
                    if (branch == "none")
                    {
                        ctx.Errors.Add(new TransformError
                        {
                            Code = TransformErrorCode.DanglingBranch.Code(),
                            Message = "'else' segment has no preceding 'if'",
                            Path = segment.Name,
                        });
                        return;
                    }
                    if (branch == "pending") ProcessSegment(segment, ctx, ref output, "");
                    branch = "none";
                    break;
                }
                default:
                    branch = "none";
                    ProcessSegment(segment, ctx, ref output, "");
                    break;
            }
        }

        private static void ProcessSegment(TransformSegment segment, ExecContext ctx, ref DynValue output, string pathPrefix)
        {
            // Guard condition (chained segments are pre-filtered by ProcessChainSegment;
            // this also covers child segments reached directly).
            if (segment.ConditionExpr != null || segment.Condition != null)
            {
                if (!EvaluateSegmentCondition(segment, ctx)) return;
            }

            // Check discriminator
            if (segment.SegmentDiscriminator != null)
            {
                var discVal = ResolvePath(ctx.Source, segment.SegmentDiscriminator.Path, ctx.Constants, ctx.Accumulators);
                bool matches = false;
                switch (discVal.Type)
                {
                    case DynValueType.String: matches = discVal.AsString() == segment.SegmentDiscriminator.Value; break;
                    case DynValueType.Integer: matches = discVal.AsInt64()?.ToString(CultureInfo.InvariantCulture) == segment.SegmentDiscriminator.Value; break;
                    case DynValueType.Float: matches = discVal.AsDouble()?.ToString(CultureInfo.InvariantCulture) == segment.SegmentDiscriminator.Value; break;
                    case DynValueType.Bool: matches = (discVal.AsBool() == true ? "true" : "false") == segment.SegmentDiscriminator.Value; break;
                }
                if (!matches) return;
            }

            var segName = segment.Name;
            var cleanName = segName.EndsWith("[]", StringComparison.Ordinal) ? segName.Substring(0, segName.Length - 2) : segName;
            var arrayIndex = ParseArrayIndex(cleanName);
            bool isRoot = string.IsNullOrEmpty(cleanName) || cleanName == "$" || cleanName == "_root";

            string currentPrefix = isRoot
                ? pathPrefix
                : (string.IsNullOrEmpty(pathPrefix) ? cleanName : pathPrefix + "." + cleanName);

            // Side-effect-only segments: names starting with "_" (e.g., "_calcSubtotal")
            // Execute mappings for side effects (like accumulate) but don't write to output.
            if (!isRoot && cleanName.StartsWith("_", StringComparison.Ordinal) && arrayIndex == null)
            {
                var dummyOutput = DynValue.Object(new List<KeyValuePair<string, DynValue>>());

                // A looping sink iterates its source so accumulators see every item.
                if (segment.SourcePath != null)
                {
                    var sinkSource = ResolvePath(ctx.Source, segment.SourcePath, ctx.Constants, ctx.Accumulators);
                    var sinkArr = sinkSource.Type == DynValueType.Array
                        ? sinkSource.AsArray()
                        : (sinkSource.Type == DynValueType.Null ? new List<DynValue>() : new List<DynValue> { sinkSource });
                    if (sinkArr != null)
                    {
                        for (int idx = 0; idx < sinkArr.Count; idx++)
                        {
                            var item = sinkArr[idx];
                            ctx.LoopVars["_item"] = item;
                            ctx.LoopVars["_index"] = DynValue.Integer(idx);
                            ctx.LoopVars["_length"] = DynValue.Integer(sinkArr.Count);
                            if (segment.Counter != null)
                            {
                                ctx.Accumulators[segment.Counter] = DynValue.Integer(idx);
                                ctx.LoopVars[segment.Counter] = DynValue.Integer(idx);
                            }
                            foreach (var mapping in segment.Mappings)
                                ProcessMapping(mapping, ctx, item, ref dummyOutput, currentPrefix);
                            ctx.LoopVars.Remove("_item");
                            ctx.LoopVars.Remove("_index");
                            ctx.LoopVars.Remove("_length");
                        }
                    }
                    return;
                }

                if (segment.Items.Count > 0)
                {
                    foreach (var item in segment.Items)
                    {
                        var m = item.AsMapping();
                        if (m != null) ProcessMapping(m, ctx, ctx.Source, ref dummyOutput, currentPrefix);
                    }
                }
                else
                {
                    foreach (var mapping in segment.Mappings)
                        ProcessMapping(mapping, ctx, ctx.Source, ref dummyOutput, currentPrefix);
                }
                return;
            }

            // Literal block: emit interpolated text lines instead of field mappings.
            if (segment.IsLiteral)
            {
                ProcessLiteralSegment(segment, ctx, ref output, cleanName, isRoot);
                return;
            }

            // Nested loops: drive a cross-product over multiple :loop directives.
            if (segment.Loops.Count > 1 && segment.IsArray)
            {
                var results = new List<DynValue>();
                // A non-array loop source raises a coded error honoring onError.
                try
                {
                    IterateLoops(segment.Loops, 0, ctx, segment, ctx.Source, currentPrefix, results, null);
                }
                catch (CodedTransformException ex)
                {
                    EmitLoopError(ctx, ex.Error);
                    return;
                }
                var arr = DynValue.Array(results);
                if (isRoot) output = arr;
                else SetPath(ref output, cleanName, arr);
                return;
            }

            // Array loop
            if (segment.SourcePath != null)
            {
                // Single :loop with an alias binds the alias to each item.
                string? singleAlias = segment.Loops.Count == 1 ? segment.Loops[0].Alias : null;
                var sourceVal = ResolvePath(ctx.Source, segment.SourcePath, ctx.Constants, ctx.Accumulators);
                // Absent/null source produces an empty array (no iterations).
                if (sourceVal.Type == DynValueType.Null)
                {
                    var emptyArr = DynValue.Array(new List<DynValue>());
                    if (isRoot)
                        output = emptyArr;
                    else
                        SetPath(ref output, cleanName, emptyArr);
                    return;
                }
                // T009: a present non-array scalar loop source is an error.
                if (sourceVal.Type != DynValueType.Array)
                {
                    EmitLoopError(ctx, new TransformError
                    {
                        Code = TransformErrorCode.LoopSourceNotArray.Code(),
                        Message = "Loop source path '" + segment.SourcePath + "' does not resolve to an array",
                        Path = segment.SourcePath,
                    });
                    return;
                }
                var arrayVal = sourceVal;
                var items = arrayVal.AsArray();
                if (items != null)
                {
                    var resultItems = new List<DynValue>();
                    var isValueOnly = segment.Mappings.All(m => m.Target == "_");
                    for (int idx = 0; idx < items.Count; idx++)
                    {
                        var item = items[idx];
                        ctx.LoopVars["_item"] = item;
                        ctx.LoopVars["_index"] = DynValue.Integer(idx);
                        ctx.LoopVars["_length"] = DynValue.Integer(items.Count);
                        if (singleAlias != null)
                            ctx.Aliases[singleAlias] = item;

                        // A :counter is readable by its name and via @$accumulator.<name>.
                        if (segment.Counter != null)
                        {
                            ctx.Accumulators[segment.Counter] = DynValue.Integer(idx);
                            ctx.LoopVars[segment.Counter] = DynValue.Integer(idx);
                        }

                        var itemOutput = DynValue.Object(new List<KeyValuePair<string, DynValue>>());
                        foreach (var mapping in segment.Mappings)
                        {
                            if (mapping.Target == "_")
                            {
                                var outputSnapshot = itemOutput;
                                try
                                {
                                    var val = EvaluateExpression(mapping.Expression, ctx, item, outputSnapshot);
                                    val = ApplyMappingDirectives(val, mapping.Directives, ctx.SourceFormat, mapping.Expression);
                                    if (isValueOnly)
                                        itemOutput = val;
                                    // else: side effect only (e.g., accumulator updates)
                                }
                                catch (Exception e)
                                {
                                    ctx.Errors.Add(new TransformError { Message = "mapping '_': " + e.Message, Path = "_" });
                                }
                            }
                            else
                            {
                                ProcessMapping(mapping, ctx, item, ref itemOutput, currentPrefix);
                            }
                        }
                        resultItems.Add(itemOutput);

                        ctx.LoopVars.Remove("_item");
                        ctx.LoopVars.Remove("_index");
                        ctx.LoopVars.Remove("_length");
                        if (singleAlias != null)
                            ctx.Aliases.Remove(singleAlias);
                    }
                    var arrResult = DynValue.Array(resultItems);
                    if (isRoot)
                        output = arrResult;
                    else
                        SetPath(ref output, cleanName, arrResult);
                }
                else
                {
                    ctx.Warnings.Add(new TransformWarning
                    {
                        Message = $"segment '{segName}': source_path '{segment.SourcePath}' did not resolve to an array",
                        Path = segment.SourcePath,
                    });
                }
            }
            else if (segment.Items.Count > 0)
            {
                // Use interleaved items list
                if (isRoot)
                {
                    foreach (var item in segment.Items)
                    {
                        var m = item.AsMapping();
                        if (m != null) ProcessMapping(m, ctx, ctx.Source, ref output, currentPrefix);
                        var child = item.AsChild();
                        if (child != null) ProcessSegment(child, ctx, ref output, currentPrefix);
                    }
                }
                else if (arrayIndex != null)
                {
                    EnsureArrayEntryAt(ref output, arrayIndex.Value.Name, arrayIndex.Value.Index);
                    foreach (var item in segment.Items)
                    {
                        var m = item.AsMapping();
                        if (m != null)
                        {
                            var target = GetArrayEntryRef(ref output, arrayIndex.Value.Name, arrayIndex.Value.Index);
                            if (target != null) ProcessMapping(m, ctx, ctx.Source, ref target, currentPrefix);
                            SetArrayEntry(ref output, arrayIndex.Value.Name, arrayIndex.Value.Index, target ?? DynValue.Null());
                        }
                        var child = item.AsChild();
                        if (child != null)
                        {
                            var target = GetArrayEntryRef(ref output, arrayIndex.Value.Name, arrayIndex.Value.Index);
                            if (target != null) ProcessSegment(child, ctx, ref target, currentPrefix);
                            SetArrayEntry(ref output, arrayIndex.Value.Name, arrayIndex.Value.Index, target ?? DynValue.Null());
                        }
                    }
                }
                else
                {
                    EnsureObjectAtPath(ref output, cleanName);
                    foreach (var item in segment.Items)
                    {
                        var m = item.AsMapping();
                        if (m != null)
                        {
                            var target = GetMutPathDeep(ref output, cleanName);
                            if (target != null) ProcessMapping(m, ctx, ctx.Source, ref target, currentPrefix);
                            SetObjectFieldDeep(ref output, cleanName, target ?? DynValue.Null());
                        }
                        var child = item.AsChild();
                        if (child != null)
                        {
                            var target = GetMutPathDeep(ref output, cleanName) ?? output;
                            ProcessSegment(child, ctx, ref target, currentPrefix);
                            SetObjectFieldDeep(ref output, cleanName, target);
                        }
                    }
                }
            }
            else
            {
                // Fallback: process mappings then children separately
                if (isRoot)
                {
                    foreach (var mapping in segment.Mappings)
                        ProcessMapping(mapping, ctx, ctx.Source, ref output, currentPrefix);
                }
                else if (arrayIndex != null)
                {
                    EnsureArrayEntryAt(ref output, arrayIndex.Value.Name, arrayIndex.Value.Index);
                    var target = GetArrayEntryRef(ref output, arrayIndex.Value.Name, arrayIndex.Value.Index);
                    if (target != null)
                    {
                        foreach (var mapping in segment.Mappings)
                            ProcessMapping(mapping, ctx, ctx.Source, ref target, currentPrefix);
                        SetArrayEntry(ref output, arrayIndex.Value.Name, arrayIndex.Value.Index, target);
                    }
                }
                else
                {
                    EnsureObjectAtPath(ref output, cleanName);
                    var target = GetMutPathDeep(ref output, cleanName);
                    if (target != null)
                    {
                        foreach (var mapping in segment.Mappings)
                            ProcessMapping(mapping, ctx, ctx.Source, ref target, currentPrefix);
                        SetObjectFieldDeep(ref output, cleanName, target);
                    }
                }

                foreach (var child in segment.Children)
                {
                    if (isRoot)
                    {
                        ProcessSegment(child, ctx, ref output, currentPrefix);
                    }
                    else if (arrayIndex != null)
                    {
                        var childTarget = GetArrayEntryRef(ref output, arrayIndex.Value.Name, arrayIndex.Value.Index) ?? output;
                        ProcessSegment(child, ctx, ref childTarget, currentPrefix);
                        SetArrayEntry(ref output, arrayIndex.Value.Name, arrayIndex.Value.Index, childTarget);
                    }
                    else
                    {
                        var childTarget = GetMutPathDeep(ref output, cleanName) ?? output;
                        ProcessSegment(child, ctx, ref childTarget, currentPrefix);
                        SetObjectFieldDeep(ref output, cleanName, childTarget);
                    }
                }
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Mapping processing
        // ─────────────────────────────────────────────────────────────────────

        private static void ProcessMapping(FieldMapping mapping, ExecContext ctx, DynValue currentSource, ref DynValue output, string pathPrefix)
        {
            var outputSnapshot = output;
            var mods = GetMappingMods(mapping);
            try
            {
                // Field-level :if / :unless guards (truthy path or `path op value`).
                var ifDir = mods.IfDir;
                if (ifDir != null && !EvaluateFieldCondition(ifDir.Value?.AsString() ?? "", ctx, currentSource, outputSnapshot))
                    return;
                var unlessDir = mods.UnlessDir;
                if (unlessDir != null && EvaluateFieldCondition(unlessDir.Value?.AsString() ?? "", ctx, currentSource, outputSnapshot))
                    return;

                // A :default rescues a missing lookup; suppress errors raised during evaluation.
                bool hasDefaultModifier = mods.DefaultDir != null;
                int errorsBefore = hasDefaultModifier ? ctx.Errors.Count : 0;

                DynValue val;
                var objectDir = mods.ObjectDir;
                if (objectDir != null)
                    val = BuildInlineObject(objectDir.Value?.AsString() ?? "", ctx, currentSource, outputSnapshot);
                else
                    val = EvaluateExpression(mapping.Expression, ctx, currentSource, outputSnapshot);

                // If a :default rescued a null result, drop errors raised during evaluation.
                if (hasDefaultModifier && ctx.Errors.Count > errorsBefore)
                    ctx.Errors.RemoveRange(errorsBefore, ctx.Errors.Count - errorsBefore);

                val = ApplyMappingDirectives(val, mapping.Directives, ctx.SourceFormat, mapping.Expression);

                // T007: a format-specific modifier used with an incompatible target
                // format is ignored and reported as a warning.
                string targetFormat = ctx.Target?.Format ?? "";
                foreach (var dir in mapping.Directives)
                {
                    if (!IsModifierCompatible(dir.Name, targetFormat))
                        ctx.Warnings.Add(new TransformWarning
                        {
                            Code = TransformErrorCode.InvalidModifier.Code(),
                            Message = "Modifier ':" + dir.Name + "' is not applicable to format '"
                                + targetFormat + "' and will be ignored",
                            Path = mapping.Target,
                        });
                }

                // Validation modifiers: :validate / :enum / :range (honors onValidation policy).
                if (mods.ValidationActive && !ValidateFieldValue(val, mapping, ctx, mods)) return;

                // Missing source path: a required field always fails (T005); an ordinary
                // field honors the onMissing policy (fail -> T005, warn -> warning,
                // skip/default -> keep null). A path present with a null value is not
                // "missing" — a required present-null field is SOURCE_MISSING.
                bool isRequired = mapping.Modifiers != null && mapping.Modifiers.Required;
                if (val.Type == DynValueType.Null && IsCopySourceAbsent(mapping, ctx, currentSource, mods))
                {
                    var rawPath = ((CopyExpression)mapping.Expression).Path;
                    var cleanPath = rawPath.StartsWith("@", StringComparison.Ordinal) ? rawPath.Substring(1) : rawPath;
                    if (cleanPath.StartsWith(".", StringComparison.Ordinal)) cleanPath = cleanPath.Substring(1);

                    if (isRequired)
                    {
                        ctx.Errors.Add(new TransformError
                        {
                            Code = TransformErrorCode.SourcePathNotFound.Code(),
                            Message = "Source path not found: " + cleanPath,
                            Path = mapping.Target,
                        });
                        return;
                    }
                    var policy = OnMissingPolicy(ctx);
                    if (policy == "fail")
                    {
                        ctx.Errors.Add(new TransformError
                        {
                            Code = TransformErrorCode.SourcePathNotFound.Code(),
                            Message = "Source path not found: " + cleanPath,
                            Path = mapping.Target,
                        });
                        return;
                    }
                    if (policy == "warn")
                    {
                        ctx.Warnings.Add(new TransformWarning
                        {
                            Code = TransformErrorCode.SourcePathNotFound.Code(),
                            Message = "Source path not found: " + cleanPath,
                            Path = mapping.Target,
                        });
                    }
                }
                else if (isRequired && val.Type == DynValueType.Null)
                {
                    // Required field present but explicitly null.
                    ctx.Errors.Add(new TransformError
                    {
                        Code = "SOURCE_MISSING",
                        Message = "Required field '" + mapping.Target + "' is missing or null",
                        Path = mapping.Target,
                    });
                    return;
                }

                // :raw emits inline JSON structurally instead of an escaped string.
                if (mods.RawDir != null)
                    val = ParseRawJsonValue(val);

                // :array wraps the value in a single-element array.
                if (mods.ArrayDir != null)
                    val = DynValue.Array(new List<DynValue> { val });

                // Apply confidential at mapping level
                if (mapping.Modifiers != null && mapping.Modifiers.Confidential && ctx.EnforceConfidential.HasValue)
                    val = ApplyConfidentialToValue(val, ctx.EnforceConfidential.Value);

                // Any "_"-prefixed target is a computation-only sink: evaluated for side
                // effects (accumulators, counters) but never emitted to the output.
                if (!mapping.Target.StartsWith("_", StringComparison.Ordinal))
                {
                    SetPath(ref output, mapping.Target, val);

                    // Record field modifiers
                    if (mapping.Modifiers != null && mapping.Modifiers.HasAny)
                    {
                        var fullKey = string.IsNullOrEmpty(pathPrefix) ? mapping.Target : pathPrefix + "." + mapping.Target;
                        ctx.FieldModifiers[fullKey] = mapping.Modifiers;
                    }
                }
            }
            catch (Exception e)
            {
                // onError policy defaults to 'fail' — surface verb/transform errors.
                var onError = OnErrorPolicy(ctx);

                // Coded errors carry a stable T-code; preserve it under fail/warn.
                if (e is CodedTransformException coded)
                {
                    if (onError == "warn")
                        ctx.Warnings.Add(new TransformWarning
                        {
                            Code = coded.Error.Code,
                            Message = coded.Error.Message,
                            Path = mapping.Target,
                        });
                    else if (onError != "skip")
                        ctx.Errors.Add(new TransformError
                        {
                            Code = coded.Error.Code,
                            Message = coded.Error.Message,
                            Path = mapping.Target,
                        });
                    return;
                }

                if (onError == "warn")
                    ctx.Warnings.Add(new TransformWarning
                    {
                        Message = $"mapping '{mapping.Target}': {e.Message}",
                        Path = mapping.Target,
                    });
                else if (onError != "skip")
                    ctx.Errors.Add(new TransformError
                    {
                        Message = $"mapping '{mapping.Target}': {e.Message}",
                        Path = mapping.Target,
                    });
            }
        }

        // Surface a loop-level coded error honoring the onError policy.
        private static void EmitLoopError(ExecContext ctx, TransformError error)
        {
            var onError = OnErrorPolicy(ctx);
            if (onError == "warn")
                ctx.Warnings.Add(new TransformWarning
                {
                    Code = error.Code,
                    Message = error.Message,
                    Path = error.Path,
                });
            else if (onError != "skip")
                ctx.Errors.Add(error);
        }

        /// <summary>Resolve the onError policy (fail/warn/skip), defaulting to 'fail'.</summary>
        private static string OnErrorPolicy(ExecContext ctx)
        {
            if (ctx.Target != null && ctx.Target.Options.TryGetValue("onError", out var p) && p.Length > 0)
                return p;
            return "fail";
        }

        /// <summary>Resolve the onMissing policy (fail/warn/skip/default), or null for silent null.</summary>
        private static string? OnMissingPolicy(ExecContext ctx)
        {
            if (ctx.Target != null && ctx.Target.Options.TryGetValue("onMissing", out var p) && p.Length > 0)
                return p;
            return null;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Field-level modifier helpers
        // ─────────────────────────────────────────────────────────────────────

        private static OdinDirective? FindDirective(List<OdinDirective> directives, string name)
        {
            for (int i = 0; i < directives.Count; i++)
                if (directives[i].Name == name) return directives[i];
            return null;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Per-mapping modifier precompute
        // ─────────────────────────────────────────────────────────────────────

        // Directive references, derived flags, and compiled validation for a mapping.
        // Computed once per FieldMapping (its modifiers are data-independent and shared
        // across executions) and reused across records.
        private sealed class MappingMods
        {
            public OdinDirective? IfDir;
            public OdinDirective? UnlessDir;
            public OdinDirective? DefaultDir;
            public OdinDirective? ObjectDir;
            public OdinDirective? ValidateDir;
            public OdinDirective? EnumDir;
            public OdinDirective? RangeDir;
            public OdinDirective? RawDir;
            public OdinDirective? ArrayDir;
            public bool HasDefaultOrObject;
            public bool ValidationActive;

            // Precompiled :validate pattern. RegexValid is false when the pattern fails to compile.
            public System.Text.RegularExpressions.Regex? ValidateRegex;
            public bool ValidatePatternPresent;
            public bool RegexValid;

            // Precompiled :enum allowed set (preserves declared order for the label).
            public HashSet<string>? EnumSet;
            public List<string>? EnumValues;
            public bool EnumPresent;

            // Precompiled :range bounds.
            public bool RangePresent;
            public string? RangeStr;
            public double? RangeMin;
            public double? RangeMax;
        }

        private static readonly ConditionalWeakTable<FieldMapping, MappingMods> MappingModsCache =
            new ConditionalWeakTable<FieldMapping, MappingMods>();

        private static MappingMods GetMappingMods(FieldMapping mapping)
            => MappingModsCache.GetValue(mapping, BuildMappingMods);

        private static MappingMods BuildMappingMods(FieldMapping mapping)
        {
            var mods = new MappingMods
            {
                IfDir = FindDirective(mapping.Directives, "if"),
                UnlessDir = FindDirective(mapping.Directives, "unless"),
                DefaultDir = FindDirective(mapping.Directives, "default"),
                ObjectDir = FindDirective(mapping.Directives, "object"),
                ValidateDir = FindDirective(mapping.Directives, "validate"),
                EnumDir = FindDirective(mapping.Directives, "enum"),
                RangeDir = FindDirective(mapping.Directives, "range"),
                RawDir = FindDirective(mapping.Directives, "raw"),
                ArrayDir = FindDirective(mapping.Directives, "array"),
            };
            mods.HasDefaultOrObject = mods.DefaultDir != null || mods.ObjectDir != null;
            mods.ValidationActive = mods.ValidateDir != null || mods.EnumDir != null || mods.RangeDir != null;

            if (mods.ValidateDir != null && mods.ValidateDir.Value?.AsString() != null)
            {
                mods.ValidatePatternPresent = true;
                var pattern = mods.ValidateDir.Value!.AsString()!;
                try
                {
                    mods.ValidateRegex = new System.Text.RegularExpressions.Regex(pattern);
                    mods.RegexValid = true;
                }
                catch
                {
                    mods.RegexValid = false;
                }
            }

            if (mods.EnumDir != null && mods.EnumDir.Value?.AsString() != null)
            {
                mods.EnumPresent = true;
                var values = new List<string>();
                foreach (var v in mods.EnumDir.Value!.AsString()!.Split(','))
                    values.Add(v.Trim().Trim('"', '\''));
                mods.EnumValues = values;
                mods.EnumSet = new HashSet<string>(values);
            }

            if (mods.RangeDir != null && mods.RangeDir.Value?.AsString() != null)
            {
                mods.RangePresent = true;
                var rangeStr = mods.RangeDir.Value!.AsString()!;
                mods.RangeStr = rangeStr;
                var parts = rangeStr.Split(new[] { ".." }, StringSplitOptions.None);
                mods.RangeMin = parts.Length > 0 && double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var mn) ? mn : (double?)null;
                mods.RangeMax = parts.Length > 1 && double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var mx) ? mx : (double?)null;
            }

            return mods;
        }

        // Evaluate a field-level :if / :unless condition (truthy path or `path op value`).
        // The left path resolves against the current loop item when present, else source.
        private static bool EvaluateFieldCondition(string condition, ExecContext ctx, DynValue currentSource, DynValue currentOutput)
        {
            string trimmed = condition.Trim();
            var m = ConditionPattern.Match(trimmed);
            if (m.Success)
            {
                string pathPart = m.Groups[1].Value;
                string op = m.Groups[2].Value;
                string valuePart = m.Groups[3].Value.Trim();
                var left = ResolvePathWithOutput(currentSource, currentOutput, ctx.GlobalOutput, pathPart, ctx.Constants, ctx.Accumulators);
                var right = ParseConditionValue(valuePart);
                return CompareConditionValues(left, op, right);
            }
            var val = ResolvePathWithOutput(currentSource, currentOutput, ctx.GlobalOutput, trimmed, ctx.Constants, ctx.Accumulators);
            return IsTruthy(val);
        }

        // Build a structural object from an inline :object {key = @path, ...} spec.
        private static DynValue BuildInlineObject(string spec, ExecContext ctx, DynValue currentSource, DynValue currentOutput)
        {
            string trimmed = spec.Trim();
            if (trimmed.StartsWith("{", StringComparison.Ordinal)) trimmed = trimmed.Substring(1);
            if (trimmed.EndsWith("}", StringComparison.Ordinal)) trimmed = trimmed.Substring(0, trimmed.Length - 1);

            var entries = new List<KeyValuePair<string, DynValue>>();
            if (trimmed.Trim().Length > 0)
            {
                foreach (var pair in SplitObjectPairs(trimmed))
                {
                    int eq = pair.IndexOf('=');
                    if (eq < 0) continue;
                    string key = pair.Substring(0, eq).Trim();
                    string rhs = pair.Substring(eq + 1).Trim();
                    if (key.Length == 0) continue;
                    var (expr, _) = ParseFieldExpressionString(rhs);
                    var v = EvaluateExpression(expr, ctx, currentSource, currentOutput);
                    entries.Add(new KeyValuePair<string, DynValue>(key, v));
                }
            }
            return DynValue.Object(entries);
        }

        // Re-parse an inline object RHS expression (e.g. "@insured.name") into a field expression.
        private static (FieldExpression Expr, List<OdinDirective> Dirs) ParseFieldExpressionString(string rhs)
        {
            var trimmed = rhs.Trim();
            if (trimmed.StartsWith("@", StringComparison.Ordinal) || trimmed.StartsWith("%", StringComparison.Ordinal))
            {
                string clean = trimmed.StartsWith("@", StringComparison.Ordinal) ? trimmed.Substring(1) : trimmed;
                if (trimmed.StartsWith("@", StringComparison.Ordinal))
                    return (FieldExpression.Copy(clean), new List<OdinDirective>());
            }
            return (FieldExpression.Literal(new OdinString(trimmed)), new List<OdinDirective>());
        }

        // Split an inline object body on commas not nested inside braces.
        private static List<string> SplitObjectPairs(string body)
        {
            var pairs = new List<string>();
            int depth = 0;
            var current = new System.Text.StringBuilder();
            foreach (char ch in body)
            {
                if (ch == '{') depth++;
                else if (ch == '}') depth--;
                if (ch == ',' && depth == 0)
                {
                    pairs.Add(current.ToString());
                    current.Clear();
                }
                else current.Append(ch);
            }
            if (current.ToString().Trim().Length > 0) pairs.Add(current.ToString());
            return pairs;
        }

        // Parse a string value as JSON for :raw, producing a structural value.
        private static DynValue ParseRawJsonValue(DynValue val)
        {
            if (val.Type != DynValueType.String) return val;
            try
            {
                using var doc = JsonDocument.Parse(val.AsString() ?? "");
                return DynValue.FromJsonElement(doc.RootElement);
            }
            catch
            {
                return val;
            }
        }

        // Validate a value against :validate / :enum / :range modifiers.
        // Returns false when the field should be dropped (onValidation = skip or fail).
        private static bool ValidateFieldValue(DynValue val, FieldMapping mapping, ExecContext ctx, MappingMods mods)
        {
            if (val.Type == DynValueType.Null) return true;
            if (!mods.ValidationActive) return true;

            string policy = "fail";
            if (ctx.Target != null && ctx.Target.Options.TryGetValue("onValidation", out var p))
                policy = p;

            var failures = new List<string>();

            if (mods.ValidatePatternPresent)
            {
                string pattern = mods.ValidateDir!.Value!.AsString()!;
                string str = CoerceToString(val);
                if (!mods.RegexValid)
                    failures.Add($"invalid validation pattern '{pattern}'");
                else if (!mods.ValidateRegex!.IsMatch(str))
                    failures.Add($"value '{str}' does not match pattern '{pattern}'");
            }

            if (mods.EnumPresent)
            {
                string str = CoerceToString(val);
                if (!mods.EnumSet!.Contains(str))
                    failures.Add($"value '{str}' is not one of [{string.Join(", ", mods.EnumValues!)}]");
            }

            if (mods.RangePresent)
            {
                string rangeStr = mods.RangeStr!;
                double? min = mods.RangeMin;
                double? max = mods.RangeMax;
                var num = ToComparableNumber(val);
                if (num == null)
                    failures.Add($"value '{CoerceToString(val)}' is not numeric for range {rangeStr}");
                else if ((min.HasValue && num.Value < min.Value) || (max.HasValue && num.Value > max.Value))
                    failures.Add($"value {num.Value.ToString(CultureInfo.InvariantCulture)} is outside range {rangeStr}");
            }

            if (failures.Count == 0) return true;

            string message = $"Validation failed for '{mapping.Target}': {string.Join("; ", failures)}";
            if (policy == "warn")
            {
                ctx.Warnings.Add(new TransformWarning { Message = message, Path = mapping.Target });
                return true;
            }
            if (policy == "skip")
                return false;

            ctx.Errors.Add(new TransformError
            {
                Code = TransformErrorCode.ValidationFailed.Code(),
                Message = message,
                Path = mapping.Target,
            });
            return false;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Expression evaluation
        // ─────────────────────────────────────────────────────────────────────

        private static DynValue EvaluateExpression(FieldExpression expr, ExecContext ctx, DynValue currentSource, DynValue currentOutput)
        {
            switch (expr)
            {
                case CopyExpression copy:
                {
                    var path = copy.Path;
                    // Loop variable awareness
                    if (path.StartsWith("_item", StringComparison.Ordinal) || path.StartsWith("@_item", StringComparison.Ordinal))
                    {
                        var clean = path.StartsWith("@", StringComparison.Ordinal) ? path.Substring(1) : path;
                        if (ctx.LoopVars.TryGetValue("_item", out var item))
                        {
                            if (clean == "_item") return item;
                            var remaining = clean.StartsWith("_item.", StringComparison.Ordinal) ? clean.Substring(6) : "";
                            return string.IsNullOrEmpty(remaining) ? item : ResolveSubPath(item, remaining);
                        }
                    }
                    if (path.StartsWith("_index", StringComparison.Ordinal) || path.StartsWith("@_index", StringComparison.Ordinal))
                    {
                        if (ctx.LoopVars.TryGetValue("_index", out var idx)) return idx;
                    }
                    if (path.StartsWith("_length", StringComparison.Ordinal) || path.StartsWith("@_length", StringComparison.Ordinal))
                    {
                        if (ctx.LoopVars.TryGetValue("_length", out var len)) return len;
                    }
                    // Loop counters declared via :counter are readable by bare name.
                    var counterKey = path.StartsWith("@", StringComparison.Ordinal) ? path.Substring(1) : path;
                    if (ctx.LoopVars.TryGetValue(counterKey, out var counterVal)) return counterVal;
                    if (TryResolveAlias(path, ctx, out var aliasVal)) return aliasVal;
                    return ResolvePathWithOutput(currentSource, currentOutput, ctx.GlobalOutput, path, ctx.Constants, ctx.Accumulators);
                }

                case LiteralExpression lit:
                {
                    var litVal = OdinValueToDyn(lit.Value);
                    // Interpolate ${...} markers embedded in literal strings.
                    if (litVal.Type == DynValueType.String)
                    {
                        var s = litVal.AsString() ?? "";
                        if (s.Contains("${"))
                            return InterpolateString(s, ctx, currentSource, currentOutput);
                    }
                    return litVal;
                }

                case TransformExpression txExpr:
                    return ExecuteVerbCall(txExpr.Call, ctx, currentSource, currentOutput);

                case ObjectExpression objExpr:
                {
                    var obj = DynValue.Object(new List<KeyValuePair<string, DynValue>>());
                    foreach (var m in objExpr.Fields)
                    {
                        var val = EvaluateExpression(m.Expression, ctx, currentSource, currentOutput);
                        SetPath(ref obj, m.Target, val);
                    }
                    return obj;
                }

                default:
                    return DynValue.Null();
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // String interpolation
        // ─────────────────────────────────────────────────────────────────────

        private const int MaxInterpolations = 640;

        /// <summary>
        /// Interpolate ${...} markers in a template string. Resolves ${@path} and
        /// ${@.path} via path lookup and ${%verb args} via verb evaluation. A
        /// backslash-escaped marker (\${...}) is emitted literally as ${...}.
        /// </summary>
        private static DynValue InterpolateString(string template, ExecContext ctx, DynValue currentSource, DynValue currentOutput)
        {
            var sb = new System.Text.StringBuilder(template.Length);
            int i = 0;
            int count = 0;

            while (i < template.Length)
            {
                bool escaped = template[i] == '\\' && i + 1 < template.Length && template[i + 1] == '$';
                int dollar = escaped ? i + 1 : i;

                if (template[dollar] == '$' && dollar + 1 < template.Length && template[dollar + 1] == '{')
                {
                    int close = template.IndexOf('}', dollar + 2);
                    // Require at least one char between braces, matching ${(.+?)}.
                    if (close > dollar + 2)
                    {
                        if (++count > MaxInterpolations)
                        {
                            sb.Append(template, i, template.Length - i);
                            break;
                        }

                        string expr = template.Substring(dollar + 2, close - (dollar + 2));

                        if (escaped)
                        {
                            sb.Append("${").Append(expr).Append('}');
                        }
                        else
                        {
                            sb.Append(EvaluateInterpolationExpr(expr.Trim(), ctx, currentSource, currentOutput,
                                template.Substring(dollar, close - dollar + 1)));
                        }

                        i = close + 1;
                        continue;
                    }
                }

                sb.Append(template[i]);
                i++;
            }

            return DynValue.String(sb.ToString());
        }

        private static string EvaluateInterpolationExpr(
            string expr, ExecContext ctx, DynValue currentSource, DynValue currentOutput, string original)
        {
            if (expr.StartsWith("%", StringComparison.Ordinal))
            {
                var verbExpr = TransformParser.ParseInlineVerbExpression(expr);
                var val = EvaluateExpression(verbExpr, ctx, currentSource, currentOutput);
                return InterpolatedValueToString(val);
            }

            if (expr.StartsWith("@", StringComparison.Ordinal))
            {
                if (TryResolveAlias(expr.Substring(1), ctx, out var aliasVal))
                    return InterpolatedValueToString(aliasVal);
                var val = ResolvePathWithOutput(currentSource, currentOutput, ctx.GlobalOutput,
                    expr.Substring(1), ctx.Constants, ctx.Accumulators);
                return InterpolatedValueToString(val);
            }

            // Unknown marker: leave untouched.
            return original;
        }

        private static string InterpolatedValueToString(DynValue val)
        {
            switch (val.Type)
            {
                case DynValueType.Null: return "";
                case DynValueType.String: return val.AsString() ?? "";
                case DynValueType.Bool: return val.AsBool() == true ? "true" : "false";
                case DynValueType.Integer: return val.AsInt64()?.ToString(CultureInfo.InvariantCulture) ?? "";
                case DynValueType.Float: return val.AsDouble()?.ToString(CultureInfo.InvariantCulture) ?? "";
                case DynValueType.FloatRaw:
                case DynValueType.CurrencyRaw:
                    return val.AsString() ?? "";
                case DynValueType.Currency:
                case DynValueType.Percent:
                    return val.AsDouble()?.ToString(CultureInfo.InvariantCulture) ?? "";
                case DynValueType.Date:
                case DynValueType.Timestamp:
                case DynValueType.Time:
                case DynValueType.Duration:
                    return val.AsString() ?? "";
                case DynValueType.Reference: return "@" + (val.AsString() ?? "");
                default: return CoerceToString(val);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Verb execution
        // ─────────────────────────────────────────────────────────────────────

        private static DynValue ExecuteVerbCall(VerbCall call, ExecContext ctx, DynValue currentSource, DynValue currentOutput)
        {
            // Short-circuit: ifElse
            if (call.Verb == "ifElse" && call.Args.Count >= 3)
            {
                var condition = EvaluateVerbArg(call.Args[0], ctx, currentSource, currentOutput);
                bool isTrue = IsTruthy(condition);
                return isTrue
                    ? EvaluateVerbArg(call.Args[1], ctx, currentSource, currentOutput)
                    : EvaluateVerbArg(call.Args[2], ctx, currentSource, currentOutput);
            }

            // Short-circuit: cond
            if (call.Verb == "cond" && call.Args.Count >= 2)
            {
                int i = 0;
                while (i + 1 < call.Args.Count)
                {
                    var condition = EvaluateVerbArg(call.Args[i], ctx, currentSource, currentOutput);
                    if (IsTruthy(condition))
                        return EvaluateVerbArg(call.Args[i + 1], ctx, currentSource, currentOutput);
                    i += 2;
                }
                if (call.Args.Count % 2 == 1)
                    return EvaluateVerbArg(call.Args[call.Args.Count - 1], ctx, currentSource, currentOutput);
                return DynValue.Null();
            }

            // Control-flow verbs evaluate the condition first and only the selected
            // branch, so unselected branches do not fire side effects and and/or/
            // coalesce short-circuit. Strict-types mode evaluates eagerly to validate
            // all argument types.
            if (!call.IsCustom && !ctx.StrictTypes
                && TryEvaluateLazyVerb(call, ctx, currentSource, currentOutput, out var lazyResult))
                return lazyResult;

            // Standard eager evaluation
            var evaluatedArgs = new DynValue[call.Args.Count];
            for (int i = 0; i < call.Args.Count; i++)
                evaluatedArgs[i] = EvaluateVerbArg(call.Args[i], ctx, currentSource, currentOutput);

            // T002: strict argument type checking.
            if (ctx.StrictTypes && !call.IsCustom)
            {
                var typeError = ValidateVerbArgTypes(call.Verb, evaluatedArgs);
                if (typeError != null)
                    throw new CodedTransformException(new TransformError
                    {
                        Code = TransformErrorCode.InvalidVerbArgs.Code(),
                        Message = "Type error in %" + call.Verb + ": " + typeError,
                    });
            }

            // Look up verb
            if (!ctx.Verbs.TryGetValue(call.Verb, out var verbFn))
            {
                if (call.IsCustom)
                    return evaluatedArgs.Length > 0 ? evaluatedArgs[0] : DynValue.Null();
                // T001: unknown built-in verb.
                throw new CodedTransformException(new TransformError
                {
                    Code = TransformErrorCode.UnknownVerb.Code(),
                    Message = "Unknown verb: " + call.Verb,
                });
            }

            var verbCtx = new VerbContext
            {
                Source = currentSource,
                LoopVars = new Dictionary<string, DynValue>(ctx.LoopVars),
                Accumulators = new Dictionary<string, DynValue>(ctx.Accumulators),
                Tables = ctx.Tables,
                GlobalOutput = ctx.GlobalOutput,
                OnMissing = OnMissingPolicy(ctx),
                SequenceCounters = ctx.SequenceCounters,
            };

            var result = verbFn(evaluatedArgs, verbCtx);

            // Merge verb-level errors (T011, etc.) and warnings into engine results
            if (verbCtx.Errors.Count > 0)
                ctx.Errors.AddRange(verbCtx.Errors);
            if (verbCtx.Warnings.Count > 0)
                ctx.Warnings.AddRange(verbCtx.Warnings);

            // accumulate / set: update context accumulators
            if ((call.Verb == "accumulate" || call.Verb == "set") && evaluatedArgs.Length > 0)
            {
                var nameStr = evaluatedArgs[0].AsString();
                if (nameStr != null)
                    ctx.Accumulators[nameStr] = result;
            }

            return result;
        }

        // Boolean coercion used by short-circuiting control-flow verbs: strings are
        // truthy only for true/yes/y/1; numbers for non-zero; collections for non-empty.
        private static bool ToBooleanLogic(DynValue v)
        {
            switch (v.Type)
            {
                case DynValueType.Null: return false;
                case DynValueType.Bool: return v.AsBool()!.Value;
                case DynValueType.Integer: return v.AsInt64()!.Value != 0;
                case DynValueType.Float:
                case DynValueType.Currency:
                case DynValueType.Percent:
                    return v.AsDouble() != 0.0;
                case DynValueType.FloatRaw:
                case DynValueType.CurrencyRaw:
                {
                    var d = v.AsDouble();
                    return d.HasValue && d.Value != 0.0;
                }
                case DynValueType.String:
                {
                    var s = (v.AsString() ?? "").ToLowerInvariant();
                    return s == "true" || s == "yes" || s == "y" || s == "1";
                }
                case DynValueType.Date:
                case DynValueType.Timestamp:
                    return true;
                case DynValueType.Time:
                case DynValueType.Duration:
                case DynValueType.Reference:
                    return !string.IsNullOrEmpty(v.AsString());
                case DynValueType.Binary:
                    return !string.IsNullOrEmpty(v.AsString());
                case DynValueType.Array:
                    return (v.AsArray()?.Count ?? 0) > 0;
                case DynValueType.Object:
                    return (v.AsObject()?.Count ?? 0) > 0;
                default:
                    return false;
            }
        }

        // Evaluate a control-flow verb lazily, evaluating only the arguments needed to
        // decide the result. Returns false to defer to eager evaluation (too few args).
        private static bool TryEvaluateLazyVerb(VerbCall call, ExecContext ctx,
            DynValue currentSource, DynValue currentOutput, out DynValue result)
        {
            var a = call.Args;
            DynValue Ev(int i) => EvaluateVerbArg(a[i], ctx, currentSource, currentOutput);
            result = DynValue.Null();

            switch (call.Verb)
            {
                case "ifNull":
                {
                    if (a.Count < 2) return false;
                    var v0 = Ev(0);
                    result = v0.IsNull ? Ev(1) : v0;
                    return true;
                }
                case "ifEmpty":
                {
                    if (a.Count < 2) return false;
                    var v0 = Ev(0);
                    bool empty = v0.Type == DynValueType.String && v0.AsString() == "";
                    result = empty ? Ev(1) : v0;
                    return true;
                }
                case "coalesce":
                {
                    for (int i = 0; i < a.Count; i++)
                    {
                        var v = Ev(i);
                        if (!v.IsNull) { result = v; return true; }
                    }
                    result = DynValue.Null();
                    return true;
                }
                case "and":
                {
                    if (a.Count < 2) return false;
                    if (!ToBooleanLogic(Ev(0))) { result = DynValue.Bool(false); return true; }
                    result = DynValue.Bool(ToBooleanLogic(Ev(1)));
                    return true;
                }
                case "or":
                {
                    if (a.Count < 2) return false;
                    if (ToBooleanLogic(Ev(0))) { result = DynValue.Bool(true); return true; }
                    result = DynValue.Bool(ToBooleanLogic(Ev(1)));
                    return true;
                }
                case "switch":
                {
                    if (a.Count < 2) return false;
                    var subject = CoerceToString(Ev(0));
                    for (int i = 1; i < a.Count - 1; i += 2)
                    {
                        if (subject == CoerceToString(Ev(i)))
                        {
                            result = Ev(i + 1);
                            return true;
                        }
                    }
                    result = (a.Count - 1) % 2 == 1 ? Ev(a.Count - 1) : DynValue.Null();
                    return true;
                }
                default:
                    return false;
            }
        }

        // Modifiers that only apply to specific output formats. Using them with any
        // other format produces a T007 warning.
        private static readonly Dictionary<string, string[]> FormatSpecificModifiers = new()
        {
            ["pos"] = new[] { "fixed-width", "fwf" },
            ["len"] = new[] { "fixed-width", "fwf" },
            ["leftPad"] = new[] { "fixed-width", "fwf" },
            ["rightPad"] = new[] { "fixed-width", "fwf" },
            ["truncate"] = new[] { "fixed-width", "fwf" },
            ["element"] = new[] { "xml" },
            ["attr"] = new[] { "xml" },
            ["ns"] = new[] { "xml" },
            ["cdata"] = new[] { "xml" },
            ["omitEmpty"] = new[] { "xml", "json" },
            ["raw"] = new[] { "json" },
        };

        private static bool IsModifierCompatible(string modifier, string format)
        {
            if (!FormatSpecificModifiers.TryGetValue(modifier, out var allowed)) return true;
            return Array.IndexOf(allowed, format) >= 0;
        }

        // Expected argument types per verb for strict type checking. Verbs absent
        // from the table accept any argument types.
        private static readonly Dictionary<string, string[]> VerbArgTypes = new()
        {
            ["abs"] = new[] { "number" }, ["round"] = new[] { "number", "integer" },
            ["floor"] = new[] { "number" }, ["ceil"] = new[] { "number" },
            ["trunc"] = new[] { "number" }, ["sign"] = new[] { "number" },
            ["negate"] = new[] { "number" }, ["add"] = new[] { "number", "number" },
            ["subtract"] = new[] { "number", "number" }, ["multiply"] = new[] { "number", "number" },
            ["divide"] = new[] { "number", "number" }, ["mod"] = new[] { "number", "number" },
            ["pow"] = new[] { "number", "number" }, ["sqrt"] = new[] { "number" },
            ["log"] = new[] { "number", "number" }, ["ln"] = new[] { "number" },
            ["log10"] = new[] { "number" }, ["exp"] = new[] { "number" },
            ["clamp"] = new[] { "number", "number", "number" },
            ["isFinite"] = new[] { "number" }, ["isNaN"] = new[] { "number" },
            ["toRadians"] = new[] { "number" }, ["toDegrees"] = new[] { "number" },
        };

        // Returns a description of the first argument type mismatch, or null when valid.
        private static string? ValidateVerbArgTypes(string verb, DynValue[] args)
        {
            if (!VerbArgTypes.TryGetValue(verb, out var expected)) return null;
            for (int i = 0; i < args.Length && i < expected.Length; i++)
            {
                string actual = DynTypeName(args[i]);
                if (!StrictTypeMatches(actual, expected[i]))
                    return "Arg " + (i + 1) + ": expected " + expected[i] + ", got " + actual;
            }
            return null;
        }

        private static string DynTypeName(DynValue v) => v.Type switch
        {
            DynValueType.Null => "null",
            DynValueType.Bool => "boolean",
            DynValueType.Integer => "integer",
            DynValueType.Float or DynValueType.FloatRaw => "number",
            DynValueType.Currency or DynValueType.CurrencyRaw => "currency",
            DynValueType.Percent => "percent",
            DynValueType.String => "string",
            DynValueType.Array => "array",
            DynValueType.Object => "object",
            DynValueType.Date => "date",
            DynValueType.Timestamp => "timestamp",
            DynValueType.Time => "time",
            DynValueType.Duration => "duration",
            DynValueType.Reference => "reference",
            DynValueType.Binary => "binary",
            _ => "any",
        };

        private static bool StrictTypeMatches(string actual, string expected)
        {
            if (expected == "any") return true;
            if (actual == "null") return true;
            if (expected == "number")
                return actual == "number" || actual == "integer" || actual == "currency";
            return actual == expected;
        }

        private static DynValue EvaluateVerbArg(VerbArg arg, ExecContext ctx, DynValue currentSource, DynValue currentOutput)
        {
            switch (arg)
            {
                case ReferenceArg refArg:
                {
                    var path = refArg.Path;
                    DynValue val;

                    if (path.StartsWith("_item", StringComparison.Ordinal) || path.StartsWith("@_item", StringComparison.Ordinal))
                    {
                        var clean = path.StartsWith("@", StringComparison.Ordinal) ? path.Substring(1) : path;
                        if (ctx.LoopVars.TryGetValue("_item", out var item))
                        {
                            if (clean == "_item") val = item;
                            else
                            {
                                var remaining = clean.StartsWith("_item.", StringComparison.Ordinal) ? clean.Substring(6) : "";
                                val = string.IsNullOrEmpty(remaining) ? item : ResolveSubPath(item, remaining);
                            }
                        }
                        else
                        {
                            val = ResolvePathWithOutput(currentSource, currentOutput, ctx.GlobalOutput, path, ctx.Constants, ctx.Accumulators);
                        }
                    }
                    else if (path.StartsWith("_index", StringComparison.Ordinal) || path.StartsWith("@_index", StringComparison.Ordinal))
                    {
                        val = ctx.LoopVars.TryGetValue("_index", out var idx)
                            ? idx
                            : ResolvePathWithOutput(currentSource, currentOutput, ctx.GlobalOutput, path, ctx.Constants, ctx.Accumulators);
                    }
                    else
                    {
                        val = ResolvePathWithOutput(currentSource, currentOutput, ctx.GlobalOutput, path, ctx.Constants, ctx.Accumulators);
                    }

                    // Apply extraction directives
                    if (refArg.Directives.Count > 0)
                        val = ApplyDirectivesForSource(val, refArg.Directives, ctx.SourceFormat);

                    return val;
                }

                case LiteralArg litArg:
                    return OdinValueToDyn(litArg.Value);

                case VerbCallArg vcArg:
                    return ExecuteVerbCall(vcArg.NestedCall, ctx, currentSource, currentOutput);

                default:
                    return DynValue.Null();
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Path resolution
        // ─────────────────────────────────────────────────────────────────────

        private static DynValue ResolvePathWithOutput(
            DynValue source, DynValue output, DynValue globalOutput,
            string path, Dictionary<string, DynValue> constants, Dictionary<string, DynValue> accumulators)
        {
            path = path.Trim();
            if (string.IsNullOrEmpty(path) || path == "@") return source;

            // Constants and accumulators always resolve from their maps
            if (path.StartsWith("$const.", StringComparison.Ordinal) || path.StartsWith("$constants.", StringComparison.Ordinal)
                || path.StartsWith("$accumulator.", StringComparison.Ordinal) || path.StartsWith("$accumulators.", StringComparison.Ordinal))
            {
                return ResolvePath(source, path, constants, accumulators);
            }

            // Leading . always resolves against source
            var clean = path.StartsWith("@", StringComparison.Ordinal) ? path.Substring(1) : path;
            if (clean.StartsWith(".", StringComparison.Ordinal) || clean.Length == 0)
                return ResolvePath(source, path, constants, accumulators);

            // Bare paths: try local output first
            var fromOutput = ResolvePath(output, path, constants, accumulators);
            if (fromOutput.Type != DynValueType.Null) return fromOutput;

            // Try global output
            var fromGlobal = ResolvePath(globalOutput, path, constants, accumulators);
            if (fromGlobal.Type != DynValueType.Null) return fromGlobal;

            // Fall back to source
            return ResolvePath(source, path, constants, accumulators);
        }

        // Whether a mapping copies a source path that is absent (the key is not
        // present) — distinct from a path present with a null value. Only plain copy
        // expressions qualify; verbs, literals, objects, and special paths never do.
        private static bool IsCopySourceAbsent(FieldMapping mapping, ExecContext ctx, DynValue currentSource, MappingMods mods)
        {
            if (mapping.Expression is not CopyExpression copy) return false;
            // A :default or :object supplies its own value; not a missing-source error.
            if (mods.HasDefaultOrObject) return false;

            var path = copy.Path.Trim();
            if (path.StartsWith("@", StringComparison.Ordinal)) path = path.Substring(1);
            if (path.Length == 0 || path.StartsWith("$", StringComparison.Ordinal)) return false;

            // Loop variables and counters are not source paths.
            if (path == "_index" || path == "_item" || path == "_length") return false;
            var counterKey = path;
            if (ctx.LoopVars.ContainsKey(counterKey)) return false;

            DynValue target;
            string subPath;
            if (path.StartsWith(".", StringComparison.Ordinal))
            {
                target = currentSource;
                subPath = path.Substring(1);
            }
            else
            {
                var firstPart = path.Split('.')[0];
                if (ctx.Aliases.ContainsKey(firstPart))
                {
                    target = ctx.Aliases[firstPart];
                    subPath = path.Contains('.') ? path.Substring(firstPart.Length + 1) : "";
                }
                else
                {
                    target = ctx.Source;
                    subPath = path;
                }
            }

            return subPath.Length == 0 ? false : !PathIsPresent(target, subPath);
        }

        // Walk a dotted/indexed path; returns false only when a key/index along the
        // way is absent. A present node holding null returns true.
        private static bool PathIsPresent(DynValue value, string path)
        {
            var segments = ParsePathSegments(path);
            var current = value;
            foreach (var seg in segments)
            {
                if (seg.IsIndex)
                {
                    if (!string.IsNullOrEmpty(seg.Name))
                    {
                        var fieldVal = current.Get(seg.Name);
                        if (fieldVal == null) return false;
                        current = fieldVal;
                    }
                    var indexed = current.GetIndex(seg.Index);
                    if (indexed == null) return false;
                    current = indexed;
                }
                else
                {
                    var next = current.Get(seg.Name);
                    if (next == null) return false;
                    current = next;
                }
            }
            return true;
        }

        internal static DynValue ResolvePath(
            DynValue source, string path,
            Dictionary<string, DynValue> constants, Dictionary<string, DynValue> accumulators)
        {
            path = path.Trim();

            // Constants
            if (path.StartsWith("$const.", StringComparison.Ordinal))
            {
                var rest = path.Substring("$const.".Length);
                return constants.TryGetValue(rest, out var v) ? v : DynValue.Null();
            }
            if (path.StartsWith("$constants.", StringComparison.Ordinal))
            {
                var rest = path.Substring("$constants.".Length);
                return constants.TryGetValue(rest, out var v) ? v : DynValue.Null();
            }

            // Accumulators
            if (path.StartsWith("$accumulator.", StringComparison.Ordinal))
            {
                var rest = path.Substring("$accumulator.".Length);
                return accumulators.TryGetValue(rest, out var v) ? v : DynValue.Null();
            }
            if (path.StartsWith("$accumulators.", StringComparison.Ordinal))
            {
                var rest = path.Substring("$accumulators.".Length);
                return accumulators.TryGetValue(rest, out var v) ? v : DynValue.Null();
            }

            // Strip @ and leading dot
            var clean = path.StartsWith("@", StringComparison.Ordinal) ? path.Substring(1) : path;
            clean = clean.StartsWith(".", StringComparison.Ordinal) ? clean.Substring(1) : clean;
            if (clean.Length == 0) return source;

            return ResolveSubPath(source, clean);
        }

        // Drive one or more :loop directives as a nested cross-product. Each level binds
        // its alias and current item, then recurses into the next loop; the innermost
        // level emits one result element per item. Relative loop paths (.field) resolve
        // against the current outer item; a non-array source at any level yields no rows.
        private static void IterateLoops(
            List<SegmentDirective> loops,
            int depth,
            ExecContext ctx,
            TransformSegment segment,
            DynValue current,
            string currentPrefix,
            List<DynValue> results,
            Action<DynValue>? onItem)
        {
            var loop = loops[depth];
            bool isOutermost = depth == 0;
            bool isInnermost = depth == loops.Count - 1;

            string loopPath = loop.Value ?? "";
            if (loopPath.StartsWith("@", StringComparison.Ordinal)) loopPath = loopPath.Substring(1);

            DynValue itemsVal;
            if (loopPath.StartsWith(".", StringComparison.Ordinal))
            {
                itemsVal = ResolveSubPath(current, loopPath.Substring(1));
            }
            else if (isOutermost)
            {
                itemsVal = ResolvePath(ctx.Source, loopPath, ctx.Constants, ctx.Accumulators);
            }
            else if (TryResolveAlias(loopPath, ctx, out var aliased))
            {
                itemsVal = aliased;
            }
            else
            {
                itemsVal = ResolveSubPath(current, loopPath);
            }

            if (itemsVal.Type != DynValueType.Array)
            {
                // A present non-array scalar is a T009 error; an absent/null source
                // yields zero rows silently.
                if (itemsVal.Type != DynValueType.Null)
                    throw new CodedTransformException(new TransformError
                    {
                        Code = TransformErrorCode.LoopSourceNotArray.Code(),
                        Message = "Loop source path '" + loopPath + "' does not resolve to an array",
                        Path = segment.Name,
                    });
                return;
            }
            var items = itemsVal.AsArray();
            if (items == null) return;

            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                ctx.LoopVars["_item"] = item;
                ctx.LoopVars["_index"] = DynValue.Integer(i);
                ctx.LoopVars["_length"] = DynValue.Integer(items.Count);
                if (loop.Alias != null) ctx.Aliases[loop.Alias] = item;
                if (segment.Counter != null && isInnermost)
                {
                    ctx.Accumulators[segment.Counter] = DynValue.Integer(i);
                    ctx.LoopVars[segment.Counter] = DynValue.Integer(i);
                }

                if (!isInnermost)
                {
                    IterateLoops(loops, depth + 1, ctx, segment, item, currentPrefix, results, onItem);
                }
                else if (onItem != null)
                {
                    onItem(item);
                }
                else
                {
                    var itemOutput = DynValue.Object(new List<KeyValuePair<string, DynValue>>());
                    foreach (var mapping in segment.Mappings)
                        ProcessMapping(mapping, ctx, item, ref itemOutput, currentPrefix);
                    results.Add(itemOutput);
                }

                if (loop.Alias != null) ctx.Aliases.Remove(loop.Alias);
            }

            ctx.LoopVars.Remove("_item");
            ctx.LoopVars.Remove("_index");
            ctx.LoopVars.Remove("_length");
        }

        // Render a :literal segment to interpolated text lines. The """ body's outer
        // delimiter newline is stripped from each end; under :loop the block renders
        // once per item. Lines are emitted verbatim by the fixed-width formatter.
        private static void ProcessLiteralSegment(
            TransformSegment segment, ExecContext ctx, ref DynValue output, string cleanName, bool isRoot)
        {
            string template = NormalizeLiteralBody(segment.LiteralBody ?? "");
            var lines = new List<DynValue>();

            void Render(DynValue current)
            {
                string rendered = InterpolateLiteralBlock(template, ctx, current, ctx.GlobalOutput, segment.Path);
                foreach (var line in rendered.Split('\n'))
                    lines.Add(DynValue.String(line));
            }

            if (segment.Loops.Count >= 1 && segment.IsArray)
            {
                // A non-array loop source raises a coded error honoring onError.
                try
                {
                    IterateLoops(segment.Loops, 0, ctx, segment, ctx.Source, cleanName, new List<DynValue>(), Render);
                }
                catch (CodedTransformException ex)
                {
                    EmitLoopError(ctx, ex.Error);
                    return;
                }
            }
            else
            {
                Render(ctx.Source);
            }

            var holder = DynValue.Object(new List<KeyValuePair<string, DynValue>>
            {
                new KeyValuePair<string, DynValue>("__literalLines", DynValue.Array(lines)),
            });
            if (isRoot) output = holder;
            else SetPath(ref output, cleanName, holder);
        }

        // Strip one leading and one trailing newline so the """ delimiters, written on
        // their own lines, do not contribute blank output lines.
        private static string NormalizeLiteralBody(string body)
        {
            var s = body;
            if (s.StartsWith("\r\n", StringComparison.Ordinal)) s = s.Substring(2);
            else if (s.StartsWith("\n", StringComparison.Ordinal)) s = s.Substring(1);
            if (s.EndsWith("\r\n", StringComparison.Ordinal)) s = s.Substring(0, s.Length - 2);
            else if (s.EndsWith("\n", StringComparison.Ordinal)) s = s.Substring(0, s.Length - 1);
            return s;
        }

        // Interpolate a :literal block body. Differs from InterpolateString in escapes
        // and nesting: \${ -> ${, \$ -> $, \\ -> \; a ${...} whose expression itself
        // contains ${ raises T014 (nested interpolation).
        private static string InterpolateLiteralBlock(
            string template, ExecContext ctx, DynValue currentSource, DynValue currentOutput, string segmentPath)
        {
            var sb = new System.Text.StringBuilder(template.Length);
            int i = 0;
            int len = template.Length;

            while (i < len)
            {
                char ch = template[i];

                if (ch == '\\')
                {
                    char next = i + 1 < len ? template[i + 1] : '\0';
                    if (next == '$' && i + 2 < len && template[i + 2] == '{') { sb.Append("${"); i += 3; continue; }
                    if (next == '\\') { sb.Append('\\'); i += 2; continue; }
                    if (next == '$') { sb.Append('$'); i += 2; continue; }
                    sb.Append('\\'); i += 1; continue;
                }

                if (ch == '$' && i + 1 < len && template[i + 1] == '{')
                {
                    int close = template.IndexOf('}', i + 2);
                    if (close == -1)
                    {
                        sb.Append(template, i, len - i);
                        break;
                    }
                    string expr = template.Substring(i + 2, close - (i + 2));
                    if (expr.Contains("${"))
                    {
                        ctx.Errors.Add(new TransformError
                        {
                            Code = "T014",
                            Message = $"Nested interpolation is not allowed: ${{{expr}}}",
                            Path = segmentPath,
                        });
                        return "";
                    }
                    sb.Append(EvaluateInterpolationExpr(expr.Trim(), ctx, currentSource, currentOutput,
                        template.Substring(i, close - i + 1)));
                    i = close + 1;
                    continue;
                }

                sb.Append(ch);
                i++;
            }

            return sb.ToString();
        }

        // Resolve a path whose first segment names a loop alias. Returns false when
        // the path is relative (leading dot) or its head is not a bound alias.
        private static bool TryResolveAlias(string path, ExecContext ctx, out DynValue result)
        {
            result = DynValue.Null();
            if (ctx.Aliases.Count == 0) return false;

            var clean = path.StartsWith("@", StringComparison.Ordinal) ? path.Substring(1) : path;
            if (clean.Length == 0 || clean[0] == '.') return false;

            int dot = clean.IndexOf('.');
            int bracket = clean.IndexOf('[');
            int end = clean.Length;
            if (dot >= 0) end = Math.Min(end, dot);
            if (bracket >= 0) end = Math.Min(end, bracket);
            var head = clean.Substring(0, end);

            if (!ctx.Aliases.TryGetValue(head, out var aliased)) return false;

            var rest = clean.Substring(end);
            if (rest.StartsWith(".", StringComparison.Ordinal)) rest = rest.Substring(1);
            result = rest.Length == 0 ? aliased : ResolveSubPath(aliased, rest);
            return true;
        }

        private static DynValue ResolveSubPath(DynValue value, string path)
        {
            if (string.IsNullOrEmpty(path)) return value;

            var segments = ParsePathSegments(path);
            var current = value;

            foreach (var seg in segments)
            {
                if (seg.IsIndex)
                {
                    if (!string.IsNullOrEmpty(seg.Name))
                    {
                        var fieldVal = current.Get(seg.Name);
                        if (fieldVal == null) return DynValue.Null();
                        current = fieldVal;
                    }
                    var indexed = current.GetIndex(seg.Index);
                    if (indexed == null) return DynValue.Null();
                    current = indexed;
                }
                else
                {
                    var next = current.Get(seg.Name);
                    if (next == null) return DynValue.Null();
                    current = next;
                }
            }

            return current;
        }

        private struct PathSeg
        {
            public string Name;
            public int Index;
            public bool IsIndex;
        }

        private static List<PathSeg> ParsePathSegments(string path)
        {
            var segments = new List<PathSeg>();
            int pos = 0;

            while (pos < path.Length)
            {
                if (path[pos] == '.') { pos++; continue; }
                if (pos >= path.Length) break;

                // Bare index [N]
                if (path[pos] == '[')
                {
                    int bracketEnd = path.IndexOf(']', pos);
                    if (bracketEnd > pos)
                    {
                        var idxStr = path.Substring(pos + 1, bracketEnd - pos - 1);
                        if (int.TryParse(idxStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var idx))
                            segments.Add(new PathSeg { Name = "", Index = idx, IsIndex = true });
                        pos = bracketEnd + 1;
                        continue;
                    }
                }

                // Find end of segment
                int end = path.Length;
                int bracketPos = -1;
                for (int i = pos; i < path.Length; i++)
                {
                    if (path[i] == '.') { end = i; break; }
                    if (path[i] == '[' && bracketPos < 0) bracketPos = i;
                    if (path[i] == ']' && bracketPos >= 0) { end = i + 1; break; }
                }

                var segStr = path.Substring(pos, end - pos);
                pos = end;

                // Check for array index in segment
                int bStart = segStr.IndexOf('[');
                int bEnd = segStr.IndexOf(']');
                if (bStart >= 0 && bEnd > bStart)
                {
                    var fieldName = segStr.Substring(0, bStart);
                    var indexStr = segStr.Substring(bStart + 1, bEnd - bStart - 1);
                    if (int.TryParse(indexStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var idx))
                    {
                        segments.Add(new PathSeg { Name = fieldName, Index = idx, IsIndex = true });
                        continue;
                    }
                }

                segments.Add(new PathSeg { Name = segStr, Index = 0, IsIndex = false });
            }

            return segments;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Setting values in output
        // ─────────────────────────────────────────────────────────────────────

        private enum SetPathPartType { Field, ArrayIndex, ArrayPush }

        private struct SetPathPart
        {
            public SetPathPartType Type;
            public string Name;
            public int Index;
        }

        internal static void SetPath(ref DynValue output, string path, DynValue value)
        {
            var parts = SplitSetPath(path);
            if (parts.Count == 0) { output = value; return; }
            if (parts.Count == 1) { SetSingleField(ref output, parts[0], value); return; }

            // Navigate to parent, creating intermediates. A following index part
            // means the current slot must be an array rather than an object.
            var current = output;
            for (int i = 0; i < parts.Count - 1; i++)
            {
                bool nextIsIndex = parts[i + 1].Type == SetPathPartType.ArrayIndex
                    && string.IsNullOrEmpty(parts[i + 1].Name);
                current = EnsureAndDescend(ref current, parts[i], nextIsIndex);
            }

            SetSingleField(ref current, parts[parts.Count - 1], value);

            // Propagate changes back up
            // (DynValue objects use mutable lists so changes propagate automatically)
        }

        private static List<SetPathPart> SplitSetPath(string path)
        {
            var parts = new List<SetPathPart>();
            int pos = 0;

            while (pos < path.Length)
            {
                if (path[pos] == '.') { pos++; continue; }
                if (pos >= path.Length) break;

                // Find next dot (not inside brackets)
                int end = path.Length;
                int depth = 0;
                for (int i = pos; i < path.Length; i++)
                {
                    if (path[i] == '[') depth++;
                    else if (path[i] == ']') depth--;
                    else if (path[i] == '.' && depth == 0 && i > pos) { end = i; break; }
                }

                var seg = path.Substring(pos, end - pos);
                pos = end;

                if (seg.EndsWith("[]", StringComparison.Ordinal))
                {
                    parts.Add(new SetPathPart { Type = SetPathPartType.ArrayPush, Name = seg.Substring(0, seg.Length - 2) });
                }
                else
                {
                    int bStart = seg.IndexOf('[');
                    if (bStart >= 0 && AddIndexedParts(seg, bStart, parts))
                        continue;
                    parts.Add(new SetPathPart { Type = SetPathPartType.Field, Name = seg });
                }
            }

            return parts;
        }

        // Emit indexed parts for a segment of the form name[i][j]...; the first
        // bracket binds to the name, subsequent brackets are bare array indices.
        private static bool AddIndexedParts(string seg, int bStart, List<SetPathPart> parts)
        {
            string name = seg.Substring(0, bStart);
            var indices = new List<int>();
            int i = bStart;
            while (i < seg.Length && seg[i] == '[')
            {
                int bEnd = seg.IndexOf(']', i);
                if (bEnd < 0) return false;
                var idxStr = seg.Substring(i + 1, bEnd - i - 1);
                if (!int.TryParse(idxStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var idx))
                    return false;
                indices.Add(idx);
                i = bEnd + 1;
            }
            if (i != seg.Length || indices.Count == 0) return false;

            parts.Add(new SetPathPart { Type = SetPathPartType.ArrayIndex, Name = name, Index = indices[0] });
            for (int k = 1; k < indices.Count; k++)
                parts.Add(new SetPathPart { Type = SetPathPartType.ArrayIndex, Name = "", Index = indices[k] });
            return true;
        }

        private static void SetSingleField(ref DynValue obj, SetPathPart part, DynValue value)
        {
            // Bare [i] index assigns directly into the host array.
            if (part.Type == SetPathPartType.ArrayIndex && string.IsNullOrEmpty(part.Name))
            {
                var hostArr = obj.AsArray();
                if (hostArr != null)
                {
                    while (hostArr.Count <= part.Index) hostArr.Add(DynValue.Null());
                    hostArr[part.Index] = value;
                }
                return;
            }

            var entries = obj.AsObject();
            if (entries == null) return;

            switch (part.Type)
            {
                case SetPathPartType.Field:
                {
                    for (int i = 0; i < entries.Count; i++)
                    {
                        if (entries[i].Key == part.Name)
                        {
                            entries[i] = new KeyValuePair<string, DynValue>(part.Name, value);
                            return;
                        }
                    }
                    entries.Add(new KeyValuePair<string, DynValue>(part.Name, value));
                    break;
                }
                case SetPathPartType.ArrayIndex:
                {
                    if (!string.IsNullOrEmpty(part.Name))
                    {
                        int arrIdx = -1;
                        for (int i = 0; i < entries.Count; i++)
                            if (entries[i].Key == part.Name) { arrIdx = i; break; }

                        if (arrIdx >= 0)
                        {
                            var arr = entries[arrIdx].Value.AsArray();
                            if (arr != null)
                            {
                                while (arr.Count <= part.Index) arr.Add(DynValue.Null());
                                arr[part.Index] = value;
                            }
                        }
                        else
                        {
                            var items = new List<DynValue>();
                            while (items.Count <= part.Index) items.Add(DynValue.Null());
                            items[part.Index] = value;
                            entries.Add(new KeyValuePair<string, DynValue>(part.Name, DynValue.Array(items)));
                        }
                    }
                    else
                    {
                        var arr = obj.AsArray();
                        if (arr != null)
                        {
                            while (arr.Count <= part.Index) arr.Add(DynValue.Null());
                            arr[part.Index] = value;
                        }
                    }
                    break;
                }
                case SetPathPartType.ArrayPush:
                {
                    int arrIdx = -1;
                    for (int i = 0; i < entries.Count; i++)
                        if (entries[i].Key == part.Name) { arrIdx = i; break; }

                    if (arrIdx >= 0)
                    {
                        var arr = entries[arrIdx].Value.AsArray();
                        if (arr != null) arr.Add(value);
                    }
                    else
                    {
                        entries.Add(new KeyValuePair<string, DynValue>(part.Name, DynValue.Array(new List<DynValue> { value })));
                    }
                    break;
                }
            }
        }

        private static DynValue EnsureAndDescend(ref DynValue current, SetPathPart part, bool childIsIndex = false)
        {
            // A bare [i] index descends into the current array directly.
            if (part.Type == SetPathPartType.ArrayIndex && string.IsNullOrEmpty(part.Name))
            {
                var hostArr = current.AsArray();
                if (hostArr != null)
                {
                    while (hostArr.Count <= part.Index)
                        hostArr.Add(childIsIndex
                            ? DynValue.Array(new List<DynValue>())
                            : DynValue.Object(new List<KeyValuePair<string, DynValue>>()));
                    return hostArr[part.Index];
                }
                return current;
            }

            var entries = current.AsObject();
            if (entries == null) return current;

            switch (part.Type)
            {
                case SetPathPartType.Field:
                {
                    for (int i = 0; i < entries.Count; i++)
                    {
                        if (entries[i].Key == part.Name) return entries[i].Value;
                    }
                    var newObj = DynValue.Object(new List<KeyValuePair<string, DynValue>>());
                    entries.Add(new KeyValuePair<string, DynValue>(part.Name, newObj));
                    return newObj;
                }
                case SetPathPartType.ArrayIndex:
                {
                    int arrIdx = -1;
                    for (int i = 0; i < entries.Count; i++)
                        if (entries[i].Key == part.Name) { arrIdx = i; break; }

                    List<DynValue> arr;
                    if (arrIdx >= 0)
                    {
                        arr = entries[arrIdx].Value.AsArray() ?? new List<DynValue>();
                    }
                    else
                    {
                        arr = new List<DynValue>();
                        var arrVal = DynValue.Array(arr);
                        entries.Add(new KeyValuePair<string, DynValue>(part.Name, arrVal));
                    }

                    // The slot is an array when the next part indexes into it.
                    while (arr.Count <= part.Index)
                        arr.Add(childIsIndex
                            ? DynValue.Array(new List<DynValue>())
                            : DynValue.Object(new List<KeyValuePair<string, DynValue>>()));
                    return arr[part.Index];
                }
                case SetPathPartType.ArrayPush:
                {
                    int arrIdx = -1;
                    for (int i = 0; i < entries.Count; i++)
                        if (entries[i].Key == part.Name) { arrIdx = i; break; }

                    List<DynValue> arr;
                    if (arrIdx >= 0)
                    {
                        arr = entries[arrIdx].Value.AsArray() ?? new List<DynValue>();
                    }
                    else
                    {
                        arr = new List<DynValue>();
                        entries.Add(new KeyValuePair<string, DynValue>(part.Name, DynValue.Array(arr)));
                    }

                    var newEntry = DynValue.Object(new List<KeyValuePair<string, DynValue>>());
                    arr.Add(newEntry);
                    return newEntry;
                }
                default:
                    return current;
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Array/Object helpers
        // ─────────────────────────────────────────────────────────────────────

        private struct ArrayIndexResult
        {
            public string Name;
            public int Index;
        }

        private static ArrayIndexResult? ParseArrayIndex(string name)
        {
            int bStart = name.IndexOf('[');
            if (bStart < 0) return null;
            int bEnd = name.IndexOf(']', bStart);
            if (bEnd < 0) return null;
            var arrName = name.Substring(0, bStart);
            var idxStr = name.Substring(bStart + 1, bEnd - bStart - 1);
            if (int.TryParse(idxStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var idx))
                return new ArrayIndexResult { Name = arrName, Index = idx };
            return null;
        }

        private static void EnsureArrayEntryAt(ref DynValue output, string arrName, int idx)
        {
            var entries = output.AsObject();
            if (entries == null) return;

            int arrPos = -1;
            for (int i = 0; i < entries.Count; i++)
                if (entries[i].Key == arrName) { arrPos = i; break; }

            if (arrPos < 0)
            {
                entries.Add(new KeyValuePair<string, DynValue>(arrName, DynValue.Array(new List<DynValue>())));
                arrPos = entries.Count - 1;
            }

            var arr = entries[arrPos].Value.AsArray();
            if (arr != null)
            {
                while (arr.Count <= idx)
                    arr.Add(DynValue.Object(new List<KeyValuePair<string, DynValue>>()));
            }
        }

        private static DynValue? GetArrayEntryRef(ref DynValue output, string arrName, int idx)
        {
            var entries = output.AsObject();
            if (entries == null) return null;
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].Key == arrName)
                {
                    var arr = entries[i].Value.AsArray();
                    if (arr != null && idx < arr.Count) return arr[idx];
                }
            }
            return null;
        }

        private static void SetArrayEntry(ref DynValue output, string arrName, int idx, DynValue value)
        {
            var entries = output.AsObject();
            if (entries == null) return;
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].Key == arrName)
                {
                    var arr = entries[i].Value.AsArray();
                    if (arr != null && idx < arr.Count) arr[idx] = value;
                }
            }
        }

        private static void EnsureObjectAt(ref DynValue output, string key)
        {
            var entries = output.AsObject();
            if (entries == null) return;
            for (int i = 0; i < entries.Count; i++)
                if (entries[i].Key == key) return;
            entries.Add(new KeyValuePair<string, DynValue>(key, DynValue.Object(new List<KeyValuePair<string, DynValue>>())));
        }

        private static DynValue? GetMutPath(ref DynValue output, string key)
        {
            var entries = output.AsObject();
            if (entries == null) return null;
            for (int i = 0; i < entries.Count; i++)
                if (entries[i].Key == key) return entries[i].Value;
            return null;
        }

        private static void SetObjectField(ref DynValue output, string key, DynValue value)
        {
            var entries = output.AsObject();
            if (entries == null) return;
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].Key == key)
                {
                    entries[i] = new KeyValuePair<string, DynValue>(key, value);
                    return;
                }
            }
            entries.Add(new KeyValuePair<string, DynValue>(key, value));
        }

        // Path-aware versions that handle dotted segment names like "nested.person"
        private static void EnsureObjectAtPath(ref DynValue output, string path)
        {
            if (path.IndexOf('.') < 0) { EnsureObjectAt(ref output, path); return; }
            var parts = path.Split('.');
            var current = output;
            foreach (var part in parts)
            {
                var entries = current.AsObject();
                if (entries == null) return;
                int idx = -1;
                for (int i = 0; i < entries.Count; i++)
                {
                    if (entries[i].Key == part) { idx = i; break; }
                }
                if (idx < 0)
                {
                    var newObj = DynValue.Object(new List<KeyValuePair<string, DynValue>>());
                    entries.Add(new KeyValuePair<string, DynValue>(part, newObj));
                    current = newObj;
                }
                else
                {
                    if (entries[idx].Value.Type != DynValueType.Object)
                    {
                        var newObj = DynValue.Object(new List<KeyValuePair<string, DynValue>>());
                        entries[idx] = new KeyValuePair<string, DynValue>(part, newObj);
                        current = newObj;
                    }
                    else
                    {
                        current = entries[idx].Value;
                    }
                }
            }
        }

        private static DynValue? GetMutPathDeep(ref DynValue output, string path)
        {
            if (path.IndexOf('.') < 0) return GetMutPath(ref output, path);
            var parts = path.Split('.');
            var current = output;
            foreach (var part in parts)
            {
                var entries = current.AsObject();
                if (entries == null) return null;
                bool found = false;
                for (int i = 0; i < entries.Count; i++)
                {
                    if (entries[i].Key == part)
                    {
                        current = entries[i].Value;
                        found = true;
                        break;
                    }
                }
                if (!found) return null;
            }
            return current;
        }

        private static void SetObjectFieldDeep(ref DynValue output, string path, DynValue value)
        {
            if (path.IndexOf('.') < 0) { SetObjectField(ref output, path, value); return; }
            var dotIdx = path.LastIndexOf('.');
            var parentPath = path.Substring(0, dotIdx);
            var fieldName = path.Substring(dotIdx + 1);
            var parent = GetMutPathDeep(ref output, parentPath);
            if (parent != null)
                SetObjectField(ref parent, fieldName, value);
        }

        // ─────────────────────────────────────────────────────────────────────
        // OdinValue -> DynValue conversion
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Convert an <see cref="OdinValue"/> to a <see cref="DynValue"/>.
        /// </summary>
        public static DynValue OdinValueToDyn(OdinValue val)
        {
            switch (val)
            {
                case OdinNull _: return DynValue.Null();
                case OdinBoolean b: return DynValue.Bool(b.Value);
                case OdinString s: return DynValue.String(s.Value);
                case OdinInteger i:
                {
                    // An integer literal beyond Int64 range is preserved (and parsed)
                    // as a floating value rather than truncated to its overflowed Value.
                    if (i.Raw != null && !long.TryParse(i.Raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out _)
                        && double.TryParse(i.Raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var bigD))
                        return DynValue.Float(bigD);
                    return DynValue.Integer(i.Value);
                }
                case OdinNumber n: return DynValue.Float(n.Value);
                case OdinCurrency c: return DynValue.Currency(c.Value, c.DecimalPlaces, c.CurrencyCode);
                case OdinPercent p: return DynValue.Percent(p.Value);
                case OdinDate d: return DynValue.Date(d.Raw);
                case OdinTimestamp ts: return DynValue.Timestamp(ts.Raw);
                case OdinTime t: return DynValue.Time(t.Value);
                case OdinDuration d: return DynValue.Duration(d.Value);
                case OdinReference r: return DynValue.Reference(r.Path);
                case OdinBinary b: return DynValue.Binary(Convert.ToBase64String(b.Data));
                case OdinArray a:
                {
                    var items = new List<DynValue>();
                    foreach (var item in a.Items)
                    {
                        var v = item.AsValue();
                        if (v != null) items.Add(OdinValueToDyn(v));
                        else
                        {
                            var rec = item.AsRecord();
                            if (rec != null)
                            {
                                var entries = new List<KeyValuePair<string, DynValue>>();
                                foreach (var kvp in rec)
                                    entries.Add(new KeyValuePair<string, DynValue>(kvp.Key, OdinValueToDyn(kvp.Value)));
                                items.Add(DynValue.Object(entries));
                            }
                        }
                    }
                    return DynValue.Array(items);
                }
                case OdinObject o:
                {
                    var entries = new List<KeyValuePair<string, DynValue>>();
                    foreach (var kvp in o.Fields)
                        entries.Add(new KeyValuePair<string, DynValue>(kvp.Key, OdinValueToDyn(kvp.Value)));
                    return DynValue.Object(entries);
                }
                case OdinVerb _:
                    return DynValue.Null();
                default:
                    return DynValue.Null();
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Confidential enforcement
        // ─────────────────────────────────────────────────────────────────────

        private static void ApplyConfidentialEnforcement(
            List<TransformSegment> segments, ConfidentialMode mode, ref DynValue output)
        {
            var paths = new List<string>();
            CollectConfidentialPaths(segments, "", paths);

            foreach (var path in paths)
            {
                var val = ResolveMutPath(ref output, path);
                if (val != null)
                {
                    var replaced = ApplyConfidentialToValue(val, mode);
                    SetByDottedPath(ref output, path, replaced);
                }
            }
        }

        private static void CollectConfidentialPaths(List<TransformSegment> segments, string prefix, List<string> paths)
        {
            foreach (var seg in segments)
            {
                string segPrefix;
                if (string.IsNullOrEmpty(seg.Name) || seg.Name == "$" || seg.Name == "_root")
                    segPrefix = prefix;
                else if (string.IsNullOrEmpty(prefix))
                    segPrefix = seg.Name;
                else
                    segPrefix = prefix + "." + seg.Name;

                foreach (var mapping in seg.Mappings)
                {
                    if (mapping.Modifiers != null && mapping.Modifiers.Confidential)
                    {
                        var fullPath = string.IsNullOrEmpty(segPrefix) ? mapping.Target : segPrefix + "." + mapping.Target;
                        paths.Add(fullPath);
                    }
                }

                CollectConfidentialPaths(seg.Children, segPrefix, paths);
            }
        }

        private static DynValue ApplyConfidentialToValue(DynValue val, ConfidentialMode mode)
        {
            if (mode == ConfidentialMode.Redact) return DynValue.Null();
            // Mask
            var s = val.AsString();
            if (s != null) return DynValue.String(new string('*', s.Length));
            return DynValue.Null();
        }

        private static DynValue? ResolveMutPath(ref DynValue output, string path)
        {
            var parts = path.Split('.');
            var current = output;
            foreach (var part in parts)
            {
                var next = current.Get(part);
                if (next == null) return null;
                current = next;
            }
            return current;
        }

        private static void SetByDottedPath(ref DynValue output, string path, DynValue value)
        {
            SetPath(ref output, path, value);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Output formatting
        // ─────────────────────────────────────────────────────────────────────

        // T010: a fixed-width field whose pos+len extends past the declared lineWidth.
        private static void CheckFixedWidthPositionOverflow(
            List<TransformSegment> segments, TargetConfig config, Action<TransformWarning> onWarning)
        {
            if (!config.Options.TryGetValue("lineWidth", out var lwStr)
                || !int.TryParse(lwStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out int lineWidth))
                return;

            foreach (var seg in segments)
            {
                foreach (var mapping in seg.Mappings)
                {
                    int pos = -1, len = -1;
                    foreach (var dir in mapping.Directives)
                    {
                        if (dir.Name == "pos") pos = (int)(dir.Value?.AsNumber() ?? -1);
                        else if (dir.Name == "len") len = (int)(dir.Value?.AsNumber() ?? -1);
                    }
                    if (pos >= 0 && len > 0 && pos + len > lineWidth)
                        onWarning(new TransformWarning
                        {
                            Code = TransformErrorCode.PositionOverflow.Code(),
                            Message = "Field at position " + pos + " with length " + len
                                + " exceeds line width " + lineWidth,
                            Path = mapping.Target,
                        });
                }
            }
        }

        private static string FormatOutput(
            DynValue output, string targetFormat, Dictionary<string, string> options,
            List<TransformSegment> segments, Dictionary<string, OdinModifiers> modifiers,
            Dictionary<string, string>? namespaces = null, Action<TransformError>? onError = null,
            Action<TransformWarning>? onWarning = null)
        {
            // A registered custom formatter handles every format name.
            if (OutputFormatter != null)
                return OutputFormatter(output, targetFormat, options, modifiers);

            // Dispatch to built-in formatters
            var config = new TargetConfig
            {
                Format = targetFormat,
                Options = new Dictionary<string, string>(options),
                Namespaces = namespaces != null ? new Dictionary<string, string>(namespaces) : new Dictionary<string, string>(),
            };

            switch (targetFormat.ToLowerInvariant())
            {
                case "odin":
                    // Transform output never includes the {$} header
                    if (!config.Options.ContainsKey("includeHeader"))
                        config.Options["includeHeader"] = "false";
                    return OdinFormatter.FormatWithModifiers(output, config, modifiers);

                case "json":
                    return JsonFormatter.Format(output, config);

                case "xml":
                    return XmlFormatter.FormatWithModifiers(output, config, modifiers);

                case "csv":
                    return CsvFormatter.Format(output, config);

                case "fixed-width":
                    if (onWarning != null)
                        CheckFixedWidthPositionOverflow(segments, config, onWarning);
                    return FixedWidthFormatter.FormatFromSegments(output, segments, config);

                case "flat":
                case "properties":
                    return FlatFormatter.Format(output, config);

                case "":
                    // No format declared: emit JSON.
                    return JsonFormatter.Format(output, config);

                default:
                    // T006: unsupported target format. Report and emit no output.
                    onError?.Invoke(new TransformError
                    {
                        Code = TransformErrorCode.InvalidOutputFormat.Code(),
                        Message = "Invalid or unsupported output format: " + targetFormat,
                    });
                    return "";
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Type directives / coercion
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Apply mapping-level directives. When the expression is a verb call,
        /// pos/len are skipped since they were already applied at the ref arg level.
        /// For bare copy expressions, pos/len are the only place they exist so they must be applied.
        /// </summary>
        private static DynValue ApplyMappingDirectives(DynValue val, List<OdinDirective> directives, string sourceFormat, FieldExpression? expr = null)
        {
            if (directives.Count == 0) return val;

            // Only filter pos/len when they were promoted from a verb arg (already applied at ref level)
            if (expr is TransformExpression)
            {
                var filtered = new List<OdinDirective>();
                foreach (var d in directives)
                {
                    if (d.Name != "pos" && d.Name != "len")
                        filtered.Add(d);
                }
                return filtered.Count > 0 ? ApplyDirectivesForSource(val, filtered, sourceFormat) : val;
            }

            return ApplyDirectivesForSource(val, directives, sourceFormat);
        }

        private static DynValue ApplyDirectivesForSource(DynValue val, List<OdinDirective> directives, string sourceFormat)
        {
            if (directives.Count == 0) return val;

            bool isRawText = sourceFormat == "fixed-width" || sourceFormat == "flat" || sourceFormat == "flat-kvp"
                             || sourceFormat == "flat-yaml" || sourceFormat == "csv" || sourceFormat == "delimited";

            if (isRawText)
                return ApplyTypeDirectives(val, directives);

            // Filter out extraction directives for structured formats
            var filtered = new List<OdinDirective>();
            foreach (var d in directives)
            {
                if (d.Name != "pos" && d.Name != "len" && d.Name != "leftPad" && d.Name != "rightPad" && d.Name != "truncate")
                    filtered.Add(d);
            }
            return filtered.Count > 0 ? ApplyTypeDirectives(val, filtered) : val;
        }

        internal static DynValue ApplyTypeDirectives(DynValue val, List<OdinDirective> directives)
        {
            if (directives.Count == 0) return val;

            int? pos = null, len = null, fieldIndex = null;
            bool shouldTrim = false;
            byte? decimalPlaces = null;
            string? currencyCode = null;
            string? typeNameFound = null;

            foreach (var dir in directives)
            {
                switch (dir.Name)
                {
                    case "pos": pos = DirectiveAsInt(dir); break;
                    case "len": len = DirectiveAsInt(dir); break;
                    case "field": fieldIndex = DirectiveAsInt(dir); break;
                    case "trim": shouldTrim = true; break;
                    case "type":
                        typeNameFound = dir.Value?.AsString();
                        break;
                    case "decimals":
                    {
                        var numVal = dir.Value?.AsNumber();
                        if (numVal.HasValue) decimalPlaces = (byte)numVal.Value;
                        else
                        {
                            var strVal = dir.Value?.AsString();
                            if (strVal != null && byte.TryParse(strVal, NumberStyles.Integer, CultureInfo.InvariantCulture, out var dp))
                                decimalPlaces = dp;
                        }
                        break;
                    }
                    case "currencyCode":
                        currencyCode = dir.Value?.AsString();
                        break;
                    case "default":
                        if (val.Type == DynValueType.Null)
                        {
                            var defaultStr = dir.Value?.AsString();
                            if (defaultStr != null)
                                val = DynValue.String(defaultStr);
                        }
                        break;
                    case "date": case "time": case "duration": case "timestamp":
                    case "boolean": case "integer": case "number":
                    case "currency": case "reference": case "binary": case "percent":
                        typeNameFound = dir.Name;
                        break;
                }
            }

            // Phase 1: extraction
            if (pos.HasValue || fieldIndex.HasValue || shouldTrim)
            {
                string s;
                if (val.Type == DynValueType.String) s = val.AsString() ?? "";
                else if (val.Type == DynValueType.Null) return val;
                else s = CoerceToString(val);

                if (fieldIndex.HasValue)
                {
                    var fields = s.Split(',');
                    s = fieldIndex.Value < fields.Length ? fields[fieldIndex.Value] : "";
                }
                if (pos.HasValue)
                {
                    int start = Math.Min(pos.Value, s.Length);
                    if (len.HasValue)
                    {
                        int end = Math.Min(start + len.Value, s.Length);
                        s = s.Substring(start, end - start);
                    }
                    else
                    {
                        s = s.Substring(start);
                    }
                }
                if (shouldTrim) s = s.Trim();
                val = DynValue.String(s);
            }

            // Phase 2: type coercion
            if (typeNameFound != null)
                return CoerceToType(val, typeNameFound, decimalPlaces, currencyCode);

            return val;
        }

        private static int? DirectiveAsInt(OdinDirective dir)
        {
            var numVal = dir.Value?.AsNumber();
            if (numVal.HasValue) return (int)numVal.Value;
            var strVal = dir.Value?.AsString();
            if (strVal != null && int.TryParse(strVal, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v)) return v;
            return null;
        }

        private static DynValue CoerceToType(DynValue val, string typeName, byte? decimalPlaces, string? currencyCode)
        {
            switch (typeName)
            {
                case "integer":
                {
                    var d = val.AsDouble();
                    if (d.HasValue) return DynValue.Integer((long)d.Value);
                    var s = val.AsString();
                    if (s != null && long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i))
                        return DynValue.Integer(i);
                    var b = val.AsBool();
                    if (b.HasValue) return DynValue.Integer(b.Value ? 1 : 0);
                    return val;
                }
                case "number":
                {
                    if (val.Type == DynValueType.Integer) return DynValue.Float((double)val.AsInt64()!.Value);
                    if (val.Type == DynValueType.Currency) return DynValue.Float(val.AsDouble()!.Value);
                    var s = val.AsString();
                    if (s != null && double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var f))
                    {
                        if (f.ToString(CultureInfo.InvariantCulture) == s)
                            return DynValue.Float(f);
                        return DynValue.FloatRaw(s);
                    }
                    return val;
                }
                case "currency":
                {
                    byte dp = decimalPlaces ?? 2;
                    if (val.Type == DynValueType.Float)
                    {
                        double fv = val.AsDouble()!.Value;
                        // Check if fixed-point representation adds trailing zeros
                        // (e.g., 149.5 → "149.50" with dp=2). If so, preserve as raw.
                        var fixedStr = fv.ToString("F" + dp, CultureInfo.InvariantCulture);
                        var gStr = fv.ToString("G", CultureInfo.InvariantCulture);
                        if (fixedStr != gStr)
                            return DynValue.CurrencyRaw(fixedStr, dp, currencyCode);
                        return DynValue.Currency(fv, dp, currencyCode);
                    }
                    if (val.Type == DynValueType.Integer) return DynValue.Currency((double)val.AsInt64()!.Value, dp, currencyCode);
                    var s = val.AsString();
                    if (s != null)
                    {
                        var cleaned = s.Replace("$", "").Replace(",", "").Replace("\u00A3", "").Replace("\u20AC", "");
                        byte actualDp = decimalPlaces ?? (byte)(s.IndexOf('.') >= 0 ? s.Length - s.IndexOf('.') - 1 : 2);
                        if (double.TryParse(cleaned, NumberStyles.Float, CultureInfo.InvariantCulture, out var f))
                        {
                            // Always preserve raw string from text sources (CSV, fixed-width)
                            // so that trailing zeros survive roundtrips (e.g., "149.50")
                            var rt = f.ToString("G", CultureInfo.InvariantCulture);
                            if (rt == cleaned)
                                return DynValue.Currency(f, actualDp, currencyCode);
                            return DynValue.CurrencyRaw(cleaned, actualDp, currencyCode);
                        }
                    }
                    return val;
                }
                case "percent":
                {
                    if (val.Type == DynValueType.Float) return DynValue.Percent(val.AsDouble()!.Value);
                    if (val.Type == DynValueType.Integer) return DynValue.Percent((double)val.AsInt64()!.Value);
                    var s = val.AsString();
                    if (s != null)
                    {
                        var cleaned = s.Replace("%", "");
                        if (double.TryParse(cleaned, NumberStyles.Float, CultureInfo.InvariantCulture, out var f))
                            return DynValue.Percent(f);
                    }
                    return val;
                }
                case "boolean":
                {
                    var s = val.AsString();
                    if (s != null)
                    {
                        switch (s.ToLowerInvariant())
                        {
                            case "true": case "yes": case "1": return DynValue.Bool(true);
                            case "false": case "no": case "0": return DynValue.Bool(false);
                        }
                    }
                    if (val.Type == DynValueType.Integer) return DynValue.Bool(val.AsInt64()!.Value != 0);
                    if (val.Type == DynValueType.Float) return DynValue.Bool(val.AsDouble()!.Value != 0.0);
                    return val;
                }
                case "date":
                    return val.Type == DynValueType.String ? DynValue.Date(val.AsString()!) : val;
                case "time":
                    return val.Type == DynValueType.String ? DynValue.Time(val.AsString()!) : val;
                case "timestamp":
                    if (val.Type == DynValueType.String)
                    {
                        string tsStr = val.AsString()!;
                        // Normalize to UTC ISO 8601 with milliseconds
                        if (DateTimeOffset.TryParse(tsStr, CultureInfo.InvariantCulture,
                            DateTimeStyles.None, out var dto))
                        {
                            string normalized = dto.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);
                            return DynValue.Timestamp(normalized);
                        }
                        return DynValue.Timestamp(tsStr);
                    }
                    return val;
                case "duration":
                    return val.Type == DynValueType.String ? DynValue.Duration(val.AsString()!) : val;
                case "reference":
                    return val.Type == DynValueType.String ? DynValue.Reference(val.AsString()!) : val;
                case "binary":
                    return val.Type == DynValueType.String ? DynValue.Binary(val.AsString()!) : val;
                default:
                    return val;
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Evaluate whether a <see cref="DynValue"/> is truthy.
        /// </summary>
        // Evaluates a segment condition: "path op value" comparison or a truthy path check.
        // Evaluates a segment condition: a verb expression (coerced to truthy),
        // or a legacy quoted-infix string.
        private static bool EvaluateSegmentCondition(TransformSegment segment, ExecContext ctx)
        {
            if (segment.ConditionExpr != null)
            {
                var val = EvaluateExpression(segment.ConditionExpr, ctx, ctx.Source, ctx.GlobalOutput);
                return IsTruthy(val);
            }
            return EvaluateInfixCondition(segment.Condition ?? "", ctx);
        }

        private static bool EvaluateInfixCondition(string condition, ExecContext ctx)
        {
            string trimmed = condition.Trim();

            var m = ConditionPattern.Match(trimmed);
            if (m.Success)
            {
                string pathPart = m.Groups[1].Value;
                string op = m.Groups[2].Value;
                string valuePart = m.Groups[3].Value.Trim();

                var left = ResolvePath(ctx.Source, pathPart, ctx.Constants, ctx.Accumulators);
                var right = ParseConditionValue(valuePart);
                return CompareConditionValues(left, op, right);
            }

            var val = ResolvePath(ctx.Source, trimmed, ctx.Constants, ctx.Accumulators);
            return IsTruthy(val);
        }

        private static readonly System.Text.RegularExpressions.Regex ConditionPattern =
            new System.Text.RegularExpressions.Regex(
                @"^(@?[\w.\[\]]+)\s*(=|==|!=|<>|<=|>=|<|>)\s*(.+)$",
                System.Text.RegularExpressions.RegexOptions.Compiled);

        // Parses the right-hand side of a comparison into a typed value.
        private static DynValue ParseConditionValue(string raw)
        {
            if (raw.Length >= 2 && raw[0] == '\'' && raw[raw.Length - 1] == '\'')
                return DynValue.String(raw.Substring(1, raw.Length - 2));
            if (raw.Length >= 2 && raw[0] == '"' && raw[raw.Length - 1] == '"')
                return DynValue.String(raw.Substring(1, raw.Length - 2));

            string lower = raw.ToLowerInvariant();
            if (lower == "true") return DynValue.Bool(true);
            if (lower == "false") return DynValue.Bool(false);
            if (lower == "null" || lower == "nil") return DynValue.Null();

            if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out double num))
            {
                if (num == Math.Floor(num) && !double.IsInfinity(num))
                    return DynValue.Integer((long)num);
                return DynValue.Float(num);
            }

            return DynValue.String(raw);
        }

        private static bool CompareConditionValues(DynValue left, string op, DynValue right)
        {
            string leftStr = CoerceToString(left);
            string rightStr = CoerceToString(right);

            var leftNum = ToComparableNumber(left);
            var rightNum = ToComparableNumber(right);
            bool numeric = leftNum.HasValue && rightNum.HasValue;

            switch (op)
            {
                case "=":
                case "==":
                    return leftStr == rightStr;
                case "!=":
                case "<>":
                    return leftStr != rightStr;
                case "<":
                    return numeric ? leftNum!.Value < rightNum!.Value
                                   : string.CompareOrdinal(leftStr, rightStr) < 0;
                case "<=":
                    return numeric ? leftNum!.Value <= rightNum!.Value
                                   : string.CompareOrdinal(leftStr, rightStr) <= 0;
                case ">":
                    return numeric ? leftNum!.Value > rightNum!.Value
                                   : string.CompareOrdinal(leftStr, rightStr) > 0;
                case ">=":
                    return numeric ? leftNum!.Value >= rightNum!.Value
                                   : string.CompareOrdinal(leftStr, rightStr) >= 0;
                default:
                    return false;
            }
        }

        private static double? ToComparableNumber(DynValue val)
        {
            switch (val.Type)
            {
                case DynValueType.Integer: return val.AsInt64();
                case DynValueType.Float:
                case DynValueType.Currency:
                case DynValueType.Percent:
                    return val.AsDouble();
                case DynValueType.String:
                {
                    var s = val.AsString();
                    if (s != null && double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double d))
                        return d;
                    return null;
                }
                default:
                    return null;
            }
        }

        public static bool IsTruthy(DynValue val)
        {
            switch (val.Type)
            {
                case DynValueType.Null: return false;
                case DynValueType.Bool: return val.AsBool() == true;
                case DynValueType.Integer: return val.AsInt64() != 0;
                case DynValueType.Float:
                case DynValueType.Currency:
                case DynValueType.Percent:
                    return val.AsDouble() != 0.0;
                case DynValueType.FloatRaw:
                case DynValueType.CurrencyRaw:
                {
                    var s = val.AsString();
                    return !string.IsNullOrEmpty(s) && s != "0";
                }
                case DynValueType.String:
                case DynValueType.Reference:
                case DynValueType.Binary:
                case DynValueType.Date:
                case DynValueType.Timestamp:
                case DynValueType.Time:
                case DynValueType.Duration:
                    return !string.IsNullOrEmpty(val.AsString());
                case DynValueType.Array:
                {
                    var arr = val.AsArray();
                    return arr != null && arr.Count > 0;
                }
                case DynValueType.Object:
                {
                    var obj = val.AsObject();
                    return obj != null && obj.Count > 0;
                }
                default:
                    return false;
            }
        }

        private static string CoerceToString(DynValue val)
        {
            switch (val.Type)
            {
                case DynValueType.String: return val.AsString() ?? "";
                case DynValueType.Integer: return val.AsInt64()?.ToString(CultureInfo.InvariantCulture) ?? "";
                case DynValueType.Float: return val.AsDouble()?.ToString(CultureInfo.InvariantCulture) ?? "";
                case DynValueType.Bool: return val.AsBool() == true ? "true" : "false";
                case DynValueType.Null: return "";
                default: return val.ToString();
            }
        }

        private static bool IsParseableFormat(string fmt)
        {
            return fmt == "csv" || fmt == "delimited" || fmt == "fixed-width" || fmt == "xml"
                   || fmt == "json" || fmt == "yaml" || fmt == "flat-kvp" || fmt == "flat-yaml";
        }

        /// <summary>
        /// Convert an <see cref="OdinDocument"/> into a <see cref="DynValue"/> object tree.
        /// Assignments are reconstructed into nested objects using their dotted path keys.
        /// </summary>
        /// <param name="doc">The source ODIN document.</param>
        /// <returns>A <see cref="DynValue"/> representing the document's assignment data.</returns>
        public static DynValue OdinDocumentToDynValue(OdinDocument doc)
        {
            var root = DynValue.Object(new List<KeyValuePair<string, DynValue>>());

            foreach (var kvp in doc.Assignments)
            {
                // Skip metadata entries
                if (kvp.Key.StartsWith("$.", StringComparison.Ordinal)) continue;

                var value = OdinValueToDyn(kvp.Value);
                SetPath(ref root, kvp.Key, value);
            }

            return root;
        }
    }
}
