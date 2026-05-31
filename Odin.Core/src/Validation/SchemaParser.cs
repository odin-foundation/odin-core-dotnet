using System;
using System.Collections.Generic;
using System.Globalization;
using Odin.Core.Types;

namespace Odin.Core.Validation
{
    /// <summary>
    /// Parses ODIN schema text into an <see cref="OdinSchemaDefinition"/>.
    /// Supports header-based type definitions ({@TypeName}), standalone types (@TypeName),
    /// type inheritance (@Child : @Parent), field constraints, array specs,
    /// and object-level constraints.
    /// </summary>
    public static class SchemaParser
    {
        private static readonly char[] LineSeparators = { '\r', '\n' };
        /// <summary>
        /// Parse ODIN schema text into an <see cref="OdinSchemaDefinition"/>.
        /// </summary>
        /// <param name="input">The schema text to parse.</param>
        /// <returns>A parsed schema definition.</returns>
        /// <exception cref="OdinParseException">Thrown when the schema text is invalid.</exception>
        public static OdinSchemaDefinition Parse(string input)
        {
            if (input == null)
                throw new ArgumentNullException(nameof(input));
            // ArgumentNullException.ThrowIfNull not available on netstandard2.0

            var state = new SchemaParserState();
            state.Parse(input);
            return state.Build();
        }

        private enum ParserContext
        {
            None,
            Metadata,
            TypeDef,
            Section,
            ArrayDef,
        }

        private sealed class SchemaParserState
        {
            private readonly SchemaMetadata _metadata = new SchemaMetadata();
            private readonly List<SchemaImport> _imports = new List<SchemaImport>();
            private readonly Dictionary<string, SchemaType> _types = new Dictionary<string, SchemaType>();
            private readonly Dictionary<string, SchemaField> _fields = new Dictionary<string, SchemaField>();
            private readonly Dictionary<string, SchemaArray> _arrays = new Dictionary<string, SchemaArray>();
            private readonly Dictionary<string, List<SchemaObjectConstraint>> _constraints = new Dictionary<string, List<SchemaObjectConstraint>>();

            private ParserContext _currentContext = ParserContext.None;
            private string _currentTypeName = "";
            private List<string> _currentTypeParents = new List<string>();
            private List<SchemaField> _currentTypeFields = new List<SchemaField>();
            private string _currentSectionPath = "";
            private int? _currentArrayMin;
            private int? _currentArrayMax;
            private bool _currentArrayUnique;

            // Relative-header context: last absolute header, plus the sub-path of the
            // current relative header within its parent ({.term} -> "term").
            private string _previousHeaderPath = "";
            private string _previousHeaderType = "";
            private string _currentTypeSubPath = "";

            public void Parse(string input)
            {
                var lines = input.Split(LineSeparators, StringSplitOptions.None);
                int lineNum = 0;

                foreach (var line in lines)
                {
                    lineNum++;
                    var trimmed = line.Trim();
                    if (trimmed.Length == 0 || trimmed[0] == ';')
                        continue;

                    // Import directive
                    if (trimmed.StartsWith("@import", StringComparison.Ordinal))
                    {
                        ParseImport(trimmed);
                        continue;
                    }

                    // Header line: {$}, {section}, {@TypeName}, {path[]}
                    if (trimmed[0] == '{' && trimmed[trimmed.Length - 1] == '}')
                    {
                        FlushCurrentContext();
                        ParseHeader(trimmed);
                        continue;
                    }

                    // Standalone type definition: @TypeName or @TypeName : @Parent
                    if (trimmed[0] == '@' && trimmed.IndexOf('=') < 0)
                    {
                        FlushCurrentContext();
                        ParseStandaloneType(trimmed);
                        continue;
                    }

                    // Object-level constraint: :one_of(...), :exactly_one(...), :invariant ...
                    // Array-level constraint: :(min..max), :unique
                    if (trimmed[0] == ':')
                    {
                        if (_currentContext == ParserContext.ArrayDef)
                        {
                            ParseArrayConstraint(trimmed);
                        }
                        else
                        {
                            ParseObjectConstraint(trimmed);
                        }
                        continue;
                    }

                    // Assignment or field definition: key = value
                    int eqPos = trimmed.IndexOf('=');
                    if (eqPos >= 0)
                    {
                        var key = trimmed.Substring(0, eqPos).Trim();
                        var value = trimmed.Substring(eqPos + 1).Trim();

                        // Type composition: `= @a & @b` (empty key). Store as a _composition field.
                        if (key.Length == 0 && value.Length > 0 && value[0] == '@')
                        {
                            ParseCompositionLine(value);
                            continue;
                        }

                        // Type-level value definition: `= constraints` (empty key)
                        if (key.Length == 0 && _currentContext == ParserContext.TypeDef)
                        {
                            // Type-level constraints (e.g., = :(10..15) :pattern "^...") - skip for now
                            continue;
                        }

                        ParseAssignment(key, value, lineNum);
                    }
                }

                FlushCurrentContext();
            }

            public OdinSchemaDefinition Build()
            {
                return new OdinSchemaDefinition
                {
                    Metadata = _metadata,
                    Imports = new List<SchemaImport>(_imports),
                    Types = new Dictionary<string, SchemaType>(_types),
                    Fields = new Dictionary<string, SchemaField>(_fields),
                    Arrays = new Dictionary<string, SchemaArray>(_arrays),
                    Constraints = new Dictionary<string, List<SchemaObjectConstraint>>(_constraints),
                };
            }

