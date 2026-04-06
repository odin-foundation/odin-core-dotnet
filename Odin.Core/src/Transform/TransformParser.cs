#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using Odin.Core.Parsing;
using Odin.Core.Types;

namespace Odin.Core.Transform
{
    /// <summary>
    /// Parses ODIN transform specification text or a pre-parsed <see cref="OdinDocument"/>
    /// into an <see cref="OdinTransform"/> structure. The parser interprets metadata,
    /// assignments, segments, field mappings, lookup tables, and verb expressions to
    /// build the complete transform specification used by <see cref="TransformEngine"/>.
    /// </summary>
    public static class TransformParser
    {
        // ─────────────────────────────────────────────────────────────────────
        // Public entry points
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Parse raw ODIN transform specification text into an <see cref="OdinTransform"/>.
        /// This is the primary entry point: it tokenizes and parses the text into an
        /// <see cref="OdinDocument"/>, then converts it into a transform specification.
        /// </summary>
        /// <param name="input">The raw ODIN transform specification text.</param>
        /// <returns>A fully populated <see cref="OdinTransform"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="input"/> is null.</exception>
        /// <exception cref="OdinParseException">Thrown on parse errors (P001-P015).</exception>
        public static OdinTransform Parse(string input)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            var doc = OdinParser.Parse(input);
            return ParseTransformDoc(doc);
        }

        /// <summary>
        /// Parse an already-parsed <see cref="OdinDocument"/> into an <see cref="OdinTransform"/>.
        /// </summary>
        /// <param name="doc">The ODIN document containing transform specification.</param>
        /// <returns>A fully populated <see cref="OdinTransform"/>.</returns>
        public static OdinTransform ParseTransformDoc(OdinDocument doc)
        {
            var metadata = ParseMetadata(doc);
            var source = ParseSourceConfig(doc);
            var target = ParseTargetConfig(doc);
            var constants = ParseConstants(doc);
            var accumulators = ParseAccumulators(doc);
            var tables = ParseLookupTables(doc);
            var imports = ParseImports(doc);
            var enforceConfidential = ParseEnforceConfidential(doc);
            var strictTypes = ParseStrictTypes(doc);
            var segments = ParseSegments(doc);
            var passes = CollectPasses(segments);

            return new OdinTransform
            {
                Metadata = metadata,
                Source = source,
                Target = target,
                Constants = constants,
                Accumulators = accumulators,
                Tables = tables,
                Segments = segments,
                Imports = imports,
                Passes = passes,
                EnforceConfidential = enforceConfidential,
                StrictTypes = strictTypes,
            };
        }

        // ─────────────────────────────────────────────────────────────────────
        // Metadata
        // ─────────────────────────────────────────────────────────────────────

        private static TransformMetadata ParseMetadata(OdinDocument doc)
        {
            return new TransformMetadata
            {
                OdinVersion = GetMetaString(doc, "odin"),
                TransformVersion = GetMetaString(doc, "transform"),
                Direction = GetMetaString(doc, "direction"),
                Name = GetMetaString(doc, "name"),
                Description = GetMetaString(doc, "description"),
            };
        }

        // ─────────────────────────────────────────────────────────────────────
        // Source / Target Config
        // ─────────────────────────────────────────────────────────────────────

        private static SourceConfig? ParseSourceConfig(OdinDocument doc)
        {
            var format = GetMetaString(doc, "source.format");
            if (format == null) return null;

            var options = new Dictionary<string, string>();
            var namespaces = new Dictionary<string, string>();

            foreach (var entry in doc.Metadata)
            {
                if (entry.Key.StartsWith("source.namespace.", StringComparison.Ordinal))
                {
                    var rest = entry.Key.Substring("source.namespace.".Length);
                    namespaces[rest] = OdinValueToString(entry.Value);
                }
                else if (entry.Key.StartsWith("source.", StringComparison.Ordinal))
                {
                    var rest = entry.Key.Substring("source.".Length);
                    if (rest != "format")
                    {
                        options[rest] = OdinValueToString(entry.Value);
                    }
                }
            }

            var discriminator = ParseSourceDiscriminator(doc);

            return new SourceConfig
            {
                Format = format,
                Options = options,
                Namespaces = namespaces,
                Discriminator = discriminator,
            };
        }

        private static SourceDiscriminator? ParseSourceDiscriminator(OdinDocument doc)
        {
            var discTypeStr = GetMetaString(doc, "source.discriminator.type");
            if (discTypeStr == null) return null;

            DiscriminatorType discType;
            switch (discTypeStr)
            {
                case "position": discType = DiscriminatorType.Position; break;
                case "field": discType = DiscriminatorType.Field; break;
                case "path": discType = DiscriminatorType.Path; break;
                default: return null;
            }

            int? pos = null;
            int? len = null;
            int? field = null;
            string? path = null;

            var posStr = GetMetaString(doc, "source.discriminator.pos");
            if (posStr != null && int.TryParse(posStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var posVal))
                pos = posVal;

            var lenStr = GetMetaString(doc, "source.discriminator.len");
            if (lenStr != null && int.TryParse(lenStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var lenVal))
                len = lenVal;

            var fieldStr = GetMetaString(doc, "source.discriminator.field");
            if (fieldStr != null && int.TryParse(fieldStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var fieldVal))
                field = fieldVal;

            path = GetMetaString(doc, "source.discriminator.path");

            return new SourceDiscriminator
            {
                Type = discType,
                Pos = pos,
                Len = len,
                Field = field,
                Path = path,
            };
        }

        private static TargetConfig ParseTargetConfig(OdinDocument doc)
        {
            var format = GetMetaString(doc, "target.format") ?? "";
            var options = new Dictionary<string, string>();

            foreach (var entry in doc.Metadata)
            {
                if (entry.Key.StartsWith("target.", StringComparison.Ordinal))
                {
                    var rest = entry.Key.Substring("target.".Length);
                    if (rest != "format")
                    {
                        options[rest] = OdinValueToString(entry.Value);
                    }
                }
            }

            return new TargetConfig { Format = format, Options = options };
        }

        // ─────────────────────────────────────────────────────────────────────
        // Constants
        // ─────────────────────────────────────────────────────────────────────

