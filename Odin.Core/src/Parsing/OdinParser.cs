using System;
using System.Collections.Generic;
using System.Globalization;
using Odin.Core.Types;

namespace Odin.Core.Parsing
{
    /// <summary>
    /// Recursive descent parser that converts a token stream into an <see cref="OdinDocument"/>.
    /// Handles headers, metadata ({$}), assignments, modifiers, array contiguity validation,
    /// verb expression parsing, directive parsing, comment preservation, and multi-document parsing.
    /// </summary>
    public static class OdinParser
    {
        /// <summary>
        /// Parse ODIN source text into an <see cref="OdinDocument"/>.
        /// </summary>
        /// <param name="source">The ODIN source text.</param>
        /// <param name="options">Parse options.</param>
        /// <returns>The parsed document.</returns>
        /// <exception cref="OdinParseException">Thrown on parse errors (P001-P015).</exception>
        public static OdinDocument Parse(string source, ParseOptions? options = null)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            var opts = options ?? ParseOptions.Default;

            var tokens = Tokenizer.Tokenize(source, opts);
            return ParseTokens(tokens, source, opts);
        }

        /// <summary>
        /// Parse ODIN source text into multiple documents (for document chaining with --- separators).
        /// </summary>
        /// <param name="source">The ODIN source text.</param>
        /// <param name="options">Parse options.</param>
        /// <returns>A list of parsed documents.</returns>
        /// <exception cref="OdinParseException">Thrown on parse errors (P001-P015).</exception>
        public static List<OdinDocument> ParseMulti(string source, ParseOptions? options = null)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            var opts = options ?? ParseOptions.Default;