            private void FlushCurrentContext()
            {
                if (_currentContext == ParserContext.TypeDef && _currentTypeName.Length > 0)
                {
                    var name = _currentTypeName;
                    var fields = _currentTypeFields;

                    if (_types.TryGetValue(name, out var existing))
                    {
                        foreach (var f in fields)
                            existing.SchemaFields.Add(f);
                    }
                    else
                    {
                        _types[name] = new SchemaType
                        {
                            Name = name,
                            SchemaFields = new List<SchemaField>(fields),
                            Parents = new List<string>(_currentTypeParents),
                        };
                    }

                    _currentTypeName = "";
                    _currentTypeFields = new List<SchemaField>();
                    _currentTypeParents = new List<string>();
                }

                if (_currentContext == ParserContext.ArrayDef && _currentSectionPath.Length > 0)
                {
                    _arrays[_currentSectionPath] = new SchemaArray
                    {
                        Name = _currentSectionPath,
                        MinItems = _currentArrayMin,
                        MaxItems = _currentArrayMax,
                        IsUnique = _currentArrayUnique,
                    };
                    _currentArrayMin = null;
                    _currentArrayMax = null;
                    _currentArrayUnique = false;
                }

                _currentContext = ParserContext.None;
            }

            private void ParseImport(string line)
            {
                var rest = line.Substring("@import".Length).Trim();
                string path;
                string? alias = null;

                if (rest.Length > 0 && rest[0] == '"')
                {
                    int endQuote = rest.IndexOf('"', 1);
                    if (endQuote > 0)
                    {
                        path = rest.Substring(1, endQuote - 1);
                        var after = rest.Substring(endQuote + 1).Trim();
                        if (after.StartsWith("as", StringComparison.Ordinal))
                            alias = after.Substring(2).Trim();
                    }
                    else
                    {
                        path = rest.Trim('"');
                    }
                }
                else
                {
                    path = rest;
                }

                _imports.Add(new SchemaImport { Path = path, Alias = alias });
            }

            private void ParseHeader(string line)
            {
                var inner = line.Substring(1, line.Length - 2).Trim();

                if (inner == "$")
                {
                    _currentContext = ParserContext.Metadata;
                    _currentSectionPath = "";
                    _currentTypeSubPath = "";
                    return;
                }

                // Relative header ({.sub}): nest under the last absolute context.
                if (inner.Length > 0 && inner[0] == '.')
                {
                    var sub = inner.Substring(1).Trim();
                    if (_previousHeaderType.Length > 0)
                    {
                        // Re-open the parent type; fields route under the sub-path.
                        _currentContext = ParserContext.TypeDef;
                        _currentTypeName = _previousHeaderType;
                        _currentTypeFields = new List<SchemaField>();
                        _currentTypeParents = new List<string>();
                        _currentTypeSubPath = sub;
                    }
                    else
                    {
                        // Object context: relative headers nest under the last absolute path.
                        _currentContext = ParserContext.Section;
                        _currentSectionPath = _previousHeaderPath.Length > 0
                            ? _previousHeaderPath + "." + sub
                            : sub;
                        _currentTypeSubPath = "";
                    }
                    return;
                }

                _currentTypeSubPath = "";

                if (inner.Length > 0 && inner[0] == '@')
                {
                    // Type definition: {@TypeName}
                    var typeName = inner.Substring(1).Trim();
                    _currentContext = ParserContext.TypeDef;
                    _currentTypeName = typeName;
                    _currentTypeFields = new List<SchemaField>();
                    _currentTypeParents = new List<string>();
                    _previousHeaderType = typeName;
                    _previousHeaderPath = "";
                    return;
                }

                if (inner.EndsWith("[]", StringComparison.Ordinal))
                {
                    var path = inner.Substring(0, inner.Length - 2).Trim();
                    _currentContext = ParserContext.ArrayDef;
                    _currentSectionPath = path;
                    _previousHeaderPath = path;
                    _previousHeaderType = "";
                    return;
                }

                // Regular section: {path}
                _currentContext = ParserContext.Section;
                _currentSectionPath = inner;
                _previousHeaderPath = inner;
                _previousHeaderType = "";
            }

            private void ParseStandaloneType(string line)
            {
                var rest = line.Substring(1); // strip leading @

                string typeName;
                var parents = new List<string>();

                int colonPos = rest.IndexOf(" : ", StringComparison.Ordinal);
                if (colonPos >= 0)
                {
                    typeName = rest.Substring(0, colonPos).Trim();
                    var parentsStr = rest.Substring(colonPos + 3).Trim();
                    foreach (var part in parentsStr.Split('&'))
                    {
                        var p = part.Trim();
                        if (p.Length > 0)
                            parents.Add(p);
                    }
                }
                else
                {
                    typeName = rest.Trim();
                }

                _currentContext = ParserContext.TypeDef;
                _currentTypeName = typeName;
                _currentTypeParents = parents;
                _currentTypeFields = new List<SchemaField>();
            }

