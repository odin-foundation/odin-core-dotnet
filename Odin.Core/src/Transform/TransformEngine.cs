#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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

        public ExecContext()
        {
            Source = DynValue.Null();
            Constants = new Dictionary<string, DynValue>();
            Accumulators = new Dictionary<string, DynValue>();
            Tables = new Dictionary<string, LookupTable>();
            LoopVars = new Dictionary<string, DynValue>();
            Verbs = new Dictionary<string, Func<DynValue[], VerbContext, DynValue>>();
            Warnings = new List<TransformWarning>();
            Errors = new List<TransformError>();
            GlobalOutput = DynValue.Object(new List<KeyValuePair<string, DynValue>>());
            FieldModifiers = new Dictionary<string, OdinModifiers>();
            SourceFormat = "";
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
        {
            if (transform == null) throw new ArgumentNullException(nameof(transform));
            if (doc == null) throw new ArgumentNullException(nameof(doc));

            var dynSource = OdinDocumentToDynValue(doc);
            return Execute(transform, dynSource);
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
        {
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
                }

                ProcessSegment(seg, ctx, ref output, "");
                ctx.GlobalOutput = output;
            }

            // 4. Apply confidential enforcement
            if (ctx.EnforceConfidential.HasValue)
                ApplyConfidentialEnforcement(transform.Segments, ctx.EnforceConfidential.Value, ref output);

            // 5. Format the output
            string formatted = FormatOutput(output, transform.Target.Format, transform.Target.Options,
                transform.Segments, ctx.FieldModifiers);

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
                transform.Segments, ctx.FieldModifiers);

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
                Verbs = new Dictionary<string, Func<DynValue[], VerbContext, DynValue>>(VerbRegistry),
                Warnings = new List<TransformWarning>(),
                Errors = new List<TransformError>(),
                EnforceConfidential = transform.EnforceConfidential,
                GlobalOutput = DynValue.Object(new List<KeyValuePair<string, DynValue>>()),
                FieldModifiers = new Dictionary<string, OdinModifiers>(),
                SourceFormat = sourceFormat,
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

        private static void ProcessSegment(TransformSegment segment, ExecContext ctx, ref DynValue output, string pathPrefix)
        {
            // Check condition
            if (segment.Condition != null)
            {
                var condVal = ResolvePath(ctx.Source, segment.Condition, ctx.Constants, ctx.Accumulators);
                if (!IsTruthy(condVal)) return;
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
            // Execute mappings for side effects (like accumulate) but don't write to output
            if (!isRoot && cleanName.StartsWith("_", StringComparison.Ordinal) && arrayIndex == null)
            {
                // Process all mappings for side effects only
                var dummyOutput = DynValue.Object(new List<KeyValuePair<string, DynValue>>());
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

            // Array loop
            if (segment.SourcePath != null)
            {
                var sourceVal = ResolvePath(ctx.Source, segment.SourcePath, ctx.Constants, ctx.Accumulators);
                // Missing/null source produces an empty array (no iterations)
                if (sourceVal.Type == DynValueType.Null)
                {
                    var emptyArr = DynValue.Array(new List<DynValue>());
                    if (isRoot)
                        output = emptyArr;
                    else
                        SetPath(ref output, cleanName, emptyArr);
                    return;
                }
                var arrayVal = sourceVal.Type == DynValueType.Array ? sourceVal : DynValue.Array(new List<DynValue> { sourceVal });
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
            try
            {
                var val = EvaluateExpression(mapping.Expression, ctx, currentSource, outputSnapshot);
                val = ApplyMappingDirectives(val, mapping.Directives, ctx.SourceFormat, mapping.Expression);

                // Apply confidential at mapping level
                if (mapping.Modifiers != null && mapping.Modifiers.Confidential && ctx.EnforceConfidential.HasValue)
                    val = ApplyConfidentialToValue(val, ctx.EnforceConfidential.Value);

                // Target "_" is a side-effect-only field (e.g., for accumulate); do not write to output
                if (mapping.Target != "_")
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
                ctx.Errors.Add(new TransformError
                {
                    Message = $"mapping '{mapping.Target}': {e.Message}",
                    Path = mapping.Target,
                });
            }
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
                    return ResolvePathWithOutput(currentSource, currentOutput, ctx.GlobalOutput, path, ctx.Constants, ctx.Accumulators);
                }

                case LiteralExpression lit:
                    return OdinValueToDyn(lit.Value);

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

            // Standard eager evaluation
            var evaluatedArgs = new DynValue[call.Args.Count];
            for (int i = 0; i < call.Args.Count; i++)
                evaluatedArgs[i] = EvaluateVerbArg(call.Args[i], ctx, currentSource, currentOutput);

            // Look up verb
            if (!ctx.Verbs.TryGetValue(call.Verb, out var verbFn))
            {
                if (call.IsCustom)
                    return evaluatedArgs.Length > 0 ? evaluatedArgs[0] : DynValue.Null();
                throw new InvalidOperationException("unknown verb: '" + call.Verb + "'");
            }

            var verbCtx = new VerbContext
            {
                Source = currentSource,
                LoopVars = new Dictionary<string, DynValue>(ctx.LoopVars),
                Accumulators = new Dictionary<string, DynValue>(ctx.Accumulators),
                Tables = ctx.Tables,
                GlobalOutput = ctx.GlobalOutput,
            };

            var result = verbFn(evaluatedArgs, verbCtx);

            // Merge verb-level errors (T011, etc.) into engine errors
            if (verbCtx.Errors.Count > 0)
                ctx.Errors.AddRange(verbCtx.Errors);

            // accumulate / set: update context accumulators
            if ((call.Verb == "accumulate" || call.Verb == "set") && evaluatedArgs.Length > 0)
            {
                var nameStr = evaluatedArgs[0].AsString();
                if (nameStr != null)
                    ctx.Accumulators[nameStr] = result;
            }

            return result;
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

            // Navigate to parent, creating intermediates
            var current = output;
            for (int i = 0; i < parts.Count - 1; i++)
                current = EnsureAndDescend(ref current, parts[i]);

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
                    int bEnd = seg.IndexOf(']');
                    if (bStart >= 0 && bEnd > bStart)
                    {
                        var name = seg.Substring(0, bStart);
                        var idxStr = seg.Substring(bStart + 1, bEnd - bStart - 1);
                        if (int.TryParse(idxStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var idx))
                        {
                            parts.Add(new SetPathPart { Type = SetPathPartType.ArrayIndex, Name = name, Index = idx });
                            continue;
                        }
                    }
                    parts.Add(new SetPathPart { Type = SetPathPartType.Field, Name = seg });
                }
            }

            return parts;
        }

        private static void SetSingleField(ref DynValue obj, SetPathPart part, DynValue value)
        {
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

        private static DynValue EnsureAndDescend(ref DynValue current, SetPathPart part)
        {
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

                    while (arr.Count <= part.Index)
                        arr.Add(DynValue.Object(new List<KeyValuePair<string, DynValue>>()));
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
                case OdinInteger i: return DynValue.Integer(i.Value);
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

        private static string FormatOutput(
            DynValue output, string targetFormat, Dictionary<string, string> options,
            List<TransformSegment> segments, Dictionary<string, OdinModifiers> modifiers)
        {
            if (OutputFormatter != null)
                return OutputFormatter(output, targetFormat, options, modifiers);

            // Dispatch to built-in formatters
            var config = new TargetConfig { Format = targetFormat, Options = new Dictionary<string, string>(options) };

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
                    return FixedWidthFormatter.FormatFromSegments(output, segments, config);

                case "flat":
                case "properties":
                    return FlatFormatter.Format(output, config);

                default:
                    // Unknown format: fall back to JSON
                    return JsonFormatter.Format(output, config);
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
        private static DynValue OdinDocumentToDynValue(OdinDocument doc)
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