            var tokens = Tokenizer.Tokenize(source, opts);
            return ParseTokensMulti(tokens, source, opts);
        }

        /// <summary>
        /// Parse a token stream into an <see cref="OdinDocument"/>.
        /// </summary>
        /// <param name="tokens">The list of tokens from the tokenizer.</param>
        /// <param name="source">The original source text.</param>
        /// <param name="options">Parse options.</param>
        /// <returns>The parsed document.</returns>
        /// <exception cref="OdinParseException">Thrown on parse errors (P001-P015).</exception>
        public static OdinDocument ParseTokens(IReadOnlyList<Token> tokens, string source, ParseOptions options)
        {
            var state = new ParserState(tokens, options);
            var docs = ParseDocuments(state);
            if (docs.Count == 0)
                return OdinDocument.Empty();
            return docs[docs.Count - 1];
        }

        /// <summary>
        /// Parse a token stream into multiple documents (for document chaining).
        /// </summary>
        /// <param name="tokens">The list of tokens from the tokenizer.</param>
        /// <param name="source">The original source text.</param>
        /// <param name="options">Parse options.</param>
        /// <returns>A list of parsed documents.</returns>
        /// <exception cref="OdinParseException">Thrown on parse errors (P001-P015).</exception>
        public static List<OdinDocument> ParseTokensMulti(IReadOnlyList<Token> tokens, string source, ParseOptions options)
        {
            var state = new ParserState(tokens, options);
            return ParseDocuments(state);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Parser state
        // ─────────────────────────────────────────────────────────────────────

        private sealed class ParserState
        {
            public readonly IReadOnlyList<Token> Tokens;
            public readonly ParseOptions Options;
            public int Pos;
            public string? CurrentHeader;
            public string? PreviousAbsoluteHeader;

            public ParserState(IReadOnlyList<Token> tokens, ParseOptions options)
            {
                Tokens = tokens;
                Options = options;
                Pos = 0;
                CurrentHeader = null;
            }

            public bool IsAtEnd =>
                Pos >= Tokens.Count || Tokens[Pos].TokenType == TokenType.Eof;

            public Token? Peek() =>
                Pos < Tokens.Count ? Tokens[Pos] : null;

            public Token CurrentToken => Tokens[Pos];

            public Token Advance()
            {
                var token = Tokens[Pos];
                Pos++;
                return token;
            }

            public void SkipNewlines()
            {
                while (!IsAtEnd)
                {
                    var tt = Tokens[Pos].TokenType;
                    if (tt == TokenType.Newline || tt == TokenType.Comment)
                        Pos++;
                    else
                        break;
                }
            }

            /// <summary>
            /// Check if the current position starts an assignment line (path = value).
            /// </summary>
            public bool IsAssignmentLine()
            {
                if (IsAtEnd) return false;
                var tok = Tokens[Pos];
                if (tok.TokenType != TokenType.Path && tok.TokenType != TokenType.BareWord)
                    return false;
                int nextPos = Pos + 1;
                if (nextPos >= Tokens.Count) return false;
                return Tokens[nextPos].TokenType == TokenType.Equals;
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Document parsing
        // ─────────────────────────────────────────────────────────────────────

        private static List<OdinDocument> ParseDocuments(ParserState state)
        {
            var documents = new List<OdinDocument>();

            while (true)
            {
                var doc = ParseSingleDocument(state);
                documents.Add(doc);

                // Check for document separator
                state.SkipNewlines();
                var peek = state.Peek();
                if (!state.IsAtEnd && peek != null && peek.TokenType == TokenType.DocumentSeparator)
                {
                    state.Advance(); // consume '---'
                    state.SkipNewlines();
                    state.CurrentHeader = null;
                }
                else
                {
                    break;
                }
            }

            return documents;
        }

        private static OdinDocument ParseSingleDocument(ParserState state)
        {
            // Pre-allocate based on token count (~1 assignment per 4 tokens)
            int est = state.Tokens.Count / 4;
            var metadata = new OrderedMap<string, OdinValue>(Math.Min(est, 16));
            var assignments = new OrderedMap<string, OdinValue>(est);
            var modifiers = new OrderedMap<string, OdinModifiers>();
            var imports = new List<OdinImport>();
            var schemas = new List<OdinSchemaRef>();
            var conditionals = new List<OdinConditional>();
            bool inMetadata = false;
            var arrayIndices = new Dictionary<string, List<int>>();

            state.SkipNewlines();

            while (!state.IsAtEnd)
            {
                var token = state.CurrentToken;

                // Stop at document separator
                if (token.TokenType == TokenType.DocumentSeparator)
                    break;

                switch (token.TokenType)
                {
                    case TokenType.Header:
                        ParseHeaderToken(state, ref inMetadata, metadata, assignments);
                        break;

                    case TokenType.Import:
                        ParseImportToken(state, imports);
                        break;

                    case TokenType.Schema:
                        ParseSchemaToken(state, schemas);
                        break;

                    case TokenType.Conditional:
                        ParseConditionalToken(state, conditionals);
                        break;

                    case TokenType.Path:
                    case TokenType.BooleanLiteral:
                        ParseAssignment(state, inMetadata, metadata, assignments, modifiers, arrayIndices);
                        break;

                    case TokenType.Newline:
                    case TokenType.Comment:
                    {
                        bool wasNewline = token.TokenType == TokenType.Newline;
                        state.Advance();
                        // Blank line (consecutive newlines) exits metadata mode,
                        // but only for the root {$} section (CurrentHeader == null).
                        if (wasNewline && inMetadata && state.CurrentHeader == null)
                        {
                            var nextPeek = state.Peek();
                            if (!state.IsAtEnd && nextPeek != null &&
                                nextPeek.TokenType == TokenType.Newline)
                            {
                                inMetadata = false;
                            }
                        }
                        break;
                    }

                    default:
                        // Skip unexpected tokens
                        state.Advance();
                        break;
                }
            }

            return new OdinDocument(
                metadata: metadata,
                assignments: assignments,
                modifiers: modifiers,
                imports: imports,
                schemas: schemas,
                conditionals: conditionals);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Header handling
        // ─────────────────────────────────────────────────────────────────────

        private static void ParseHeaderToken(
            ParserState state,
            ref bool inMetadata,
            OrderedMap<string, OdinValue> metadata,
            OrderedMap<string, OdinValue> assignments)
        {
            string headerValue = state.CurrentToken.Value;
            state.Advance();
            state.SkipNewlines();

            if (headerValue == "$")
            {
                inMetadata = true;
                state.CurrentHeader = null;
            }
            else if (headerValue.StartsWith("$table.", StringComparison.Ordinal) ||
                     headerValue.StartsWith("$.table.", StringComparison.Ordinal) ||
                     headerValue.StartsWith("table.", StringComparison.Ordinal))
            {
                inMetadata = true;
                state.CurrentHeader = null;
                string tablePart;
                if (headerValue.StartsWith("$.", StringComparison.Ordinal))
                    tablePart = headerValue.Substring(2);
                else if (headerValue.StartsWith("$", StringComparison.Ordinal))
                    tablePart = headerValue.Substring(1);
                else
                    tablePart = headerValue;
                ParseTableData(state, tablePart, metadata);
            }
            else if (string.IsNullOrEmpty(headerValue))
            {
                // {} — root section
                inMetadata = false;
                state.CurrentHeader = null;
            }
            else if (headerValue[0] == '$')
            {
                inMetadata = true;
                state.CurrentHeader = headerValue.Substring(1);
            }
            else if (headerValue[0] == '@')
            {
                inMetadata = false;
                state.CurrentHeader = headerValue.Substring(1);
            }
            else if (headerValue.Contains("[] :"))
            {
                inMetadata = false;
                state.CurrentHeader = null;
                ParseTabularSection(state, headerValue, assignments);
            }
            else if (headerValue.Length > 0 && headerValue[0] == '.')
            {
                // Relative header: {.path} resolves against last absolute header
                inMetadata = false;
                string relativePart = headerValue.Substring(1);
                if (state.PreviousAbsoluteHeader != null)
                    state.CurrentHeader = state.PreviousAbsoluteHeader + "." + relativePart;
                else
                    state.CurrentHeader = relativePart;
            }
            else
            {
                inMetadata = false;
                state.CurrentHeader = headerValue;
                state.PreviousAbsoluteHeader = headerValue;
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Import, Schema, Conditional
        // ─────────────────────────────────────────────────────────────────────

        private static void ParseImportToken(ParserState state, List<OdinImport> imports)
        {
            int line = state.CurrentToken.Line;
            int col = state.CurrentToken.Column;
            string value = state.CurrentToken.Value;
            state.Advance();

            string trimmed = value.Trim();
            if (string.IsNullOrEmpty(trimmed))
            {
                throw new OdinParseException(
                    ParseErrorCode.InvalidDirective, line, col,
                    "Invalid import directive syntax");
            }

            // Check for trailing " as" without identifier
            if (trimmed.EndsWith(" as", StringComparison.Ordinal))
            {
                throw new OdinParseException(
                    ParseErrorCode.InvalidDirective, line, col,
                    "Import alias requires identifier");
            }

            int asPos = trimmed.IndexOf(" as ", StringComparison.Ordinal);
            if (asPos >= 0)
            {
                string path = trimmed.Substring(0, asPos).Trim();
                path = StripQuotes(path);
                string aliasStr = trimmed.Substring(asPos + 4).Trim();
                if (string.IsNullOrEmpty(aliasStr))
                {
                    throw new OdinParseException(
                        ParseErrorCode.InvalidDirective, line, col,
                        "Import alias requires identifier");
                }
                imports.Add(new OdinImport(path) { Alias = aliasStr, Line = line });
            }
            else
            {
                imports.Add(new OdinImport(StripQuotes(trimmed)) { Line = line });
            }
        }

        private static string StripQuotes(string s)
        {
            if (s.Length >= 2 && s[0] == '"' && s[s.Length - 1] == '"')
                return s.Substring(1, s.Length - 2);
            return s;
        }

        private static void ParseSchemaToken(ParserState state, List<OdinSchemaRef> schemas)
        {
            int line = state.CurrentToken.Line;
            int col = state.CurrentToken.Column;
            string value = state.CurrentToken.Value;
            state.Advance();

            string trimmed = value.Trim();
            if (string.IsNullOrEmpty(trimmed))
            {
                throw new OdinParseException(
                    ParseErrorCode.InvalidDirective, line, col,
                    "Schema directive requires URL");
            }

            schemas.Add(new OdinSchemaRef(trimmed) { Line = line });
        }

        private static void ParseConditionalToken(ParserState state, List<OdinConditional> conditionals)
        {
            int line = state.CurrentToken.Line;
            int col = state.CurrentToken.Column;
            string value = state.CurrentToken.Value;
            state.Advance();

            string trimmed = value.Trim();
            if (string.IsNullOrEmpty(trimmed))
            {
                throw new OdinParseException(
                    ParseErrorCode.InvalidDirective, line, col,
                    "Conditional directive requires expression");
            }
            conditionals.Add(new OdinConditional(trimmed) { Line = line });
        }

        // ─────────────────────────────────────────────────────────────────────
        // Assignment parsing
        // ─────────────────────────────────────────────────────────────────────

        private static void ParseAssignment(
            ParserState state,
            bool inMetadata,
            OrderedMap<string, OdinValue> metadata,
            OrderedMap<string, OdinValue> assignments,
            OrderedMap<string, OdinModifiers> modifiers,
            Dictionary<string, List<int>> arrayIndices)
        {
            string pathValue = state.CurrentToken.Value;
            int pathLine = state.CurrentToken.Line;
            int pathCol = state.CurrentToken.Column;
            state.Advance();

            // Build full path with current header
            string fullPath;
            if (state.CurrentHeader != null)
            {
                // If path starts with '[', don't insert dot (array index continuation)
                // Also strip trailing [] from array section headers (e.g., tags[] + [0].value → tags[0].value)
                if (pathValue.Length > 0 && pathValue[0] == '[')
                {
                    var header = state.CurrentHeader;
                    if (header.EndsWith("[]", StringComparison.Ordinal))
                        header = header.Substring(0, header.Length - 2);
                    fullPath = string.Concat(header, pathValue);
                }
                else
                    fullPath = string.Concat(state.CurrentHeader, ".", pathValue);
            }
            else
            {
                fullPath = pathValue;
            }

            // Normalize leading zeros in array indices: [007] -> [7]
            if (fullPath.IndexOf('[') >= 0)
            {
                fullPath = System.Text.RegularExpressions.Regex.Replace(fullPath, @"\[(\d+)\]", m =>
                    "[" + long.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture) + "]");
            }

            // P010: Validate nesting depth (count dots and brackets)
            int depth = 1;
            for (int i = 0; i < fullPath.Length; i++)
            {
                char c = fullPath[i];
                if (c == '.' || c == '[')
                    depth++;
            }
            if (depth > state.Options.MaxDepth)
            {
                throw new OdinParseException(
                    ParseErrorCode.MaximumDepthExceeded,
                    pathLine, pathCol,
                    string.Format(CultureInfo.InvariantCulture, "Maximum nesting depth exceeded: {0} > {1}", depth, state.Options.MaxDepth));
            }

            // P015: Validate all array indices for range before P013 contiguity check
            {
                long cumulativeIndex = 0;
                int searchStart = 0;
                while (true)
                {
                    int bp = fullPath.IndexOf('[', searchStart);
                    if (bp < 0) break;
                    int cp = fullPath.IndexOf(']', bp);
                    if (cp < 0) break;
                    int idxLen = cp - bp - 1;
                    if (idxLen > 0)
                    {
                        // Inline parse digits to avoid Substring allocation
                        long parsedIdx = 0;
                        bool isNeg = false;
                        bool isDigits = true;
                        int si = bp + 1;
                        if (fullPath[si] == '-') { isNeg = true; si++; }
                        for (int di = si; di < cp; di++)
                        {
                            char dc = fullPath[di];
                            if (dc >= '0' && dc <= '9')
                                parsedIdx = parsedIdx * 10 + (dc - '0');
                            else
                            { isDigits = false; break; }
                        }
                        if (isNeg) parsedIdx = -parsedIdx;
                        if (isDigits && (si < cp))
                        {
                            if (parsedIdx < 0)
                            {
                                throw new OdinParseException(
                                    ParseErrorCode.InvalidArrayIndex,
                                    pathLine, pathCol,
                                    string.Format(CultureInfo.InvariantCulture, "Negative array index: {0}", parsedIdx));
                            }
                            if (parsedIdx > state.Options.MaxArrayIndex)
                            {
                                throw new OdinParseException(
                                    ParseErrorCode.ArrayIndexOutOfRange,
                                    pathLine, pathCol,
                                    string.Format(CultureInfo.InvariantCulture, "Array index out of range: {0} > {1}", parsedIdx, state.Options.MaxArrayIndex));
                            }
                            cumulativeIndex += parsedIdx;
                            if (cumulativeIndex > state.Options.MaxArrayIndex)
                            {
                                throw new OdinParseException(
                                    ParseErrorCode.ArrayIndexOutOfRange,
                                    pathLine, pathCol,
                                    string.Format(CultureInfo.InvariantCulture, "Cumulative array index out of range: {0} > {1}", cumulativeIndex, state.Options.MaxArrayIndex));
                            }
                        }
                    }
                    searchStart = cp + 1;
                }
            }

            // P013: Validate array contiguity (first bracket only for tracking)
            int bracketPos = fullPath.IndexOf('[');
            if (bracketPos >= 0)
            {
                int closePos = fullPath.IndexOf(']', bracketPos);
                if (closePos > bracketPos + 1)
                {
                    // Inline parse the index to avoid Substring + TryParse allocation
                    int idx = 0;
                    bool validDigits = true;
                    for (int di = bracketPos + 1; di < closePos; di++)
                    {
                        char dc = fullPath[di];
                        if (dc >= '0' && dc <= '9')
                            idx = idx * 10 + (dc - '0');
                        else
                        { validDigits = false; break; }
                    }
                    if (validDigits)
                    {
                        string arrayBase = fullPath.Substring(0, bracketPos);
                        List<int>? indices;
                        if (!arrayIndices.TryGetValue(arrayBase, out indices))
                        {
                            indices = new List<int>();
                            arrayIndices[arrayBase] = indices;
                        }

                        if (!indices.Contains(idx))
                        {
                            int expected = indices.Count == 0 ? 0 : MaxValue(indices) + 1;
                            if (idx != expected)
                            {
                                throw new OdinParseException(
                                    ParseErrorCode.NonContiguousArrayIndices,
                                    pathLine, pathCol,
                                    string.Format(CultureInfo.InvariantCulture, "Non-contiguous array indices: expected {0}, got {1}", expected, idx));
                            }
                            indices.Add(idx);
                        }
                    }
                }
            }

            // Expect '='
            if (state.IsAtEnd || state.CurrentToken.TokenType != TokenType.Equals)
            {
                throw new OdinParseException(
                    ParseErrorCode.UnexpectedCharacter,
                    pathLine, pathCol,
                    string.Format(CultureInfo.InvariantCulture, "Expected '=' after '{0}'", fullPath));
            }
            state.Advance(); // consume '='

            // Check for duplicate paths
            if (!state.Options.AllowDuplicates)
            {
                bool isDup;
                if (inMetadata)
                    isDup = metadata.ContainsKey(fullPath);
                else if (fullPath.Length > 2 && fullPath[0] == '$' && fullPath[1] == '.')
                    isDup = metadata.ContainsKey(fullPath.Substring(2));
                else
                    isDup = assignments.ContainsKey(fullPath);

                if (isDup)
                {
                    throw new OdinParseException(
                        ParseErrorCode.DuplicatePathAssignment,
                        pathLine, pathCol,
                        fullPath);
                }
            }

            // Parse modifiers and value
            var modsResult = ValueParser.ParseModifiers(state.Tokens, state.Pos);
            var mods = modsResult.Modifiers;
            state.Pos += modsResult.Consumed;

            if (state.IsAtEnd || state.CurrentToken.TokenType == TokenType.Newline)
            {
                // Empty value - treat as empty string
                OdinValue emptyValue = OdinValues.String("");
                if (inMetadata)
                {
                    metadata.Set(fullPath, emptyValue);
                    assignments.Set(string.Concat("$.", fullPath), emptyValue);
                }
                else if (fullPath.Length > 2 && fullPath[0] == '$' && fullPath[1] == '.')
                {
                    string bareKey = fullPath.Substring(2);
                    metadata.Set(bareKey, emptyValue);
                }
                else
                {
                    assignments.Set(fullPath, emptyValue);
                }
                return;
            }

            var valueResult = ValueParser.ParseValue(state.Tokens, state.Pos);
            OdinValue value = valueResult.Value;
            state.Pos += valueResult.Consumed;

            // Parse trailing directives
            var directives = new List<OdinDirective>();
            while (!state.IsAtEnd)
            {
                var tt = state.CurrentToken.TokenType;
                if (tt == TokenType.Newline || tt == TokenType.Comment)
                    break;

                if (tt == TokenType.Directive)
                {
                    string dirName = state.CurrentToken.Value;
                    state.Advance();
                    // Check for directive value
                    DirectiveValue? dirValue = null;
                    if (!state.IsAtEnd)
                    {
                        var nextTt = state.CurrentToken.TokenType;
                        if (nextTt != TokenType.Newline && nextTt != TokenType.Comment && nextTt != TokenType.Directive)
                        {
                            string v = state.CurrentToken.Value;
                            state.Advance();
                            double numVal;
                            if (double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out numVal))
                            {
                                dirValue = DirectiveValue.FromNumber(numVal);
                            }
                            else
                            {
                                dirValue = DirectiveValue.FromString(v);
                            }
                        }
                    }
                    directives.Add(new OdinDirective(dirName, dirValue));
                }
                else
                {
                    // Skip any non-directive tokens remaining on this line
                    state.Advance();
                }
            }

            if (directives.Count > 0)
            {
                value = value.WithDirectives(directives);
            }

            // Apply modifiers to value
            if (mods.HasAny)
            {
                value = value.WithModifiers(mods);
                modifiers.Set(fullPath, mods);
            }

            if (inMetadata)
            {
                assignments.Set(string.Concat("$.", fullPath), value);
                metadata.Set(fullPath, value);
            }
            else if (fullPath.Length > 2 && fullPath[0] == '$' && fullPath[1] == '.')
            {
                // Canonical metadata path: $.key — store in metadata only
                string bareKey = fullPath.Substring(2);
                metadata.Set(bareKey, value);
            }
            else
            {
                assignments.Set(fullPath, value);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Table data parsing
        // ─────────────────────────────────────────────────────────────────────

        private static void ParseTableData(
            ParserState state,
            string header,
            OrderedMap<string, OdinValue> metadata)
        {
            int bracketPos = header.IndexOf('[');
            if (bracketPos < 0) return;
            int closePos = header.IndexOf(']');
            if (closePos < 0) return;

            // Skip "table." prefix (6 chars)
            int tableNameStart = header.IndexOf("table.", StringComparison.Ordinal);
            if (tableNameStart < 0) return;
            string tableName = header.Substring(tableNameStart + 6, bracketPos - (tableNameStart + 6));
            string colsStr = header.Substring(bracketPos + 1, closePos - bracketPos - 1);
            string[] columns = SplitAndTrim(colsStr, ',');
            // Also check for columns after "] :" (e.g., "table.states[] : code, name")
            if (columns.Length == 0 || (columns.Length == 1 && string.IsNullOrEmpty(columns[0])))
            {
                int colonPos = header.IndexOf(':', closePos);
                if (colonPos >= 0)
                {
                    string afterColon = header.Substring(colonPos + 1);
                    columns = SplitAndTrim(afterColon, ',');
                }
            }
            if (columns.Length == 0 || string.IsNullOrEmpty(tableName))
                return;

            int rowIndex = 0;

            while (true)
            {
                state.SkipNewlines();
                if (state.IsAtEnd) break;

                var tok = state.Peek();
                if (tok != null && (tok.TokenType == TokenType.Header || tok.TokenType == TokenType.DocumentSeparator))
                    break;
                if (tok != null && tok.TokenType == TokenType.Comment)
                {
                    state.Advance();
                    continue;
                }

                // Collect values on this line
                var values = new List<string>();
                string? currentVal = null;

                while (!state.IsAtEnd)
                {
                    var t = state.CurrentToken;
                    if (t.TokenType == TokenType.Newline || t.TokenType == TokenType.Header ||
                        t.TokenType == TokenType.DocumentSeparator)
                        break;
                    if (t.TokenType == TokenType.Comment)
                    {
                        state.Advance();
                        break;
                    }

                    if (t.TokenType == TokenType.QuotedString)
                    {
                        currentVal = t.Value;
                        state.Advance();
                        // Check for end of line
                        if (!state.IsAtEnd)
                        {
                            var next = state.Peek();
                            if (next != null &&
                                (next.TokenType == TokenType.Newline ||
                                 next.TokenType == TokenType.Header ||
                                 next.TokenType == TokenType.Comment ||
                                 next.TokenType == TokenType.DocumentSeparator))
                            {
                                if (currentVal != null)
                                {
                                    values.Add(currentVal);
                                    currentVal = null;
                                }
                                break;
                            }
                        }
                    }
                    else if (t.TokenType == TokenType.Path || t.TokenType == TokenType.BareWord)
                    {
                        string v = t.Value;
                        state.Advance();
                        if (v == ",")
                        {
                            if (currentVal != null)
                            {
                                values.Add(currentVal);
                                currentVal = null;
                            }
                        }
                        else if (v.IndexOf(',') >= 0)
                        {
                            if (currentVal != null)
                            {
                                values.Add(currentVal);
                                currentVal = null;
                            }
                            foreach (string part in v.Split(','))
                            {
                                string trimmed = part.Trim().Trim('"');
                                if (trimmed.Length > 0)
                                    values.Add(trimmed);
                            }
                        }
                        else
                        {
                            currentVal = v;
                        }
                    }
                    else
                    {
                        string v = t.Value;
                        state.Advance();
                        if (v == ",")
                        {
                            if (currentVal != null)
                            {
                                values.Add(currentVal);
                                currentVal = null;
                            }
                        }
                        else if (v.IndexOf(',') >= 0)
                        {
                            if (currentVal != null)
                            {
                                values.Add(currentVal);
                                currentVal = null;
                            }
                            foreach (string part in v.Split(','))
                            {
                                string trimmed = part.Trim().Trim('"');
                                if (trimmed.Length > 0)
                                    values.Add(trimmed);
                            }
                        }
                    }
                }
                if (currentVal != null)
                {
                    values.Add(currentVal);
                }

                if (values.Count == 0)
                    continue;

                // Generate metadata entries for this row
                for (int colIdx = 0; colIdx < columns.Length && colIdx < values.Count; colIdx++)
                {
                    string key = string.Format(CultureInfo.InvariantCulture, "table.{0}[{1}].{2}", tableName, rowIndex, columns[colIdx]);
                    metadata.Set(key, OdinValues.String(values[colIdx]));
                }
                rowIndex++;
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Tabular section parsing
        // ─────────────────────────────────────────────────────────────────────

        private static void ParseTabularSection(
            ParserState state,
            string header,
            OrderedMap<string, OdinValue> assignments)
        {
            int colonPos = header.IndexOf(" : ", StringComparison.Ordinal);
            if (colonPos < 0) return;

            string namePart = header.Substring(0, colonPos);
            string colsStr = header.Substring(colonPos + 3);
            string[] columns = SplitAndTrim(colsStr, ',');

            // Resolve relative column names (.field inherits parent from previous dotted column)
            string currentParent = "";
            for (int i = 0; i < columns.Length; i++)
            {
                if (columns[i].Length > 0 && columns[i][0] == '.')
                {
                    columns[i] = currentParent + columns[i];
                }
                else
                {
                    int dotPos = columns[i].IndexOf('.');
                    if (dotPos > 0)
                        currentParent = columns[i].Substring(0, dotPos);
                    else
                        currentParent = "";
                }
            }

            // Extract the base name (strip [] and leading .)
            bool isRelativeTabular = namePart.Length > 0 && namePart[0] == '.';
            string baseName = namePart.TrimStart('.');
            if (baseName.EndsWith("[]", StringComparison.Ordinal))
                baseName = baseName.Substring(0, baseName.Length - 2);

            // Resolve relative tabular headers against previous absolute header
            if (isRelativeTabular && state.PreviousAbsoluteHeader != null)
            {
                baseName = state.PreviousAbsoluteHeader + "." + baseName;
            }

            if (columns.Length == 0 || string.IsNullOrEmpty(baseName))
                return;

            bool isPrimitive = columns.Length == 1 && columns[0] == "~";

            int rowIndex = 0;

            while (true)
            {
                state.SkipNewlines();
                if (state.IsAtEnd) break;

                var tok = state.Peek();
                if (tok != null && (tok.TokenType == TokenType.Header || tok.TokenType == TokenType.DocumentSeparator))
                    break;
                if (tok != null && tok.TokenType == TokenType.Comment)
                {
                    state.Advance();
                    continue;
                }

                // Check if this line is an assignment rather than tabular data
                if (state.IsAssignmentLine())
                {
                    string fieldName = state.Advance().Value;
                    state.Advance(); // consume '='

                    OdinValue val;
                    int consumed;
                    try
                    {
                        var result = ValueParser.ParseValue(state.Tokens, state.Pos);
                        val = result.Value;
                        consumed = result.Consumed;
                    }
                    catch (OdinParseException)
                    {
                        // Skip to end of line on error
                        while (!state.IsAtEnd &&
                               state.CurrentToken.TokenType != TokenType.Newline &&
                               state.CurrentToken.TokenType != TokenType.Header)
                        {
                            state.Advance();
                        }
                        continue;
                    }
                    state.Pos += consumed;

                    string fullKey = string.Format(CultureInfo.InvariantCulture, "{0}[].{1}", baseName, fieldName);

                    // Collect directives
                    var directives = new List<OdinDirective>();
                    if (val.Directives != null)
                    {
                        for (int di = 0; di < val.Directives.Count; di++)
                            directives.Add(val.Directives[di]);
                    }

                    while (!state.IsAtEnd)
                    {
                        var t = state.CurrentToken;
                        if (t.TokenType == TokenType.Newline || t.TokenType == TokenType.Header ||
                            t.TokenType == TokenType.Comment || t.TokenType == TokenType.DocumentSeparator)
                            break;

                        if (t.TokenType == TokenType.Directive)
                        {
                            string dirName = t.Value;
                            state.Advance();
                            DirectiveValue? dirVal = null;
                            if (!state.IsAtEnd)
                            {
                                var next = state.CurrentToken;
                                if (next.TokenType != TokenType.Newline &&
                                    next.TokenType != TokenType.Header &&
                                    next.TokenType != TokenType.Directive &&
                                    next.TokenType != TokenType.Comment &&
                                    next.TokenType != TokenType.DocumentSeparator)
                                {
                                    string sv = next.Value;
                                    state.Advance();
                                    double numVal;
                                    if (double.TryParse(sv, NumberStyles.Float, CultureInfo.InvariantCulture, out numVal))
                                        dirVal = DirectiveValue.FromNumber(numVal);
                                    else
                                        dirVal = DirectiveValue.FromString(sv);
                                }
                            }
                            directives.Add(new OdinDirective(dirName, dirVal));
                        }
                        else
                        {
                            state.Advance();
                        }
                    }

                    if (directives.Count > 0)
                        val = val.WithDirectives(directives);

                    assignments.Set(fullKey, val);

                    // Skip to end of line
                    while (!state.IsAtEnd &&
                           state.CurrentToken.TokenType != TokenType.Newline &&
                           state.CurrentToken.TokenType != TokenType.Header)
                    {
                        state.Advance();
                    }
                    continue;
                }

                // Collect values on this line
                var values = new List<OdinValue>();

                while (!state.IsAtEnd)
                {
                    var t = state.CurrentToken;
                    if (t.TokenType == TokenType.Newline || t.TokenType == TokenType.Header ||
                        t.TokenType == TokenType.DocumentSeparator)
                        break;
                    if (t.TokenType == TokenType.Comment)
                    {
                        state.Advance();
                        break;
                    }

                    try
                    {
                        var result = ValueParser.ParseValue(state.Tokens, state.Pos);
                        state.Pos += result.Consumed;
                        values.Add(result.Value);

                        // Skip remaining tokens until comma or newline
                        while (!state.IsAtEnd)
                        {
                            var ct = state.CurrentToken;
                            if (ct.TokenType == TokenType.Newline || ct.TokenType == TokenType.Header ||
                                ct.TokenType == TokenType.Comment || ct.TokenType == TokenType.DocumentSeparator)
                                break;
                            if (ct.TokenType == TokenType.Comma)
                            {
                                state.Advance();
                                break;
                            }
                            state.Advance();
                        }
                    }
                    catch (OdinParseException)
                    {
                        state.Advance(); // skip unparseable token
                    }
                }

                if (values.Count == 0)
                    continue;

                // Generate assignments for this row
                if (isPrimitive)
                {
                    // Primitive array: just baseName[rowIndex]
                    if (values.Count > 0)
                    {
                        string key = string.Format(CultureInfo.InvariantCulture, "{0}[{1}]", baseName, rowIndex);
                        assignments.Set(key, values[0]);
                    }
                }
                else
                {
                    for (int colIdx = 0; colIdx < columns.Length && colIdx < values.Count; colIdx++)
                    {
                        string key = string.Format(CultureInfo.InvariantCulture, "{0}[{1}].{2}", baseName, rowIndex, columns[colIdx]);
                        assignments.Set(key, values[colIdx]);
                    }
                }
                rowIndex++;
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Utility methods
        // ─────────────────────────────────────────────────────────────────────

        private static string[] SplitAndTrim(string s, char separator)
        {
            string[] parts = s.Split(separator);
            var result = new List<string>(parts.Length);
            for (int i = 0; i < parts.Length; i++)
            {
                string trimmed = parts[i].Trim();
                if (trimmed.Length > 0)
                    result.Add(trimmed);
            }
            return result.ToArray();
        }

        private static int MaxValue(List<int> list)
        {
            int max = list[0];
            for (int i = 1; i < list.Count; i++)
            {
                if (list[i] > max)
                    max = list[i];
            }
            return max;
        }
    }
}