            private void ParseCompositionLine(string value)
            {
                var members = new List<string>();
                foreach (var part in value.Split('&'))
                {
                    var p = part.Trim();
                    if (p.Length > 0 && p[0] == '@')
                        p = p.Substring(1).Trim();
                    // Drop a trailing :override or other directive.
                    int colon = p.IndexOf(':');
                    if (colon >= 0) p = p.Substring(0, colon).Trim();
                    if (p.Length > 0)
                        members.Add(p);
                }
                if (members.Count == 0)
                    return;

                var compField = new SchemaField
                {
                    Name = "_composition",
                    FieldType = SchemaFieldType.TypeRef(string.Join("&", members)),
                };

                if (_currentContext == ParserContext.TypeDef)
                {
                    _currentTypeFields.Add(compField);
                }
                else
                {
                    var path = _currentSectionPath.Length > 0
                        ? _currentSectionPath + "._composition"
                        : "_composition";
                    _fields[path] = compField;
                }
            }

            private void ParseObjectConstraint(string line)
            {
                var path = _currentSectionPath;

                if (line.StartsWith(":invariant", StringComparison.Ordinal))
                {
                    var expr = line.Substring(":invariant".Length).Trim();
                    if (!_constraints.ContainsKey(path))
                        _constraints[path] = new List<SchemaObjectConstraint>();
                    _constraints[path].Add(SchemaObjectConstraint.Invariant(expr));
                    return;
                }

                int? min = null;
                int? max = null;
                string rest;

                if (line.StartsWith(":exactly_one", StringComparison.Ordinal))
                {
                    min = 1; max = 1;
                    rest = line.Substring(":exactly_one".Length).Trim();
                }
                else if (line.StartsWith(":at_most_one", StringComparison.Ordinal))
                {
                    max = 1;
                    rest = line.Substring(":at_most_one".Length).Trim();
                }
                else if (line.StartsWith(":one_of", StringComparison.Ordinal))
                {
                    min = 1;
                    rest = line.Substring(":one_of".Length).Trim();
                }
                else if (line.StartsWith(":of", StringComparison.Ordinal))
                {
                    rest = line.Substring(":of".Length).Trim();
                    if (rest.Length > 0 && rest[0] == '(')
                    {
                        int parenEnd = rest.IndexOf(')');
                        if (parenEnd >= 0)
                        {
                            var rangeStr = rest.Substring(1, parenEnd - 1);
                            ParseRange(rangeStr, out min, out max);
                            rest = rest.Substring(parenEnd + 1).Trim();
                        }
                    }
                }
                else
                {
                    return;
                }

                // Parse field list — could be (a, b, c) or a, b, c
                var fieldsStr = rest;
                if (fieldsStr.Length > 0 && fieldsStr[0] == '(')
                {
                    int parenEnd = fieldsStr.IndexOf(')');
                    if (parenEnd >= 0)
                        fieldsStr = fieldsStr.Substring(1, parenEnd - 1);
                }

                var fieldList = new List<string>();
                foreach (var part in fieldsStr.Split(','))
                {
                    var f = part.Trim();
                    if (f.Length > 0)
                        fieldList.Add(f);
                }

                if (fieldList.Count > 0)
                {
                    if (!_constraints.ContainsKey(path))
                        _constraints[path] = new List<SchemaObjectConstraint>();
                    _constraints[path].Add(SchemaObjectConstraint.Cardinality(fieldList, min, max));
                }
            }

            private void ParseArrayConstraint(string line)
            {
                if (line.StartsWith(":unique", StringComparison.Ordinal))
                {
                    _currentArrayUnique = true;
                    return;
                }

                // :(min..max) — array bounds
                if (line.Length > 1 && line[1] == '(')
                {
                    int parenEnd = line.IndexOf(')');
                    if (parenEnd >= 0)
                    {
                        var inner = line.Substring(2, parenEnd - 2);
                        if (inner.Contains(".."))
                        {
                            ParseRange(inner, out var min, out var max);
                            _currentArrayMin = min;
                            _currentArrayMax = max;
                        }
                    }
                    return;
                }

                // Also handle invariant/cardinality on arrays (delegate to object constraint)
                ParseObjectConstraint(line);
            }