        private static Dictionary<string, OdinValue> ParseConstants(OdinDocument doc)
        {
            var constants = new Dictionary<string, OdinValue>();
            var arrayEntries = new Dictionary<string, List<(int Index, OdinValue Value)>>();

            // Constants can appear in Metadata (from {$} header) or Assignments (from {const} section)
            foreach (var source in new[] { doc.Metadata, doc.Assignments })
            {
                foreach (var entry in source)
                {
                    if (!entry.Key.StartsWith("const.", StringComparison.Ordinal)) continue;

                    var name = entry.Key.Substring("const.".Length);

                    // Check for array index syntax: name[N]
                    int bracketPos = name.IndexOf('[');
                    if (bracketPos >= 0 && name.EndsWith("]", StringComparison.Ordinal))
                    {
                        var baseName = name.Substring(0, bracketPos);
                        var idxStr = name.Substring(bracketPos + 1, name.Length - bracketPos - 2);
                        if (int.TryParse(idxStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var idx))
                        {
                            if (!arrayEntries.ContainsKey(baseName))
                                arrayEntries[baseName] = new List<(int, OdinValue)>();
                            arrayEntries[baseName].Add((idx, entry.Value));
                            continue;
                        }
                    }

                    constants[name] = entry.Value;
                }
            }

            // Build arrays from indexed entries
            foreach (var kvp in arrayEntries)
            {
                var entries = kvp.Value;
                entries.Sort((a, b) => a.Index.CompareTo(b.Index));
                int maxIdx = 0;
                foreach (var e in entries)
                    if (e.Index > maxIdx) maxIdx = e.Index;

                var arr = new List<OdinArrayItem>(maxIdx + 1);
                for (int i = 0; i <= maxIdx; i++)
                    arr.Add(OdinArrayItem.FromValue(new OdinNull()));

                foreach (var e in entries)
                    arr[e.Index] = OdinArrayItem.FromValue(e.Value);

                constants[kvp.Key] = OdinValues.Array(arr.ToArray());
            }

            return constants;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Accumulators
        // ─────────────────────────────────────────────────────────────────────

        private static Dictionary<string, AccumulatorDef> ParseAccumulators(OdinDocument doc)
        {
            var accumulators = new Dictionary<string, AccumulatorDef>();

            // First pass: create definitions (check both Metadata and Assignments)
            foreach (var source in new[] { doc.Metadata, doc.Assignments })
            {
                foreach (var entry in source)
                {
                    if (!entry.Key.StartsWith("accumulator.", StringComparison.Ordinal)) continue;

                    var name = entry.Key.Substring("accumulator.".Length);
                    if (name.EndsWith("._persist", StringComparison.Ordinal)) continue;

                    accumulators[name] = new AccumulatorDef
                    {
                        Name = name,
                        Initial = entry.Value,
                        Persist = false,
                    };
                }
            }

            // Second pass: set persist flags
            foreach (var source in new[] { doc.Metadata, doc.Assignments })
            {
                foreach (var entry in source)
                {
                    if (!entry.Key.StartsWith("accumulator.", StringComparison.Ordinal)) continue;
                    var name = entry.Key.Substring("accumulator.".Length);
                    if (!name.EndsWith("._persist", StringComparison.Ordinal)) continue;

                    var accName = name.Substring(0, name.Length - "._persist".Length);
                    if (accumulators.TryGetValue(accName, out var def))
                    {
                        def.Persist = entry.Value is OdinBoolean b && b.Value;
                    }
                }
            }

            return accumulators;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Lookup Tables
        // ─────────────────────────────────────────────────────────────────────

        private static Dictionary<string, LookupTable> ParseLookupTables(OdinDocument doc)
        {
            var tables = new Dictionary<string, LookupTable>();
            var tableRows = new Dictionary<string, List<(int RowIndex, string Column, DynValue Value)>>();
            var tableDefaults = new Dictionary<string, DynValue>();

            // Tables can appear in Metadata (from {$} header) or Assignments (from {table.*} sections)
            foreach (var source in new[] { doc.Metadata, doc.Assignments })
            {
                foreach (var entry in source)
                {
                    if (!entry.Key.StartsWith("table.", StringComparison.Ordinal)) continue;
                    var rest = entry.Key.Substring("table.".Length);

                    // Check for default: table.NAME._default
                    if (rest.EndsWith("._default", StringComparison.Ordinal))
                    {
                        var nameAndDefault = rest.Substring(0, rest.Length - "._default".Length);
                        if (nameAndDefault.Length > 0 && nameAndDefault.IndexOf('[') < 0)
                        {
                            tableDefaults[nameAndDefault] = OdinValueToDynForTable(entry.Value);
                            continue;
                        }
                    }

                    // Parse NAME[row].column
                    int bracketPos = rest.IndexOf('[');
                    if (bracketPos < 0) continue;
                    int closePos = rest.IndexOf(']', bracketPos);
                    if (closePos < 0) continue;

                    var tableName = rest.Substring(0, bracketPos);
                    var idxStr = rest.Substring(bracketPos + 1, closePos - bracketPos - 1);
                    var afterBracket = rest.Substring(closePos + 1);

                    if (!int.TryParse(idxStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var rowIdx))
                        continue;

                    var colName = afterBracket.StartsWith(".", StringComparison.Ordinal)
                        ? afterBracket.Substring(1)
                        : afterBracket;

                    if (string.IsNullOrEmpty(colName)) continue;

                    if (!tableRows.ContainsKey(tableName))
                        tableRows[tableName] = new List<(int, string, DynValue)>();

                    tableRows[tableName].Add((rowIdx, colName, OdinValueToDynForTable(entry.Value)));
                }
            }

            // Build lookup tables
            foreach (var kvp in tableRows)
            {
                var tableName = kvp.Key;
                var rows = kvp.Value;

                // Discover column names in first-appearance order
                var columns = new List<string>();
                foreach (var r in rows)
                {
                    if (!columns.Contains(r.Column))
                        columns.Add(r.Column);
                }

                // Find max row index
                int maxRow = 0;
                foreach (var r in rows)
                    if (r.RowIndex > maxRow) maxRow = r.RowIndex;

                // Group by row index
                var rowData = new Dictionary<int, Dictionary<string, DynValue>>();
                foreach (var r in rows)
                {
                    if (!rowData.ContainsKey(r.RowIndex))
                        rowData[r.RowIndex] = new Dictionary<string, DynValue>();
                    rowData[r.RowIndex][r.Column] = r.Value;
                }

                // Build ordered row array
                var builtRows = new List<List<DynValue>>();
                for (int i = 0; i <= maxRow; i++)
                {
                    if (rowData.TryGetValue(i, out var rd))
                    {
                        var row = new List<DynValue>();
                        foreach (var col in columns)
                        {
                            row.Add(rd.TryGetValue(col, out var v) ? v : DynValue.Null());
                        }
                        builtRows.Add(row);
                    }
                }

                tableDefaults.TryGetValue(tableName, out var defaultVal);

                tables[tableName] = new LookupTable
                {
                    Name = tableName,
                    Columns = columns,
                    Rows = builtRows,
                    Default = defaultVal,
                };
            }

            return tables;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Imports
        // ─────────────────────────────────────────────────────────────────────

        private static List<ImportRef> ParseImports(OdinDocument doc)
        {
            var imports = new List<ImportRef>();
            foreach (var imp in doc.Imports)
            {
                imports.Add(new ImportRef { Path = imp.Path, Alias = imp.Alias });
            }
            return imports;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Confidential / Strict Types
        // ─────────────────────────────────────────────────────────────────────

        private static ConfidentialMode? ParseEnforceConfidential(OdinDocument doc)
        {
            var val = GetMetaString(doc, "enforceConfidential");
            if (val == null) return null;
            switch (val)
            {
                case "redact": return ConfidentialMode.Redact;
                case "mask": return ConfidentialMode.Mask;
                default: return null;
            }
        }

        private static bool ParseStrictTypes(OdinDocument doc)
        {
            var strVal = GetMetaString(doc, "strictTypes");
            if (strVal != null) return strVal == "true";

            // Try boolean metadata value
            if (doc.Metadata.TryGetValue("strictTypes", out var val))
            {
                if (val is OdinBoolean b) return b.Value;
            }
            return false;
        }

        /// <summary>
        /// Merge directive-based modifiers (:confidential, :required, :deprecated, :attr)
        /// into the existing modifiers from the document's prefix modifiers.
        /// </summary>
        private static OdinModifiers? MergeDirectiveModifiers(
            OdinModifiers? modifiers,
            List<OdinDirective> directives)
        {
            bool hasConf = false, hasReq = false, hasDep = false, hasAttr = false;
            for (int i = 0; i < directives.Count; i++)
            {
                switch (directives[i].Name)
                {
                    case "confidential": hasConf = true; break;
                    case "required": hasReq = true; break;
                    case "deprecated": hasDep = true; break;
                    case "attr": hasAttr = true; break;
                }
            }

            if (!hasConf && !hasReq && !hasDep && !hasAttr)
                return modifiers;

            bool mReq = modifiers?.Required ?? false;
            bool mConf = modifiers?.Confidential ?? false;
            bool mDep = modifiers?.Deprecated ?? false;
            bool mAttr = modifiers?.Attr ?? false;

            return new OdinModifiers
            {
                Required = mReq || hasReq,
                Confidential = mConf || hasConf,
                Deprecated = mDep || hasDep,
                Attr = mAttr || hasAttr,
            };
        }

        // ─────────────────────────────────────────────────────────────────────
        // Segments
        // ─────────────────────────────────────────────────────────────────────

        private static List<TransformSegment> ParseSegments(OdinDocument doc)
        {
            var sectionOrder = new List<string>();
            var sectionFields = new Dictionary<string, List<(string Field, OdinValue Value, OdinModifiers? Modifiers)>>();

            foreach (var entry in doc.Assignments)
            {
                if (entry.Key.StartsWith("$.", StringComparison.Ordinal)) continue;

                var (section, field) = SplitSectionKey(entry.Key);
                if (section.Length > 0 && section[0] == '$') continue;
                // Skip reserved sections handled elsewhere
                if (section == "const" || section == "accumulator" || section.StartsWith("table.", StringComparison.Ordinal)) continue;

                if (!sectionFields.TryGetValue(section, out _))
                {
                    sectionOrder.Add(section);
                    sectionFields[section] = new List<(string, OdinValue, OdinModifiers?)>();
                }

                OdinModifiers? modifiers = null;
                doc.PathModifiers.TryGetValue(entry.Key, out modifiers);

                sectionFields[section].Add((field, entry.Value, modifiers));
            }

            var segments = new List<TransformSegment>();
            foreach (var sectionName in sectionOrder)
            {
                if (sectionFields.TryGetValue(sectionName, out var fields))
                {
                    segments.Add(BuildSegment(sectionName, fields));
                }
            }

            return segments;
        }

        private static bool NeedsChildSegment(
            string childName,
            List<(string Field, OdinValue Value, OdinModifiers? Modifiers)> fields)
        {
            if (childName.Contains("[]")) return true;
            foreach (var f in fields)
            {
                if (f.Field.StartsWith("_", StringComparison.Ordinal) || f.Field.Contains("[]"))
                    return true;
                // Detect deeply nested directives (e.g., "contacts._loop" means
                // the "contacts" sub-group has a _loop directive and needs its own segment)
                if (f.Field.Contains("._"))
                    return true;
            }
            return false;
        }

        private static (string Section, string Field) SplitSectionKey(string key)
        {
            int dotPos = key.IndexOf('.');
            if (dotPos >= 0)
                return (key.Substring(0, dotPos), key.Substring(dotPos + 1));
            return ("", key);
        }

        private static TransformSegment BuildSegment(
            string name,
            List<(string Field, OdinValue Value, OdinModifiers? Modifiers)> fields)
        {
            string? sourcePath = null;
            Discriminator? discriminator = null;
            int? pass = null;
            string? condition = null;
            var mappings = new List<FieldMapping>();
            var children = new List<TransformSegment>();
            var childFields = new Dictionary<string, List<(string, OdinValue, OdinModifiers?)>>();

            // Track interleaved item order
            var itemOrder = new List<object>(); // FieldMapping or string (child ref)
            var seenChildren = new HashSet<string>();

            foreach (var (field, value, modifiers) in fields)
            {
                // Nested sub-section (e.g., "Items.Name")
                int dotPos = field.IndexOf('.');
                if (dotPos >= 0)
                {
                    var childSection = field.Substring(0, dotPos);
                    var childField = field.Substring(dotPos + 1);

                    if (seenChildren.Add(childSection))
                        itemOrder.Add(childSection);

                    if (!childFields.ContainsKey(childSection))
                        childFields[childSection] = new List<(string, OdinValue, OdinModifiers?)>();

                    childFields[childSection].Add((childField, value, modifiers));
                    continue;
                }

                if (field.StartsWith("_", StringComparison.Ordinal))
                {
                    // Directive field
                    switch (field)
                    {
                        case "_loop":
                        case "_from":
                            sourcePath = OdinValueToString(value);
                            break;
                        case "_pass":
                            if (value.AsInt64().HasValue)
                                pass = (int)value.AsInt64().Value;
                            else if (int.TryParse(OdinValueToString(value), NumberStyles.Integer, CultureInfo.InvariantCulture, out var pv))
                                pass = pv;
                            break;
                        case "_if":
                        case "_when":
                            condition = OdinValueToString(value);
                            break;
                        case "_discriminator":
                            if (value is OdinReference refVal)
                                discriminator = new Discriminator { Path = refVal.Path, Value = "" };
                            else
                                discriminator = new Discriminator { Path = OdinValueToString(value), Value = "" };
                            break;
                        case "_discriminatorValue":
                        case "_value":
                            if (discriminator != null)
                                discriminator.Value = OdinValueToString(value);
                            else
                                discriminator = new Discriminator { Path = "", Value = OdinValueToString(value) };
                            break;
                        default:
                        {
                            var m = BuildFieldMapping(field, value, modifiers);
                            itemOrder.Add(m);
                            mappings.Add(m);
                            break;
                        }
                    }
                }
                else
                {
                    var m = BuildFieldMapping(field, value, modifiers);
                    itemOrder.Add(m);
                    mappings.Add(m);
                }
            }

            // Build interleaved items list
            var items = new List<SegmentItem>();
            foreach (var itemRef in itemOrder)
            {
                if (itemRef is FieldMapping fm)
                {
                    items.Add(SegmentItem.FromMapping(fm));
                }
                else if (itemRef is string childName)
                {
                    if (childFields.TryGetValue(childName, out var cf))
                    {
                        childFields.Remove(childName);

                        if (NeedsChildSegment(childName, cf))
                        {
                            var seg = BuildSegment(childName, cf);
                            children.Add(seg);
                            items.Add(SegmentItem.FromChild(seg));
                        }
                        else
                        {
                            // Flatten: emit as dotted-path mappings
                            foreach (var (childField, childValue, mods) in cf)
                            {
                                var fullTarget = childName + "." + childField;
                                var m = BuildFieldMapping(fullTarget, childValue, mods);
                                mappings.Add(m);
                                items.Add(SegmentItem.FromMapping(m));
                            }
                        }
                    }
                }
            }

            // Rebuild mappings from items to preserve correct interleaved order
            var orderedMappings = new List<FieldMapping>();
            foreach (var item in items)
            {
                var m = item.AsMapping();
                if (m != null) orderedMappings.Add(m);
            }

            bool isArray = name.EndsWith("[]", StringComparison.Ordinal);

            return new TransformSegment
            {
                Name = name,
                Path = name,
                SourcePath = sourcePath,
                SegmentDiscriminator = discriminator,
                IsArray = isArray,
                Mappings = orderedMappings,
                Children = children,
                Items = items,
                Pass = pass,
                Condition = condition,
            };
        }

        private static FieldMapping BuildFieldMapping(string target, OdinValue value, OdinModifiers? modifiers)
        {
            var dirs = new List<OdinDirective>();
            if (value.Directives != null)
            {
                foreach (var d in value.Directives)
                    dirs.Add(d);
            }

            var (expr, trailingDirs) = ValueToFieldExpressionWithDirectives(value);

            // Merge trailing directives
            foreach (var td in trailingDirs)
            {
                bool exists = false;
                foreach (var d in dirs)
                    if (d.Name == td.Name) { exists = true; break; }
                if (!exists) dirs.Add(td);
            }

            // Promote formatting directives from verb args
            var fmtDirs = CollectFormattingDirectives(expr);
            foreach (var fd in fmtDirs)
            {
                bool exists = false;
                foreach (var d in dirs)
                    if (d.Name == fd.Name) { exists = true; break; }
                if (!exists) dirs.Add(fd);
            }

            var mergedMods = MergeDirectiveModifiers(modifiers, dirs);

            return new FieldMapping
            {
                Target = target,
                Expression = expr,
                Directives = dirs,
                Modifiers = mergedMods,
            };
        }

        // ─────────────────────────────────────────────────────────────────────
        // Verb Arity Map
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Gets the expected argument count for a verb. Returns -1 for variadic verbs.
        /// </summary>
        internal static int GetVerbArity(string verb)
        {
            switch (verb)
            {
                // Arity 0
                case "today":
                case "now":
                    return 0;

                // Arity 1
                case "upper": case "lower": case "trim": case "trimLeft": case "trimRight":
                case "coerceString": case "coerceNumber": case "coerceInteger": case "coerceBoolean":
                case "coerceDate": case "coerceTimestamp": case "tryCoerce":
                case "toArray": case "toObject":
                case "not": case "isNull": case "isString": case "isNumber": case "isBoolean":
                case "isArray": case "isObject": case "isDate": case "typeOf":
                case "capitalize": case "titleCase": case "length": case "reverseString":
                case "camelCase": case "snakeCase": case "kebabCase": case "pascalCase":
                case "slugify": case "normalizeSpace": case "stripAccents": case "clean":
                case "wordCount": case "soundex":
                case "abs": case "floor": case "ceil": case "negate": case "sign": case "trunc":
                case "isFinite": case "isNaN": case "ln": case "log10": case "exp": case "sqrt":
                case "formatInteger": case "formatCurrency":
                case "startOfDay": case "endOfDay": case "startOfMonth": case "endOfMonth":
                case "startOfYear": case "endOfYear": case "dayOfWeek": case "weekOfYear":
                case "quarter": case "isLeapYear": case "toUnix": case "fromUnix":
                case "base64Encode": case "base64Decode": case "urlEncode": case "urlDecode":
                case "jsonEncode": case "jsonDecode": case "hexEncode": case "hexDecode":
                case "sha256": case "md5": case "sha1": case "sha512": case "crc32":
                case "nextBusinessDay": case "formatDuration":
                case "flatten": case "distinct": case "sort": case "sortDesc": case "reverse":
                case "compact": case "unique": case "cumsum": case "cumprod":
                case "sum": case "count": case "min": case "max": case "avg": case "first": case "last":
                case "std": case "stdSample": case "variance": case "varianceSample":
                case "median": case "mode": case "rowNumber":
                case "uuid": case "sequence": case "resetSequence":
                case "keys": case "values": case "entries":
                case "toRadians": case "toDegrees":
                    return 1;

                // Arity 2
                case "ifNull": case "ifEmpty":
                case "and": case "or": case "xor": case "eq": case "ne": case "lt": case "lte": case "gt": case "gte":
                case "contains": case "startsWith": case "endsWith": case "truncate": case "join":
                case "mask": case "match": case "leftOf": case "rightOf": case "repeat":
                case "matches": case "levenshtein": case "tokenize":
                case "add": case "subtract": case "multiply": case "divide": case "mod":
                case "formatNumber": case "pow": case "log": case "formatPercent": case "parseInt":
                case "formatLocaleNumber": case "round":
                case "formatDate": case "parseDate": case "addDays": case "addMonths": case "addYears":
                case "addHours": case "addMinutes": case "addSeconds": case "formatTime":
                case "formatTimestamp": case "parseTimestamp": case "isBefore": case "isAfter":
                case "daysBetweenDates": case "ageFromDate": case "isValidDate":
                case "formatLocaleDate":
                case "formatPhone": case "movingAvg": case "businessDays":
                case "accumulate": case "set":
                case "percentile": case "quantile": case "covariance": case "correlation":
                case "weightedAvg": case "npv": case "irr": case "zscore":
                case "sortBy": case "map": case "indexOf": case "at": case "includes": case "concatArrays":
                case "zip": case "groupBy": case "take": case "drop": case "chunk": case "pluck":
                case "dedupe": case "diff": case "pctChange": case "limit":
                case "nanoid":
                case "has": case "merge": case "jsonPath":
                case "assert":
                    return 2;

                // Arity 3
                case "ifElse": case "between":
                case "substring": case "replace": case "replaceRegex": case "padLeft": case "padRight":
                case "pad": case "split": case "extract": case "wrap": case "center":
                case "clamp": case "random": case "safeDivide":
                case "dateDiff": case "isBetween":
                case "reduce": case "pivot": case "unpivot": case "convertUnit":
                case "compound": case "discount": case "pmt": case "fv": case "pv": case "depreciation":
                case "slice": case "range": case "shift": case "rank": case "lag": case "lead":
                case "sample": case "fillMissing":
                case "get":
                    return 3;

                // Arity 4
                case "rate": case "nper":
                case "filter": case "every": case "some": case "find": case "findIndex": case "partition":
                case "bearing": case "midpoint":
                    return 4;

                // Arity 5
                case "distance": case "interpolate":
                    return 5;

                // Arity 6
                case "inBoundingBox":
                    return 6;

                // Variadic (including unknown verbs)
                default:
                    return -1;
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Transform Expression Parser
        // ─────────────────────────────────────────────────────────────────────

        private static (FieldExpression Expr, List<OdinDirective> Dirs) ParseStringExpressionWithDirectives(string raw)
        {
            var trimmed = raw.Trim();

            if (trimmed.StartsWith("%", StringComparison.Ordinal))
            {
                var (expr, consumed) = ParseVerbExpression(trimmed);
                var remaining = consumed < trimmed.Length ? trimmed.Substring(consumed) : "";
                var dirs = ParseRemainingDirectives(remaining);
                return (expr, dirs);
            }

            if (trimmed.StartsWith("@", StringComparison.Ordinal))
            {
                var afterAt = trimmed.Substring(1);
                var path = ExtractPathToken(afterAt);
                var pathEnd = 1 + path.Length;
                var remaining = pathEnd < trimmed.Length ? trimmed.Substring(pathEnd) : "";
                var dirs = ParseRemainingDirectives(remaining);
                return (FieldExpression.Copy(path), dirs);
            }

            // Literal string
            return (FieldExpression.Literal(new OdinString(raw)), new List<OdinDirective>());
        }

        private static List<OdinDirective> ParseRemainingDirectives(string s)
        {
            var dirs = new List<OdinDirective>();
            var trimmed = s.Trim();
            if (trimmed.Length == 0) return dirs;

            int pos = 0;
            while (pos < trimmed.Length)
            {
                // Skip whitespace
                while (pos < trimmed.Length && char.IsWhiteSpace(trimmed[pos])) pos++;
                if (pos >= trimmed.Length || trimmed[pos] != ':') break;

                var (dir, consumed) = ParseExtractionDirective(trimmed.Substring(pos));
                if (dir != null)
                {
                    dirs.Add(dir);
                    pos += consumed;
                }
                else
                {
                    break;
                }
            }
            return dirs;
        }

        private static (FieldExpression Expr, int Consumed) ParseVerbExpression(string raw)
        {
            bool isCustom = raw.StartsWith("%&", StringComparison.Ordinal);
            int start = isCustom ? 2 : 1;

            int verbEnd = raw.Length;
            for (int i = start; i < raw.Length; i++)
            {
                if (char.IsWhiteSpace(raw[i]))
                {
                    verbEnd = i;
                    break;
                }
            }
            var verb = raw.Substring(start, verbEnd - start);

            if (verb.Length == 0)
                return (FieldExpression.Literal(new OdinString(raw)), raw.Length);

            int arity = GetVerbArity(verb);
            var argsStr = verbEnd < raw.Length ? raw.Substring(verbEnd) : "";
            var (args, argsConsumed) = ParseExpressionArgs(argsStr, arity);

            var verbCall = new VerbCall { Verb = verb, IsCustom = isCustom, Args = args };
            return (FieldExpression.Transform(verbCall), verbEnd + argsConsumed);
        }

        private static (VerbArg Arg, int Consumed) ParseVerbArgExpression(string raw)
        {
            bool isCustom = raw.StartsWith("%&", StringComparison.Ordinal);
            int start = isCustom ? 2 : 1;

            int verbEnd = raw.Length;
            for (int i = start; i < raw.Length; i++)
            {
                if (char.IsWhiteSpace(raw[i]))
                {
                    verbEnd = i;
                    break;
                }
            }
            var verb = raw.Substring(start, verbEnd - start);

            if (verb.Length == 0)
                return (VerbArg.Lit(new OdinString(raw)), raw.Length);

            int arity = GetVerbArity(verb);
            var argsStr = verbEnd < raw.Length ? raw.Substring(verbEnd) : "";
            var (args, argsConsumed) = ParseExpressionArgs(argsStr, arity);

            var verbCall = new VerbCall { Verb = verb, IsCustom = isCustom, Args = args };
            return (VerbArg.NestedCall(verbCall), verbEnd + argsConsumed);
        }

        private static (List<VerbArg> Args, int Consumed) ParseExpressionArgs(string argsStr, int limit)
        {
            var args = new List<VerbArg>();
            int pos = 0;

            // Skip leading whitespace
            while (pos < argsStr.Length && char.IsWhiteSpace(argsStr[pos])) pos++;

            while (pos < argsStr.Length)
            {
                if (limit >= 0 && args.Count >= limit) break;
                if (argsStr[pos] == ':') break;

                if (argsStr[pos] == '%')
                {
                    // Nested verb
                    var (arg, consumed) = ParseVerbArgExpression(argsStr.Substring(pos));
                    args.Add(arg);
                    pos += consumed;
                }
                else if (argsStr[pos] == '@')
                {
                    // Reference
                    int pathStart = pos + 1;
                    int pathEnd = FindTokenEnd(argsStr, pathStart) ;
                    var path = argsStr.Substring(pathStart, pathEnd - pathStart);
                    pos = pathEnd;

                    // Skip whitespace before potential directives
                    while (pos < argsStr.Length && char.IsWhiteSpace(argsStr[pos])) pos++;

                    // Collect extraction directives
                    var refDirectives = new List<OdinDirective>();
                    while (pos < argsStr.Length && argsStr[pos] == ':')
                    {
                        var (dir, consumed) = ParseExtractionDirective(argsStr.Substring(pos));
                        if (dir != null)
                        {
                            refDirectives.Add(dir);
                            pos += consumed;
                            while (pos < argsStr.Length && char.IsWhiteSpace(argsStr[pos])) pos++;
                        }
                        else
                        {
                            break;
                        }
                    }

                    args.Add(VerbArg.Ref(path, refDirectives));
                }
                else if (argsStr[pos] == '"')
                {
                    // Quoted string literal
                    var (s, consumed) = ParseQuotedStringArg(argsStr.Substring(pos));
                    args.Add(VerbArg.Lit(new OdinString(s)));
                    pos += consumed;
                }
                else if (pos + 1 < argsStr.Length && argsStr[pos] == '#' && argsStr[pos + 1] == '$')
                {
                    // Currency: #$99.99
                    int numStart = pos + 2;
                    int numEnd = FindNumberEnd(argsStr, numStart);
                    var numStr = argsStr.Substring(numStart, numEnd - numStart);
                    if (double.TryParse(numStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                    {
                        int dotIdx = numStr.IndexOf('.');
                        byte dp = dotIdx >= 0 ? (byte)(numStr.Length - dotIdx - 1) : (byte)2;
                        args.Add(VerbArg.Lit(new OdinCurrency(v) { DecimalPlaces = dp, Raw = numStr }));
                    }
                    pos = numEnd;
                }
                else if (pos + 1 < argsStr.Length && argsStr[pos] == '#' && argsStr[pos + 1] == '#')
                {
                    // Integer: ##42
                    int numStart = pos + 2;
                    int numEnd = FindNumberEnd(argsStr, numStart);
                    var raw = argsStr.Substring(numStart, numEnd - numStart);
                    long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var val);
                    args.Add(VerbArg.Lit(new OdinInteger(val)));
                    pos = numEnd;
                }
                else if (argsStr[pos] == '#')
                {
                    // Number: #3.14
                    int numStart = pos + 1;
                    int numEnd = FindNumberEnd(argsStr, numStart);
                    var raw = argsStr.Substring(numStart, numEnd - numStart);
                    if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                    {
                        int dotIdx = raw.IndexOf('.');
                        byte? dp = dotIdx >= 0 ? (byte?)(raw.Length - dotIdx - 1) : null;
                        args.Add(VerbArg.Lit(new OdinNumber(v) { DecimalPlaces = dp, Raw = raw }));
                    }
                    pos = numEnd;
                }
                else if (argsStr[pos] == '~')
                {
                    args.Add(VerbArg.Lit(new OdinNull()));
                    pos += 1;
                }
                else if (pos + 4 <= argsStr.Length && argsStr.Substring(pos, 4) == "true"
                         && (pos + 4 >= argsStr.Length || char.IsWhiteSpace(argsStr[pos + 4])))
                {
                    args.Add(VerbArg.Lit(new OdinBoolean(true)));
                    pos += 4;
                }
                else if (pos + 5 <= argsStr.Length && argsStr.Substring(pos, 5) == "false"
                         && (pos + 5 >= argsStr.Length || char.IsWhiteSpace(argsStr[pos + 5])))
                {
                    args.Add(VerbArg.Lit(new OdinBoolean(false)));
                    pos += 5;
                }
                else
                {
                    // Unquoted string (table name, field name, etc.)
                    int end = FindTokenEnd(argsStr, pos);
                    var val = argsStr.Substring(pos, end - pos);
                    args.Add(VerbArg.Lit(new OdinString(val)));
                    pos = end;
                }

                // Skip whitespace
                while (pos < argsStr.Length && char.IsWhiteSpace(argsStr[pos])) pos++;
            }

            return (args, pos);
        }

        private static string ExtractPathToken(string s)
        {
            int end = FindTokenEnd(s, 0);
            return s.Substring(0, end);
        }

        private static (OdinDirective? Dir, int Consumed) ParseExtractionDirective(string s)
        {
            if (s.Length == 0 || s[0] != ':') return (null, 0);

            // Get directive name
            int nameStart = 1;
            int nameEnd = s.Length;
            for (int i = nameStart; i < s.Length; i++)
            {
                if (char.IsWhiteSpace(s[i]))
                {
                    nameEnd = i;
                    break;
                }
            }
            var name = s.Substring(nameStart, nameEnd - nameStart);

            // Only consume recognized directives
            bool recognized;
            switch (name)
            {
                case "pos": case "len": case "field": case "trim": case "type":
                case "date": case "time": case "duration": case "timestamp":
                case "boolean": case "integer": case "number":
                case "currency": case "reference": case "binary": case "percent":
                case "decimals": case "currencyCode":
                case "leftPad": case "rightPad": case "truncate": case "default":
                case "upper": case "lower":
                case "required": case "confidential": case "deprecated": case "attr":
                    recognized = true;
                    break;
                default:
                    recognized = false;
                    break;
            }

            if (!recognized) return (null, 0);

            int consumed = nameEnd;

            // Check for a value after the directive name
            bool needsValue;
            switch (name)
            {
                case "pos": case "len": case "field": case "type": case "decimals":
                case "currencyCode": case "leftPad": case "rightPad": case "default":
                    needsValue = true;
                    break;
                default:
                    needsValue = false;
                    break;
            }

            DirectiveValue? value = null;
            if (needsValue)
            {
                // Skip whitespace
                while (consumed < s.Length && char.IsWhiteSpace(s[consumed])) consumed++;

                if (consumed < s.Length)
                {
                    if (s[consumed] == '"')
                    {
                        var (qstr, qconsumed) = ParseQuotedStringArg(s.Substring(consumed));
                        consumed += qconsumed;
                        value = DirectiveValue.FromString(qstr);
                    }
                    else
                    {
                        int valEnd = s.Length;
                        for (int i = consumed; i < s.Length; i++)
                        {
                            if (char.IsWhiteSpace(s[i]))
                            {
                                valEnd = i;
                                break;
                            }
                        }
                        var valStr = s.Substring(consumed, valEnd - consumed);
                        consumed = valEnd;

                        if (double.TryParse(valStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var n))
                            value = DirectiveValue.FromNumber(n);
                        else
                            value = DirectiveValue.FromString(valStr);
                    }
                }
            }

            return (new OdinDirective(name, value), consumed);
        }

        private static int FindTokenEnd(string s, int start)
        {
            for (int i = start; i < s.Length; i++)
            {
                if (char.IsWhiteSpace(s[i])) return i;
            }
            return s.Length;
        }

        private static int FindNumberEnd(string s, int start)
        {
            int i = start;
            // Allow leading minus
            if (i < s.Length && s[i] == '-') i++;
            while (i < s.Length)
            {
                char c = s[i];
                if (c >= '0' && c <= '9' || c == '.') { i++; continue; }
                if ((c == 'e' || c == 'E' || c == '+' || c == '-') && i > start) { i++; continue; }
                break;
            }
            return i == start ? Math.Min(s.Length, start + 1) : i;
        }

        private static (string Value, int Consumed) ParseQuotedStringArg(string s)
        {
            if (s.Length == 0 || s[0] != '"') return ("", 0);
            var result = new System.Text.StringBuilder();
            int i = 1; // skip opening quote
            while (i < s.Length)
            {
                if (s[i] == '\\' && i + 1 < s.Length)
                {
                    switch (s[i + 1])
                    {
                        case '"': result.Append('"'); i += 2; break;
                        case '\\': result.Append('\\'); i += 2; break;
                        case 'n': result.Append('\n'); i += 2; break;
                        case 't': result.Append('\t'); i += 2; break;
                        case 'r': result.Append('\r'); i += 2; break;
                        default: result.Append(s[i]); i++; break;
                    }
                }
                else if (s[i] == '"')
                {
                    i++; // skip closing quote
                    break;
                }
                else
                {
                    result.Append(s[i]);
                    i++;
                }
            }
            return (result.ToString(), i);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Field Expression Conversion
        // ─────────────────────────────────────────────────────────────────────

        private static (FieldExpression Expr, List<OdinDirective> Dirs) ValueToFieldExpressionWithDirectives(OdinValue value)
        {
            switch (value)
            {
                case OdinReference refVal:
                    return (FieldExpression.Copy(refVal.Path), new List<OdinDirective>());

                case OdinVerb verbVal:
                {
                    if (verbVal.Args.Count == 0 && verbVal.Name.StartsWith("%", StringComparison.Ordinal))
                    {
                        // Bare verb expression — re-parse
                        return ParseStringExpressionWithDirectives(verbVal.Name);
                    }

                    var verbCall = new VerbCall
                    {
                        Verb = verbVal.Name,
                        IsCustom = verbVal.IsCustom,
                        Args = new List<VerbArg>(),
                    };
                    foreach (var arg in verbVal.Args)
                        verbCall.Args.Add(OdinValueToVerbArg(arg));

                    return (FieldExpression.Transform(verbCall), new List<OdinDirective>());
                }

                case OdinObject objVal:
                {
                    var fieldMappings = new List<FieldMapping>();
                    foreach (var kvp in objVal.Fields)
                    {
                        OdinModifiers? mods = kvp.Value.Modifiers;
                        var dirs = new List<OdinDirective>();
                        if (kvp.Value.Directives != null)
                            foreach (var d in kvp.Value.Directives) dirs.Add(d);
                        var (innerExpr, _) = ValueToFieldExpressionWithDirectives(kvp.Value);
                        fieldMappings.Add(new FieldMapping
                        {
                            Target = kvp.Key,
                            Expression = innerExpr,
                            Directives = dirs,
                            Modifiers = mods,
                        });
                    }
                    return (FieldExpression.Object(fieldMappings), new List<OdinDirective>());
                }

                case OdinString strVal:
                {
                    var trimmed = strVal.Value.Trim();
                    if (trimmed.StartsWith("@", StringComparison.Ordinal))
                        return ParseStringExpressionWithDirectives(trimmed);
                    if (trimmed.StartsWith("%", StringComparison.Ordinal))
                        return ParseStringExpressionWithDirectives(trimmed);
                    if (trimmed.StartsWith("$const.", StringComparison.Ordinal)
                        || trimmed.StartsWith("$constants.", StringComparison.Ordinal))
                        return (FieldExpression.Copy(trimmed), new List<OdinDirective>());
                    return (FieldExpression.Literal(value), new List<OdinDirective>());
                }

                default:
                    return (FieldExpression.Literal(value), new List<OdinDirective>());
            }
        }

        private static readonly string[] FormattingDirectiveNames = new[]
        {
            "pos", "len", "leftPad", "rightPad", "truncate", "default", "upper", "lower"
        };

        private static List<OdinDirective> CollectFormattingDirectives(FieldExpression expr)
        {
            var collected = new List<OdinDirective>();
            if (expr is TransformExpression txExpr)
            {
                CollectFromVerbArgs(txExpr.Call.Args, collected);
            }
            return collected;
        }

        private static void CollectFromVerbArgs(List<VerbArg> args, List<OdinDirective> collected)
        {
            foreach (var arg in args)
            {
                if (arg is ReferenceArg refArg)
                {
                    foreach (var dir in refArg.Directives)
                    {
                        bool isFormatting = false;
                        foreach (var name in FormattingDirectiveNames)
                        {
                            if (dir.Name == name) { isFormatting = true; break; }
                        }
                        if (!isFormatting) continue;

                        bool exists = false;
                        foreach (var d in collected)
                            if (d.Name == dir.Name) { exists = true; break; }
                        if (!exists) collected.Add(dir);
                    }
                }
                else if (arg is VerbCallArg vcArg)
                {
                    CollectFromVerbArgs(vcArg.NestedCall.Args, collected);
                }
            }
        }

        private static VerbArg OdinValueToVerbArg(OdinValue value)
        {
            switch (value)
            {
                case OdinReference refVal:
                {
                    var dirs = new List<OdinDirective>();
                    if (refVal.Directives != null)
                        foreach (var d in refVal.Directives) dirs.Add(d);
                    return VerbArg.Ref(refVal.Path, dirs);
                }
                case OdinVerb verbVal:
                {
                    var args = new List<VerbArg>();
                    foreach (var arg in verbVal.Args)
                        args.Add(OdinValueToVerbArg(arg));
                    var vc = new VerbCall { Verb = verbVal.Name, IsCustom = verbVal.IsCustom, Args = args };
                    return VerbArg.NestedCall(vc);
                }
                default:
                    return VerbArg.Lit(value);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Pass Collection
        // ─────────────────────────────────────────────────────────────────────

        private static List<int> CollectPasses(List<TransformSegment> segments)
        {
            var passes = new List<int>();
            CollectPassesRecursive(segments, passes);
            passes.Sort();
            // Deduplicate
            var deduped = new List<int>();
            int prev = -1;
            foreach (var p in passes)
            {
                if (p != prev)
                {
                    deduped.Add(p);
                    prev = p;
                }
            }
            return deduped;
        }

        private static void CollectPassesRecursive(List<TransformSegment> segments, List<int> passes)
        {
            foreach (var seg in segments)
            {
                if (seg.Pass.HasValue)
                    passes.Add(seg.Pass.Value);
                CollectPassesRecursive(seg.Children, passes);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────────────────────────────

        private static string? GetMetaString(OdinDocument doc, string key)
        {
            if (!doc.Metadata.TryGetValue(key, out var value)) return null;
            return OdinValueToString(value);
        }

        /// <summary>
        /// Convert any <see cref="OdinValue"/> to a plain string representation.
        /// </summary>
        internal static string OdinValueToString(OdinValue value)
        {
            switch (value)
            {
                case OdinString s: return s.Value;
                case OdinTime t: return t.Value;
                case OdinDuration d: return d.Value;
                case OdinBoolean b: return b.Value ? "true" : "false";
                case OdinInteger i: return i.Raw ?? i.Value.ToString(CultureInfo.InvariantCulture);
                case OdinNumber n: return n.Raw ?? n.Value.ToString(CultureInfo.InvariantCulture);
                case OdinCurrency c: return c.Raw ?? c.Value.ToString(CultureInfo.InvariantCulture);
                case OdinPercent p: return p.Raw ?? p.Value.ToString(CultureInfo.InvariantCulture);
                case OdinNull _: return "~";
                case OdinReference r: return "@" + r.Path;
                case OdinDate d: return d.Raw;
                case OdinTimestamp ts: return ts.Raw;
                case OdinBinary _: return "<binary>";
                case OdinVerb v: return "%" + v.Name;
                case OdinArray a: return "[" + a.Items.Count + " items]";
                case OdinObject o: return "{" + o.Fields.Count + " fields}";
                default: return value.ToString() ?? "";
            }
        }

        /// <summary>
        /// Convert an <see cref="OdinValue"/> to a <see cref="DynValue"/> for table storage.
        /// </summary>
        private static DynValue OdinValueToDynForTable(OdinValue val)
        {
            switch (val)
            {
                case OdinNull _: return DynValue.Null();
                case OdinBoolean b: return DynValue.Bool(b.Value);
                case OdinString s: return DynValue.String(s.Value);
                case OdinTime t: return DynValue.String(t.Value);
                case OdinDuration d: return DynValue.String(d.Value);
                case OdinInteger i: return DynValue.Integer(i.Value);
                case OdinNumber n: return DynValue.Float(n.Value);
                case OdinCurrency c: return DynValue.Float((double)c.Value);
                case OdinPercent p: return DynValue.Float(p.Value);
                case OdinDate d: return DynValue.String(d.Raw);
                case OdinTimestamp ts: return DynValue.String(ts.Raw);
                case OdinReference r: return DynValue.String(r.Path);
                default: return DynValue.String(OdinValueToString(val));
            }
        }
    }
}