            private void ParseAssignment(string key, string value, int lineNum)
            {
                switch (_currentContext)
                {
                    case ParserContext.Metadata:
                        var uv = Unquote(value);
                        if (key == "id") _metadata.Id = uv;
                        else if (key == "title") _metadata.Title = uv;
                        else if (key == "description") _metadata.Description = uv;
                        else if (key == "version") _metadata.Version = uv;
                        break;

                    case ParserContext.TypeDef:
                    {
                        var fieldName = key;
                        if (fieldName.EndsWith("[]", StringComparison.Ordinal))
                            fieldName = fieldName.Substring(0, fieldName.Length - 2).Trim();
                        // Inside a relative sub-section ({.term}), prefix the field name.
                        if (_currentTypeSubPath.Length > 0)
                            fieldName = _currentTypeSubPath + "." + fieldName;
                        var field = ParseFieldDef(fieldName, value);
                        _currentTypeFields.Add(field);
                        break;
                    }

                    case ParserContext.Section:
                    {
                        string fullPath;
                        var cleanKey = key;
                        if (cleanKey.EndsWith("[]", StringComparison.Ordinal))
                            cleanKey = cleanKey.Substring(0, cleanKey.Length - 2);

                        if (_currentSectionPath.Length == 0)
                            fullPath = key;
                        else if (key.EndsWith("[]", StringComparison.Ordinal))
                            fullPath = string.Concat(_currentSectionPath, ".", key.Substring(0, key.Length - 2));
                        else
                            fullPath = _currentSectionPath + "." + key;

                        var field = ParseFieldDef(cleanKey, value);
                        _fields[fullPath] = field;
                        break;
                    }

                    case ParserContext.ArrayDef:
                    {
                        var fullPath = _currentSectionPath + "[]." + key;
                        var field = ParseFieldDef(key, value);
                        _fields[fullPath] = field;
                        break;
                    }

                    default:
                    {
                        var field = ParseFieldDef(key, value);
                        _fields[key] = field;
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Parse a field definition from schema text value.
        /// </summary>
        internal static SchemaField ParseFieldDef(string name, string value)
        {
            bool required = false;
            bool confidential = false;
            bool deprecated = false;
            bool immutable = false;
            bool computed = false;
            bool nullable = false;
            var constraints = new List<SchemaConstraint>();
            var conditionals = new List<SchemaConditional>();
            var fieldType = SchemaFieldType.String();
            SchemaDefaultValue? typedDefault = null;

            name = name.Trim();
            value = value.Trim();

            // Strip inline comment
            int semiPos = FindUnquotedSemicolon(value);
            if (semiPos >= 0)
                value = value.Substring(0, semiPos).Trim();

            var rest = value;

            // Check for modifier prefix (! ~ * -)
            while (rest.Length > 0)
            {
                if (rest[0] == '!')
                {
                    required = true;
                    rest = rest.Substring(1).TrimStart();
                }
                else if (rest[0] == '~' && rest.Length > 1)
                {
                    nullable = true;
                    rest = rest.Substring(1).TrimStart();
                }
                else if (rest[0] == '*')
                {
                    confidential = true;
                    rest = rest.Substring(1).TrimStart();
                }
                else if (rest[0] == '-' && rest.Length > 1 && !char.IsDigit(rest[1]))
                {
                    deprecated = true;
                    rest = rest.Substring(1).TrimStart();
                }
                else
                {
                    break;
                }
            }

            // Detect type from prefix (the type token plus any glued :directive suffix)
            string defaultRaw = ""; // bare numeric default trailing a numeric prefix (e.g. ##3)
            if (rest.StartsWith("##", StringComparison.Ordinal))
            {
                fieldType = SchemaFieldType.Integer();
                rest = rest.Substring(2);
                defaultRaw = TakeNumericDefault(ref rest);
                rest = rest.TrimStart();
                if (defaultRaw.Length > 0 &&
                    double.TryParse(defaultRaw, NumberStyles.Float, CultureInfo.InvariantCulture, out var iv))
                    typedDefault = SchemaDefaultValue.Numeric("integer", iv);
            }
            else if (rest.StartsWith("#%", StringComparison.Ordinal))
            {
                fieldType = SchemaFieldType.Percent();
                rest = rest.Substring(2);
                defaultRaw = TakeNumericDefault(ref rest);
                rest = rest.TrimStart();
                if (defaultRaw.Length > 0 &&
                    double.TryParse(defaultRaw, NumberStyles.Float, CultureInfo.InvariantCulture, out var pv))
                    typedDefault = SchemaDefaultValue.Numeric("percent", pv);
            }
            else if (rest.StartsWith("#$", StringComparison.Ordinal))
            {
                fieldType = SchemaFieldType.Currency();
                rest = rest.Substring(2);
                defaultRaw = TakeNumericDefault(ref rest);
                rest = rest.TrimStart();
                if (defaultRaw.Length > 0 &&
                    double.TryParse(defaultRaw, NumberStyles.Float, CultureInfo.InvariantCulture, out var cv))
                    typedDefault = SchemaDefaultValue.Numeric("currency", cv);
            }
            else if (rest.Length > 0 && rest[0] == '#' && !rest.StartsWith("#(", StringComparison.Ordinal))
            {
                fieldType = SchemaFieldType.Number();
                rest = rest.Substring(1);
                defaultRaw = TakeNumericDefault(ref rest);
                rest = rest.TrimStart();
                if (defaultRaw.Length > 0 &&
                    double.TryParse(defaultRaw, NumberStyles.Float, CultureInfo.InvariantCulture, out var nv))
                    typedDefault = SchemaDefaultValue.Numeric("number", nv);
            }
            else if (rest.Length > 0 && rest[0] == '?')
            {
                fieldType = SchemaFieldType.Boolean();
                rest = rest.Substring(1).TrimStart();
            }
            else if (rest == "~")
            {
                fieldType = SchemaFieldType.Null();
                rest = "";
            }

            // Temporal type word, possibly with glued :directive suffix (e.g. timestamp:immutable).
            if (fieldType is StringFieldType && rest.Length > 0 && char.IsLetter(rest[0]))
            {
                int wEnd = 0;
                while (wEnd < rest.Length && char.IsLetter(rest[wEnd])) wEnd++;
                var word = rest.Substring(0, wEnd);
                var named = NamedType(word);
                // Only consume when the word is a standalone token or carries a glued :directive,
                // not when it is followed by a union pipe (handled by ApplyUnion).
                bool gluedDirective = wEnd < rest.Length && rest[wEnd] == ':';
                bool standalone = wEnd >= rest.Length || char.IsWhiteSpace(rest[wEnd]) || gluedDirective;
                if (named != null && standalone)
                {
                    fieldType = named;
                    rest = rest.Substring(wEnd);
                    // Apply glued :immutable/:computed directives.
                    while (rest.Length > 0 && rest[0] == ':')
                    {
                        var after = rest.Substring(1);
                        if (after.StartsWith("immutable", StringComparison.Ordinal))
                        {
                            immutable = true;
                            rest = after.Substring("immutable".Length);
                        }
                        else if (after.StartsWith("computed", StringComparison.Ordinal))
                        {
                            computed = true;
                            rest = after.Substring("computed".Length);
                        }
                        else
                        {
                            break;
                        }
                    }
                    rest = rest.TrimStart();
                }
            }

            // Union type: a trailing |member chain (e.g. #|~, date|timestamp).
            fieldType = ApplyUnion(fieldType, ref rest);

            // Check for @TypeRef
            if (rest.Length > 0 && rest[0] == '@')
            {
                int spaceIdx = rest.IndexOf(' ');
                var typeRef = spaceIdx >= 0 ? rest.Substring(0, spaceIdx) : rest;
                fieldType = SchemaFieldType.TypeRef(typeRef.Substring(1));
                rest = rest.Substring(typeRef.Length).TrimStart();
            }

            // Parse constraint directives
            var remaining = rest;
            while (true)
            {
                remaining = remaining.TrimStart();
                if (remaining.Length == 0)
                    break;

                if (remaining[0] == ':')
                {
                    var after = remaining.Substring(1);

                    if (after.StartsWith("required", StringComparison.Ordinal))
                    {
                        required = true;
                        remaining = after.Substring("required".Length).TrimStart();
                        continue;
                    }
                    if (after.StartsWith("optional", StringComparison.Ordinal))
                    {
                        required = false;
                        remaining = after.Substring("optional".Length).TrimStart();
                        continue;
                    }
                    if (after.StartsWith("format ", StringComparison.Ordinal))
                    {
                        var formatRest = after.Substring("format ".Length);
                        int spIdx = IndexOfWhitespace(formatRest);
                        var formatName = spIdx >= 0 ? formatRest.Substring(0, spIdx) : formatRest;
                        remaining = spIdx >= 0 ? formatRest.Substring(spIdx).TrimStart() : "";
                        constraints.Add(SchemaConstraint.Format(formatName));
                        continue;
                    }
                    if (after.StartsWith("pattern ", StringComparison.Ordinal))
                    {
                        var patRest = after.Substring("pattern ".Length);
                        var extracted = ExtractQuoted(patRest);
                        if (extracted != null)
                        {
                            constraints.Add(SchemaConstraint.Pattern(extracted.Value.Content));
                            remaining = patRest.Substring(extracted.Value.EndPos).TrimStart();
                            continue;
                        }
                    }
                    if (after.StartsWith("unique", StringComparison.Ordinal))
                    {
                        constraints.Add(SchemaConstraint.Unique());
                        remaining = after.Substring("unique".Length).TrimStart();
                        continue;
                    }
                    if (after.StartsWith("enum(", StringComparison.Ordinal) || after.StartsWith("enum (", StringComparison.Ordinal))
                    {
                        int start = after.StartsWith("enum(", StringComparison.Ordinal) ? 5 : 6;
                        int parenEnd = after.IndexOf(')', start);
                        if (parenEnd >= 0)
                        {
                            var inner = after.Substring(start, parenEnd - start);
                            var values = ParseEnumValues(inner);
                            constraints.Add(SchemaConstraint.Enum(values));
                            remaining = after.Substring(parenEnd + 1).TrimStart();
                            continue;
                        }
                    }
                    if (after.StartsWith("computed", StringComparison.Ordinal))
                    {
                        computed = true;
                        remaining = after.Substring("computed".Length).TrimStart();
                        continue;
                    }
                    if (after.StartsWith("immutable", StringComparison.Ordinal))
                    {
                        immutable = true;
                        remaining = after.Substring("immutable".Length).TrimStart();
                        continue;
                    }
                    if (after.StartsWith("deprecated", StringComparison.Ordinal))
                    {
                        deprecated = true;
                        remaining = after.Substring("deprecated".Length).TrimStart();
                        // Skip optional message
                        if (remaining.Length > 0 && remaining[0] == '"')
                        {
                            int endQ = remaining.IndexOf('"', 1);
                            if (endQ >= 0)
                                remaining = remaining.Substring(endQ + 1).TrimStart();
                        }
                        continue;
                    }
                    if (after.StartsWith("override", StringComparison.Ordinal))
                    {
                        remaining = after.Substring("override".Length).TrimStart();
                        continue;
                    }
                    if (after.StartsWith("if ", StringComparison.Ordinal) || after.StartsWith("unless ", StringComparison.Ordinal))
                    {
                        bool unless = after.StartsWith("unless ", StringComparison.Ordinal);
                        var condRest = unless ? after.Substring("unless ".Length).Trim() : after.Substring("if ".Length).Trim();

                        // Parse: field op value
                        string condField;
                        ConditionalOperator condOp = ConditionalOperator.Eq;
                        string condValStr = "";

                        // Find operator
                        int geIdx = condRest.IndexOf(">=", StringComparison.Ordinal);
                        int leIdx = condRest.IndexOf("<=", StringComparison.Ordinal);
                        int neIdx = condRest.IndexOf("!=", StringComparison.Ordinal);
                        int eqIdx = condRest.IndexOf(" = ", StringComparison.Ordinal);
                        int gtIdx = condRest.IndexOf(">", StringComparison.Ordinal);
                        int ltIdx = condRest.IndexOf("<", StringComparison.Ordinal);

                        if (geIdx >= 0) { condField = condRest.Substring(0, geIdx).Trim(); condOp = ConditionalOperator.Gte; condValStr = condRest.Substring(geIdx + 2).Trim(); }
                        else if (leIdx >= 0) { condField = condRest.Substring(0, leIdx).Trim(); condOp = ConditionalOperator.Lte; condValStr = condRest.Substring(leIdx + 2).Trim(); }
                        else if (neIdx >= 0) { condField = condRest.Substring(0, neIdx).Trim(); condOp = ConditionalOperator.NotEq; condValStr = condRest.Substring(neIdx + 2).Trim(); }
                        else if (eqIdx >= 0) { condField = condRest.Substring(0, eqIdx).Trim(); condOp = ConditionalOperator.Eq; condValStr = condRest.Substring(eqIdx + 3).Trim(); }
                        else if (gtIdx >= 0 && (leIdx < 0 && geIdx < 0)) { condField = condRest.Substring(0, gtIdx).Trim(); condOp = ConditionalOperator.Gt; condValStr = condRest.Substring(gtIdx + 1).Trim(); }
                        else if (ltIdx >= 0 && (leIdx < 0 && neIdx < 0)) { condField = condRest.Substring(0, ltIdx).Trim(); condOp = ConditionalOperator.Lt; condValStr = condRest.Substring(ltIdx + 1).Trim(); }
                        else { condField = condRest; }

                        condValStr = condValStr.Trim().Trim('"');

                        ConditionalValue condValue;
                        if (double.TryParse(condValStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var numCond))
                            condValue = ConditionalValue.FromNumber(numCond);
                        else if (condValStr == "true")
                            condValue = ConditionalValue.FromBool(true);
                        else if (condValStr == "false")
                            condValue = ConditionalValue.FromBool(false);
                        else
                            condValue = ConditionalValue.FromString(condValStr);

                        conditionals.Add(new SchemaConditional
                        {
                            Field = condField,
                            Operator = condOp,
                            CondValue = condValue,
                            Unless = unless,
                        });

                        remaining = "";
                        continue;
                    }
                    if (after.StartsWith("timestamp", StringComparison.Ordinal))
                    {
                        fieldType = SchemaFieldType.Timestamp();
                        remaining = after.Substring("timestamp".Length).TrimStart();
                        continue;
                    }
                    if (after.StartsWith("date", StringComparison.Ordinal))
                    {
                        fieldType = SchemaFieldType.Date();
                        remaining = after.Substring("date".Length).TrimStart();
                        continue;
                    }
                    if (after.StartsWith("time", StringComparison.Ordinal))
                    {
                        fieldType = SchemaFieldType.Time();
                        remaining = after.Substring("time".Length).TrimStart();
                        continue;
                    }
                    // Bounds/enum: :(...)
                    if (after.Length > 0 && after[0] == '(')
                    {
                        int parenEnd = after.IndexOf(')');
                        if (parenEnd >= 0)
                        {
                            var inner = after.Substring(1, parenEnd - 1);
                            if (inner.Contains(".."))
                            {
                                constraints.Add(ParseBoundsConstraint(inner));
                                remaining = after.Substring(parenEnd + 1).TrimStart();
                                // A typed default may trail the bounds (e.g. (1..5) ##3).
                                typedDefault ??= TakeTrailingDefault(fieldType, ref remaining);
                                continue;
                            }
                            else if (inner.IndexOf(',') >= 0)
                            {
                                // Enum
                                var values = ParseEnumValues(inner);
                                constraints.Add(SchemaConstraint.Enum(values));
                            }
                            else
                            {
                                // Single value — exact length
                                if (int.TryParse(inner.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n))
                                {
                                    constraints.Add(SchemaConstraint.Bounds(
                                        n.ToString(CultureInfo.InvariantCulture),
                                        n.ToString(CultureInfo.InvariantCulture)));
                                }
                            }
                            remaining = after.Substring(parenEnd + 1).TrimStart();
                            continue;
                        }
                    }
                    // Pattern: :/regex/
                    if (after.Length > 0 && after[0] == '/')
                    {
                        int endSlash = after.IndexOf('/', 1);
                        if (endSlash >= 0)
                        {
                            var pattern = after.Substring(1, endSlash - 1);
                            constraints.Add(SchemaConstraint.Pattern(pattern));
                            remaining = after.Substring(endSlash + 1).TrimStart();
                            continue;
                        }
                    }
                    // Unknown directive — skip
                    int nextSpace = IndexOfWhitespace(after);
                    remaining = nextSpace >= 0 ? after.Substring(nextSpace).TrimStart() : "";
                    continue;
                }

                // Bare parenthesized value: (a, b, c) as enum or (min..max) as bounds
                if (remaining.Length > 0 && remaining[0] == '(')
                {
                    int parenEnd = remaining.IndexOf(')');
                    if (parenEnd >= 0)
                    {
                        var inner = remaining.Substring(1, parenEnd - 1);
                        if (inner.Contains(".."))
                        {
                            constraints.Add(ParseBoundsConstraint(inner));
                            remaining = remaining.Substring(parenEnd + 1).TrimStart();
                            typedDefault ??= TakeTrailingDefault(fieldType, ref remaining);
                            continue;
                        }
                        else if (inner.IndexOf(',') >= 0)
                        {
                            var values = ParseEnumValues(inner);
                            constraints.Add(SchemaConstraint.Enum(values));
                            fieldType = SchemaFieldType.Enum(values);
                        }
                        remaining = remaining.Substring(parenEnd + 1).TrimStart();
                        continue;
                    }
                }

                // Non-directive remaining text — check for type name
                if (remaining.Length > 0)
                {
                    int spIdx = IndexOfWhitespace(remaining);
                    var word = spIdx >= 0 ? remaining.Substring(0, spIdx) : remaining;
                    word = word.Trim('"');
                    switch (word)
                    {
                        case "string": fieldType = SchemaFieldType.String(); break;
                        case "boolean": case "bool": fieldType = SchemaFieldType.Boolean(); break;
                        case "number": case "float": case "decimal": fieldType = SchemaFieldType.Number(); break;
                        case "integer": case "int": fieldType = SchemaFieldType.Integer(); break;
                        case "currency": fieldType = SchemaFieldType.Currency(); break;
                        case "date": fieldType = SchemaFieldType.Date(); break;
                        case "timestamp": fieldType = SchemaFieldType.Timestamp(); break;
                        case "time": fieldType = SchemaFieldType.Time(); break;
                        case "duration": fieldType = SchemaFieldType.Duration(); break;
                        case "percent": fieldType = SchemaFieldType.Percent(); break;
                        case "binary": fieldType = SchemaFieldType.Binary(); break;
                        case "null": fieldType = SchemaFieldType.Null(); break;
                    }
                }
                break;
            }

            return new SchemaField
            {
                Name = name,
                FieldType = fieldType,
                Required = required,
                Confidential = confidential,
                Deprecated = deprecated,
                Immutable = immutable,
                Computed = computed,
                Nullable = nullable,
                TypedDefault = typedDefault,
                Constraints = constraints,
                Conditionals = conditionals,
            };
        }

        /// <summary>
        /// Consume a leading bare numeric literal (the default trailing a numeric prefix,
        /// e.g. the "3" in "##3"), returning it and advancing <paramref name="rest"/>.
        /// </summary>
        private static string TakeNumericDefault(ref string rest)
        {
            int i = 0;
            while (i < rest.Length && (char.IsDigit(rest[i]) || rest[i] == '.' || rest[i] == '-' || rest[i] == '+'))
                i++;
            // Require at least one digit to count as a default; a leading ':' or '|' is not one.
            bool hasDigit = false;
            for (int j = 0; j < i; j++)
                if (char.IsDigit(rest[j])) { hasDigit = true; break; }
            if (!hasDigit) return "";
            var taken = rest.Substring(0, i);
            rest = rest.Substring(i);
            return taken;
        }

        /// <summary>
        /// Parse a trailing union chain. For a leading bare temporal word (date|timestamp)
        /// the first member is read from <paramref name="rest"/>; otherwise the prefix-parsed
        /// <paramref name="baseType"/> is the first member. Returns a union when |members follow.
        /// </summary>
        private static SchemaFieldType ApplyUnion(SchemaFieldType baseType, ref string rest)
        {
            var members = new List<SchemaFieldType>();
            var work = rest.TrimStart();

            // Leading bare temporal word as the first union member (no type prefix matched).
            if (baseType is StringFieldType)
            {
                int pipe = work.IndexOf('|');
                if (pipe > 0)
                {
                    var first = NamedType(work.Substring(0, pipe).Trim());
                    if (first != null)
                    {
                        members.Add(first);
                        work = work.Substring(pipe); // keep leading | for the member loop
                    }
                }
            }

            if (members.Count == 0)
            {
                if (work.Length == 0 || work[0] != '|')
                    return baseType; // no union
                members.Add(baseType);
            }

            // Consume |member tokens.
            while (work.Length > 0 && work[0] == '|')
            {
                work = work.Substring(1);
                SchemaFieldType? member;
                if (work.StartsWith("##", StringComparison.Ordinal)) { member = SchemaFieldType.Integer(); work = work.Substring(2); }
                else if (work.StartsWith("#%", StringComparison.Ordinal)) { member = SchemaFieldType.Percent(); work = work.Substring(2); }
                else if (work.StartsWith("#$", StringComparison.Ordinal)) { member = SchemaFieldType.Currency(); work = work.Substring(2); }
                else if (work.StartsWith("~", StringComparison.Ordinal)) { member = SchemaFieldType.Null(); work = work.Substring(1); }
                else if (work.StartsWith("?", StringComparison.Ordinal)) { member = SchemaFieldType.Boolean(); work = work.Substring(1); }
                else if (work.StartsWith("\"\"", StringComparison.Ordinal)) { member = SchemaFieldType.String(); work = work.Substring(2); }
                else if (work.StartsWith("#", StringComparison.Ordinal)) { member = SchemaFieldType.Number(); work = work.Substring(1); }
                else
                {
                    int end = 0;
                    while (end < work.Length && char.IsLetter(work[end])) end++;
                    member = NamedType(work.Substring(0, end));
                    if (member != null) work = work.Substring(end);
                }
                if (member == null) break;
                members.Add(member);
            }

            rest = work;
            return members.Count > 1 ? SchemaFieldType.Union(members) : baseType;
        }

        /// <summary>Map a bare type-name word to its <see cref="SchemaFieldType"/>, if recognized.</summary>
        private static SchemaFieldType? NamedType(string word) => word switch
        {
            "date" => SchemaFieldType.Date(),
            "timestamp" => SchemaFieldType.Timestamp(),
            "time" => SchemaFieldType.Time(),
            "duration" => SchemaFieldType.Duration(),
            _ => null,
        };

        /// <summary>
        /// Parse a bounds constraint from a "min..max" body, preserving temporal and
        /// decimal literals as strings (compared chronologically/numerically downstream).
        /// </summary>
        private static SchemaConstraint ParseBoundsConstraint(string inner)
        {
            int dotDot = inner.IndexOf("..", StringComparison.Ordinal);
            string? min = null, max = null;
            if (dotDot >= 0)
            {
                var left = inner.Substring(0, dotDot).Trim();
                var right = inner.Substring(dotDot + 2).Trim();
                if (left.Length > 0) min = left;
                if (right.Length > 0) max = right;
            }
            return SchemaConstraint.Bounds(min, max);
        }

        /// <summary>
        /// Consume a typed default trailing a bounds constraint (e.g. the "##3" in "(1..5) ##3"),
        /// tagging it to match the field's declared type. Advances <paramref name="remaining"/>.
        /// </summary>
        private static SchemaDefaultValue? TakeTrailingDefault(SchemaFieldType fieldType, ref string remaining)
        {
            var s = remaining.TrimStart();
            if (s.Length == 0) return null;

            string typeTag;
            string body;
            if (s.StartsWith("##", StringComparison.Ordinal)) { typeTag = "integer"; body = s.Substring(2); }
            else if (s.StartsWith("#%", StringComparison.Ordinal)) { typeTag = "percent"; body = s.Substring(2); }
            else if (s.StartsWith("#$", StringComparison.Ordinal)) { typeTag = "currency"; body = s.Substring(2); }
            else if (s.StartsWith("#", StringComparison.Ordinal)) { typeTag = "number"; body = s.Substring(1); }
            else if (s.StartsWith("?", StringComparison.Ordinal))
            {
                var rest2 = s.Substring(1);
                int e2 = 0; while (e2 < rest2.Length && char.IsLetter(rest2[e2])) e2++;
                var word = rest2.Substring(0, e2);
                remaining = rest2.Substring(e2).TrimStart();
                return SchemaDefaultValue.Boolean(word == "true");
            }
            else { body = s; typeTag = DefaultTypeTag(fieldType); }

            int i = 0;
            while (i < body.Length && (char.IsDigit(body[i]) || body[i] == '.' || body[i] == '-' || body[i] == '+')) i++;
            var numStr = body.Substring(0, i);
            if (numStr.Length > 0 &&
                double.TryParse(numStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var n))
            {
                remaining = body.Substring(i).TrimStart();
                return SchemaDefaultValue.Numeric(typeTag, n);
            }
            return null;
        }

        /// <summary>Map a field type to its default-value type tag.</summary>
        private static string DefaultTypeTag(SchemaFieldType t) => t switch
        {
            IntegerFieldType => "integer",
            CurrencyFieldType => "currency",
            PercentFieldType => "percent",
            _ => "number",
        };

        private static void ParseRange(string s, out int? min, out int? max)
        {
            min = null;
            max = null;
            int dotDot = s.IndexOf("..", StringComparison.Ordinal);
            if (dotDot < 0) return;

            var left = s.Substring(0, dotDot).Trim();
            var right = s.Substring(dotDot + 2).Trim();
            if (int.TryParse(left, NumberStyles.Integer, CultureInfo.InvariantCulture, out var minVal))
                min = minVal;
            if (int.TryParse(right, NumberStyles.Integer, CultureInfo.InvariantCulture, out var maxVal))
                max = maxVal;
        }

        private static string Unquote(string s)
        {
            s = s.Trim();
            if (s.Length >= 2 && s[0] == '"' && s[s.Length - 1] == '"')
                return s.Substring(1, s.Length - 2);
            return s;
        }

        private static int FindUnquotedSemicolon(string s)
        {
            bool inQuote = false;
            for (int i = 0; i < s.Length; i++)
            {
                var ch = s[i];
                if (ch == '"') inQuote = !inQuote;
                else if (ch == ';' && !inQuote) return i;
            }
            return -1;
        }

        private static int IndexOfWhitespace(string s)
        {
            for (int i = 0; i < s.Length; i++)
            {
                if (char.IsWhiteSpace(s[i]))
                    return i;
            }
            return -1;
        }

        private struct QuotedResult
        {
            public string Content;
            public int EndPos;
        }

        private static QuotedResult? ExtractQuoted(string s)
        {
            if (s.Length > 0 && s[0] == '"')
            {
                int endQ = s.IndexOf('"', 1);
                if (endQ >= 0)
                {
                    return new QuotedResult
                    {
                        Content = s.Substring(1, endQ - 1),
                        EndPos = endQ + 1,
                    };
                }
            }
            return null;
        }

        private static List<string> ParseEnumValues(string inner)
        {
            var values = new List<string>();
            foreach (var part in inner.Split(','))
            {
                var v = part.Trim().Trim('"');
                if (v.Length > 0)
                    values.Add(v);
            }
            return values;
        }
    }
}
