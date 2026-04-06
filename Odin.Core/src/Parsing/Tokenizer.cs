using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Odin.Core.Types;

namespace Odin.Core.Parsing
{
    /// <summary>
    /// Single-pass character scanner that converts ODIN source text into a stream of tokens.
    /// </summary>
    public static class Tokenizer
    {
        /// <summary>
        /// Tokenize ODIN source text into a list of tokens.
        /// </summary>
        /// <param name="source">The ODIN source text.</param>
        /// <param name="options">Parse options controlling limits.</param>
        /// <returns>A list of tokens ending with an Eof token.</returns>
        /// <exception cref="OdinParseException">Thrown on invalid syntax.</exception>
        public static List<Token> Tokenize(string source, ParseOptions options)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (options == null) throw new ArgumentNullException(nameof(options));

            // Check document size limit
            if (source.Length > options.MaxDocumentSize)
            {
                throw new OdinParseException(ParseErrorCode.MaximumDocumentSizeExceeded, 1, 1);
            }

            // Strip UTF-8 BOM if present
            if (source.Length > 0 && source[0] == '\uFEFF')
            {
                source = source.Substring(1);
            }

            var state = new TokenizerState(source);
            int estimatedSize = source.Length / 12 + 16;
            var tokens = new List<Token>(estimatedSize);

            while (!state.IsAtEnd)
            {
                var token = NextToken(state);
                if (token != null)
                {
                    tokens.Add(token);
                }
            }

            tokens.Add(new Token(TokenType.Eof, state.Pos, state.Pos, state.Line, state.Column, ""));
            return tokens;
        }

        private sealed class TokenizerState
        {
            public readonly string Source;
            public int Pos;
            public int Line;
            public int Column;

            public TokenizerState(string source)
            {
                Source = source;
                Pos = 0;
                Line = 1;
                Column = 1;
            }

            public bool IsAtEnd => Pos >= Source.Length;

            public char Peek()
            {
                return Pos < Source.Length ? Source[Pos] : '\0';
            }

            public char PeekAt(int offset)
            {
                int idx = Pos + offset;
                return idx < Source.Length ? Source[idx] : '\0';
            }

            public bool HasCharAt(int offset)
            {
                return (Pos + offset) < Source.Length;
            }

            public char Advance()
            {
                char ch = Source[Pos];
                Pos++;
                if (ch == '\n')
                {
                    Line++;
                    Column = 1;
                }
                else
                {
                    Column++;
                }
                return ch;
            }

            public void SkipWhitespace()
            {
                while (!IsAtEnd)
                {
                    char ch = Peek();
                    if (ch == ' ' || ch == '\t')
                    {
                        Advance();
                    }
                    else
                    {
                        break;
                    }
                }
            }

            public Token MakeToken(TokenType type, int start, int startLine, int startCol, string value)
            {
                return new Token(type, start, Pos, startLine, startCol, value);
            }
        }

        private static Token? NextToken(TokenizerState state)
        {
            state.SkipWhitespace();

            if (state.IsAtEnd)
                return null;

            char ch = state.Peek();
            int startLine = state.Line;
            int startCol = state.Column;
            int startPos = state.Pos;

            switch (ch)
            {
                case '\n':
                    state.Advance();
                    return new Token(TokenType.Newline, startPos, state.Pos, startLine, startCol, "\n");

                case '\r':
                    state.Advance();
                    if (!state.IsAtEnd && state.Peek() == '\n')
                        state.Advance();
                    return new Token(TokenType.Newline, startPos, state.Pos, startLine, startCol, "\n");

                case ';':
                    return ScanComment(state);

                case '{':
                    return ScanHeader(state);

                case '=':
                    state.Advance();
                    return new Token(TokenType.Equals, startPos, state.Pos, startLine, startCol, "=");

                case '"':
                    return ScanQuotedString(state);

                case '~':
                    state.Advance();
                    return new Token(TokenType.Null, startPos, state.Pos, startLine, startCol, "~");

                case '@':
                    return ScanAt(state);

                case '^':
                    return ScanBinary(state);

                case '#':
                    return ScanHash(state);

                case '?':
                    state.Advance();
                    return new Token(TokenType.BooleanPrefix, startPos, state.Pos, startLine, startCol, "?");

                case '%':
                    return ScanVerb(state);

                case '!':
                    state.Advance();
                    return new Token(TokenType.Modifier, startPos, state.Pos, startLine, startCol, "!");

                case '*':
                    state.Advance();
                    return new Token(TokenType.Modifier, startPos, state.Pos, startLine, startCol, "*");

                case '-':
                    return ScanDash(state);

                case ',':
                    state.Advance();
                    return new Token(TokenType.Comma, startPos, state.Pos, startLine, startCol, ",");

                case ':':
                    return ScanDirective(state);

                case '|':
                    state.Advance();
                    return new Token(TokenType.Pipe, startPos, state.Pos, startLine, startCol, "|");

                case '[':
                    // Array index path at line start: [0].field
                    return ScanBracketPath(state);

                case '&':
                    return ScanExtensionPath(state);

                default:
                    if (ch >= '0' && ch <= '9')
                    {
                        if (LooksLikeDate(state))
                            return ScanDateOrTimestamp(state);
                        else
                            return ScanNumber(state);
                    }
                    if (IsIdentifierStart(ch))
                        return ScanIdentifier(state);
                    return ScanBareValue(state);
            }
        }

        private static Token ScanComment(TokenizerState state)
        {
            int start = state.Pos;
            int startLine = state.Line;
            int startCol = state.Column;
            state.Advance(); // skip ';'
            while (!state.IsAtEnd && state.Peek() != '\n')
            {
                state.Advance();
            }
            string value = state.Source.Substring(start, state.Pos - start);
            return state.MakeToken(TokenType.Comment, start, startLine, startCol, value);
        }

        private static Token ScanQuotedString(TokenizerState state)
        {
            int start = state.Pos;
            int startLine = state.Line;
            int startCol = state.Column;
            state.Advance(); // skip opening '"'

            var value = new StringBuilder();
            bool hasEscapes = false;

            while (!state.IsAtEnd)
            {
                char c = state.Source[state.Pos];
                if (c == '"')
                {
                    state.Advance(); // skip closing '"'
                    if (hasEscapes)
                    {
                        return state.MakeToken(TokenType.QuotedString, start, startLine, startCol, value.ToString());
                    }
                    // No escapes - return the raw content without quotes
                    string raw = state.Source.Substring(start + 1, state.Pos - start - 2);
                    return state.MakeToken(TokenType.QuotedString, start, startLine, startCol, raw);
                }
                if (c == '\\')
                {
                    hasEscapes = true;
                    state.Advance();
                    if (state.IsAtEnd)
                    {
                        throw new OdinParseException(ParseErrorCode.UnterminatedString, startLine, startCol);
                    }
                    char esc = state.Advance();
                    switch (esc)
                    {
                        case 'n': value.Append('\n'); break;
                        case 'r': value.Append('\r'); break;
                        case 't': value.Append('\t'); break;
                        case '\\': value.Append('\\'); break;
                        case '"': value.Append('"'); break;
                        case '/': value.Append('/'); break;
                        case '0': value.Append('\0'); break;
                        case 'u':
                        {
                            char unicodeChar = ScanUnicodeEscape(state, 4, startLine, startCol);
                            int codePoint = unicodeChar;
                            // Check for surrogate pair
                            if (codePoint >= 0xD800 && codePoint <= 0xDBFF)
                            {
                                // High surrogate - expect \uXXXX for low surrogate
                                if (!state.IsAtEnd && state.Peek() == '\\' &&
                                    state.HasCharAt(1) && state.PeekAt(1) == 'u')
                                {
                                    state.Advance(); // skip backslash
                                    state.Advance(); // skip u
                                    char lowChar = ScanUnicodeEscape(state, 4, startLine, startCol);
                                    int lowCode = lowChar;
                                    if (lowCode >= 0xDC00 && lowCode <= 0xDFFF)
                                    {
                                        int combined = 0x10000 + ((codePoint - 0xD800) << 10) + (lowCode - 0xDC00);
                                        value.Append(char.ConvertFromUtf32(combined));
                                    }
                                }
                            }
                            else
                            {
                                value.Append(unicodeChar);
                            }
                            break;
                        }
                        case 'U':
                        {
                            string unicodeStr = ScanUnicodeEscapeString(state, 8, startLine, startCol);
                            value.Append(unicodeStr);
                            break;
                        }
                        default:
                            throw new OdinParseException(
                                ParseErrorCode.InvalidEscapeSequence,
                                state.Line, state.Column,
                                string.Format(CultureInfo.InvariantCulture, "unknown escape: \\{0}", esc));
                    }
                }
                else if (c == '\n')
                {
                    throw new OdinParseException(ParseErrorCode.UnterminatedString, startLine, startCol);
                }
                else
                {
                    value.Append(c);
                    state.Advance();
                }
            }

            throw new OdinParseException(ParseErrorCode.UnterminatedString, startLine, startCol);
        }

        private static char ScanUnicodeEscape(TokenizerState state, int digits, int startLine, int startCol)
        {
            var hex = new StringBuilder(digits);
            for (int i = 0; i < digits; i++)
            {
                if (state.IsAtEnd)
                {
                    throw new OdinParseException(
                        ParseErrorCode.InvalidEscapeSequence,
                        startLine, startCol,
                        "incomplete unicode escape");
                }
                hex.Append(state.Advance());
            }

            int code;
            try
            {
                code = Convert.ToInt32(hex.ToString(), 16);
            }
            catch (FormatException)
            {
                throw new OdinParseException(
                    ParseErrorCode.InvalidEscapeSequence,
                    startLine, startCol,
                    string.Format(CultureInfo.InvariantCulture, "invalid hex in unicode escape: \\u{0}", hex));
            }
            catch (OverflowException)
            {
                throw new OdinParseException(
                    ParseErrorCode.InvalidEscapeSequence,
                    startLine, startCol,
                    string.Format(CultureInfo.InvariantCulture, "invalid unicode code point: U+{0:X4}", hex));
            }

            // For surrogate range, return the raw char value (will be combined later)
            if (code >= 0xD800 && code <= 0xDFFF)
                return (char)code;

            if (code < 0 || code > 0x10FFFF)
            {
                throw new OdinParseException(
                    ParseErrorCode.InvalidEscapeSequence,
                    startLine, startCol,
                    string.Format(CultureInfo.InvariantCulture, "invalid unicode code point: U+{0:X4}", code));
            }

            return (char)code;
        }

        /// <summary>Scan a unicode escape that may produce a surrogate pair (for \U 8-digit escapes).</summary>
        private static string ScanUnicodeEscapeString(TokenizerState state, int digits, int startLine, int startCol)
        {
            var hex = new StringBuilder(digits);
            for (int i = 0; i < digits; i++)
            {
                if (state.IsAtEnd)
                {
                    throw new OdinParseException(
                        ParseErrorCode.InvalidEscapeSequence,
                        startLine, startCol,
                        "unterminated unicode escape");
                }
                hex.Append(state.Peek());
                state.Advance();
            }

            int code;
            try
            {
                code = Convert.ToInt32(hex.ToString(), 16);
            }
            catch (FormatException)
            {
                throw new OdinParseException(
                    ParseErrorCode.InvalidEscapeSequence,
                    startLine, startCol,
                    string.Format(CultureInfo.InvariantCulture, "invalid hex in unicode escape: \\U{0}", hex));
            }

            if (code < 0 || code > 0x10FFFF || (code >= 0xD800 && code <= 0xDFFF))
            {
                throw new OdinParseException(
                    ParseErrorCode.InvalidEscapeSequence,
                    startLine, startCol,
                    string.Format(CultureInfo.InvariantCulture, "invalid unicode code point: U+{0:X}", code));
            }

            return char.ConvertFromUtf32(code);
        }

        private static Token ScanHeader(TokenizerState state)
        {
            int start = state.Pos;
            int startLine = state.Line;
            int startCol = state.Column;
            state.Advance(); // skip '{'

            int contentStart = state.Pos;
            while (!state.IsAtEnd)
            {
                char c = state.Source[state.Pos];
                if (c == '}')
                {
                    string headerValue = state.Source.Substring(contentStart, state.Pos - contentStart);
                    state.Advance(); // skip '}'

                    // Validate bracket usage in headers
                    int bracketStart = headerValue.IndexOf('[');
                    if (bracketStart >= 0 && !headerValue.StartsWith("$table", StringComparison.Ordinal) && !headerValue.StartsWith("table.", StringComparison.Ordinal))
                    {
                        int bracketEnd = headerValue.IndexOf(']');
                        if (bracketEnd < 0)
                        {
                            throw new OdinParseException(
                                ParseErrorCode.InvalidArrayIndex,
                                startLine, startCol,
                                headerValue);
                        }
                        string bracketContent = headerValue.Substring(bracketStart + 1, bracketEnd - bracketStart - 1);
                        // Valid: empty (array push), digits (index), or field list
                        bool valid = bracketContent.Length == 0
                            || IsAllDigits(bracketContent)
                            || IsValidFieldList(bracketContent);
                        if (!valid)
                        {
                            throw new OdinParseException(
                                ParseErrorCode.InvalidArrayIndex,
                                startLine, startCol,
                                headerValue);
                        }
                    }

                    return state.MakeToken(TokenType.Header, start, startLine, startCol, headerValue);
                }
                if (c == '\n')
                {
                    throw new OdinParseException(ParseErrorCode.InvalidHeaderSyntax, startLine, startCol);
                }
                state.Advance();
            }

            throw new OdinParseException(ParseErrorCode.InvalidHeaderSyntax, startLine, startCol);
        }

        private static Token ScanIdentifier(TokenizerState state)
        {
            int start = state.Pos;
            int startLine = state.Line;
            int startCol = state.Column;

            char first = state.Peek();

            // Check for time literal: T + digit
            if (first == 'T' && state.HasCharAt(1) && state.PeekAt(1) >= '0' && state.PeekAt(1) <= '9')
            {
                return ScanTime(state);
            }

            // Check for duration literal: P + (digit|T)
            if (first == 'P' && state.HasCharAt(1))
            {
                char next = state.PeekAt(1);
                if ((next >= '0' && next <= '9') || next == 'T')
                {
                    return ScanDuration(state);
                }
            }

            bool inBracket = false;
            while (!state.IsAtEnd)
            {
                char c = state.Peek();
                if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') ||
                    (c >= '0' && c <= '9') || c == '_' || c == '.' || c == '$')
                {
                    state.Advance();
                }
                else if (c == '[')
                {
                    inBracket = true;
                    state.Advance();
                }
                else if (c == ']')
                {
                    inBracket = false;
                    state.Advance();
                }
                else if (c == '-' && inBracket)
                {
                    // Allow '-' inside brackets for negative index detection
                    state.Advance();
                }
                else
                {
                    break;
                }
            }

            string identValue = state.Source.Substring(start, state.Pos - start);

            // Check for negative array indices -> P003
            if (identValue.Contains("[-"))
            {
                throw new OdinParseException(
                    ParseErrorCode.InvalidArrayIndex,
                    startLine, startCol,
                    string.Format(CultureInfo.InvariantCulture, "Negative array index in path: {0}", identValue));
            }

            // Check for special bare words
            if (identValue == "true" || identValue == "false")
            {
                return state.MakeToken(TokenType.BooleanLiteral, start, startLine, startCol, identValue);
            }

            return state.MakeToken(TokenType.Path, start, startLine, startCol, identValue);
        }

        private static Token ScanBracketPath(TokenizerState state)
        {
            int start = state.Pos;
            int startLine = state.Line;
            int startCol = state.Column;

            // Scan [digits] and any following path segments (.field, [n], etc.)
            bool inBracket = false;
            while (!state.IsAtEnd)
            {
                char c = state.Peek();
                if (c == '[')
                {
                    inBracket = true;
                    state.Advance();
                }
                else if (c == ']')
                {
                    inBracket = false;
                    state.Advance();
                }
                else if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') ||
                         (c >= '0' && c <= '9') || c == '_' || c == '.')
                {
                    state.Advance();
                }
                else if (c == '-' && inBracket)
                {
                    state.Advance();
                }
                else
                {
                    break;
                }
            }

            string pathValue = state.Source.Substring(start, state.Pos - start);
            return state.MakeToken(TokenType.Path, start, startLine, startCol, pathValue);
        }

        private static Token ScanBareValue(TokenizerState state)
        {
            int start = state.Pos;
            int startLine = state.Line;
            int startCol = state.Column;

            while (!state.IsAtEnd)
            {
                char c = state.Peek();
                if (c == '\n' || c == '\r' || c == ';')
                    break;

                if (c == ' ' || c == '\t')
                {
                    // Check if this is just trailing whitespace before EOL/comment
                    int savedPos = state.Pos;
                    int savedLine = state.Line;
                    int savedCol = state.Column;
                    state.SkipWhitespace();
                    if (state.IsAtEnd || state.Peek() == '\n' || state.Peek() == '\r' || state.Peek() == ';')
                    {
                        break;
                    }
                    // Not trailing - restore and continue
                    state.Pos = savedPos;
                    state.Line = savedLine;
                    state.Column = savedCol;
                    state.Advance();
                }
                else
                {
                    state.Advance();
                }
            }

            string bareValue = state.Source.Substring(start, state.Pos - start).TrimEnd();
            return state.MakeToken(TokenType.BareWord, start, startLine, startCol, bareValue);
        }

        private static Token ScanNumber(TokenizerState state)
        {
            int start = state.Pos;
            int startLine = state.Line;
            int startCol = state.Column;

            // Allow leading negative sign
            if (!state.IsAtEnd && state.Peek() == '-')
            {
                state.Advance();
            }

            while (!state.IsAtEnd)
            {
                char c = state.Peek();
                if ((c >= '0' && c <= '9') || c == '.' || c == 'e' || c == 'E' || c == '+' || c == '-')
                {
                    state.Advance();
                }
                else
                {
                    break;
                }
            }

            string numValue = state.Source.Substring(start, state.Pos - start);
            return state.MakeToken(TokenType.NumericLiteral, start, startLine, startCol, numValue);
        }

        private static Token ScanDateOrTimestamp(TokenizerState state)
        {
            int start = state.Pos;
            int startLine = state.Line;
            int startCol = state.Column;

            // Read the full value until whitespace/newline/semicolon
            while (!state.IsAtEnd)
            {
                char c = state.Peek();
                if (c == '\n' || c == '\r' || c == ' ' || c == '\t' || c == ';')
                    break;
                state.Advance();
            }

            string dtValue = state.Source.Substring(start, state.Pos - start);

            // Determine if timestamp (contains 'T') or date
            if (dtValue.IndexOf('T') >= 0)
            {
                return state.MakeToken(TokenType.TimestampLiteral, start, startLine, startCol, dtValue);
            }
            return state.MakeToken(TokenType.DateLiteral, start, startLine, startCol, dtValue);
        }

        private static Token ScanTime(TokenizerState state)
        {
            int start = state.Pos;
            int startLine = state.Line;
            int startCol = state.Column;

            while (!state.IsAtEnd)
            {
                char c = state.Peek();
                if ((c >= '0' && c <= '9') || c == 'T' || c == ':' || c == '.')
                {
                    state.Advance();
                }
                else
                {
                    break;
                }
            }

            string timeValue = state.Source.Substring(start, state.Pos - start);
            return state.MakeToken(TokenType.TimeLiteral, start, startLine, startCol, timeValue);
        }

        private static Token ScanDuration(TokenizerState state)
        {
            int start = state.Pos;
            int startLine = state.Line;
            int startCol = state.Column;

            while (!state.IsAtEnd)
            {
                char c = state.Peek();
                if (c == 'P' || c == 'T' || c == 'Y' || c == 'M' || c == 'W' ||
                    c == 'D' || c == 'H' || c == 'S' || (c >= '0' && c <= '9') || c == '.')
                {
                    state.Advance();
                }
                else
                {
                    break;
                }
            }

            string durValue = state.Source.Substring(start, state.Pos - start);
            return state.MakeToken(TokenType.DurationLiteral, start, startLine, startCol, durValue);
        }

        private static Token ScanDirective(TokenizerState state)
        {
            int start = state.Pos;
            int startLine = state.Line;
            int startCol = state.Column;
            state.Advance(); // skip ':'

            int nameStart = state.Pos;
            while (!state.IsAtEnd)
            {
                char c = state.Peek();
                if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') ||
                    (c >= '0' && c <= '9') || c == '_')
                {
                    state.Advance();
                }
                else
                {
                    break;
                }
            }

            string name = state.Source.Substring(nameStart, state.Pos - nameStart);
            return state.MakeToken(TokenType.Directive, start, startLine, startCol, name);
        }

        private static Token ScanAt(TokenizerState state)
        {
            int start = state.Pos;
            int startLine = state.Line;
            int startCol = state.Column;
            state.Advance(); // skip '@'

            // Read the keyword/path after @
            int wordStart = state.Pos;
            while (!state.IsAtEnd)
            {
                char c = state.Peek();
                if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') ||
                    (c >= '0' && c <= '9') || c == '_' || c == '.' ||
                    c == '[' || c == ']' || c == '$' || c == ':' || c == '-' || c == '@')
                {
                    state.Advance();
                }
                else
                {
                    break;
                }
            }
            string word = state.Source.Substring(wordStart, state.Pos - wordStart);

            // Normalize leading zeros in array indices: [007] -> [7]
            if (word.IndexOf('[') >= 0)
            {
                word = System.Text.RegularExpressions.Regex.Replace(word, @"\[(\d+)\]", m =>
                    "[" + long.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture) + "]");
            }

            switch (word)
            {
                case "import":
                {
                    state.SkipWhitespace();
                    int restStart = state.Pos;
                    while (!state.IsAtEnd && state.Peek() != '\n' && state.Peek() != '\r')
                    {
                        state.Advance();
                    }
                    string rest = state.Source.Substring(restStart, state.Pos - restStart).TrimEnd();
                    int commentIdx = FindCommentStart(rest);
                    if (commentIdx >= 0)
                        rest = rest.Substring(0, commentIdx).TrimEnd();
                    return state.MakeToken(TokenType.Import, start, startLine, startCol, rest);
                }
                case "schema":
                {
                    state.SkipWhitespace();
                    int restStart = state.Pos;
                    while (!state.IsAtEnd && state.Peek() != '\n' && state.Peek() != '\r')
                    {
                        state.Advance();
                    }
                    string rest = state.Source.Substring(restStart, state.Pos - restStart).TrimEnd();
                    int commentIdx = FindCommentStart(rest);
                    if (commentIdx >= 0)
                        rest = rest.Substring(0, commentIdx).TrimEnd();
                    return state.MakeToken(TokenType.Schema, start, startLine, startCol, rest);
                }
                case "if":
                {
                    state.SkipWhitespace();
                    int restStart = state.Pos;
                    while (!state.IsAtEnd && state.Peek() != '\n' && state.Peek() != '\r')
                    {
                        state.Advance();
                    }
                    string rest = state.Source.Substring(restStart, state.Pos - restStart).TrimEnd();
                    int commentIdx = FindCommentStart(rest);
                    if (commentIdx >= 0)
                        rest = rest.Substring(0, commentIdx).TrimEnd();
                    return state.MakeToken(TokenType.Conditional, start, startLine, startCol, rest);
                }
                case "":
                    // Bare '@' at column 1 is always invalid
                    if (startCol == 1)
                    {
                        throw new OdinParseException(
                            ParseErrorCode.UnexpectedCharacter,
                            startLine, startCol,
                            "Unexpected character: @");
                    }
                    // Bare '@' followed by '#' is invalid (e.g., @#$invalid)
                    if (!state.IsAtEnd && state.Peek() == '#')
                    {
                        throw new OdinParseException(
                            ParseErrorCode.UnexpectedCharacter,
                            startLine, startCol,
                            "Unexpected character: @#");
                    }
                    // Valid in transform context as "current item" reference
                    return state.MakeToken(TokenType.ReferencePrefix, start, startLine, startCol, string.Empty);

                default:
                    // At column 1, an unknown @word is an invalid directive
                    // UNLESS it looks like a reference (contains brackets or dots)
                    if (startCol == 1 && word.IndexOf('[') < 0 && word.IndexOf('.') < 0)
                    {
                        throw new OdinParseException(
                            ParseErrorCode.UnexpectedCharacter,
                            startLine, startCol,
                            string.Format(CultureInfo.InvariantCulture, "Invalid directive: @{0}", word));
                    }
                    // Otherwise it's a reference: @path.to.thing
                    return state.MakeToken(TokenType.ReferencePrefix, start, startLine, startCol, word);
            }
        }

        private static Token ScanBinary(TokenizerState state)
        {
            int start = state.Pos;
            int startLine = state.Line;
            int startCol = state.Column;
            state.Advance(); // skip '^'

            int valStart = state.Pos;
            while (!state.IsAtEnd)
            {
                char c = state.Peek();
                if (c == '\n' || c == '\r' || c == ' ' || c == '\t' || c == ';')
                    break;
                state.Advance();
            }
            string binValue = state.Source.Substring(valStart, state.Pos - valStart);
            return new Token(TokenType.BinaryPrefix, start, state.Pos, startLine, startCol, binValue);
        }

        private static Token ScanHash(TokenizerState state)
        {
            int start = state.Pos;
            int startLine = state.Line;
            int startCol = state.Column;
            state.Advance(); // skip '#'

            if (!state.IsAtEnd)
            {
                char next = state.Peek();
                switch (next)
                {
                    case '#':
                    {
                        state.Advance();
                        var num = ScanNumber(state);
                        return new Token(TokenType.IntegerPrefix, start, num.End, startLine, startCol, num.Value);
                    }
                    case '$':
                    {
                        state.Advance();
                        var num = ScanNumber(state);
                        string hashValue = num.Value;
                        // Check for currency code after colon
                        if (!state.IsAtEnd && state.Peek() == ':')
                        {
                            state.Advance();
                            int codeStart = state.Pos;
                            while (!state.IsAtEnd)
                            {
                                char c = state.Peek();
                                if ((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z'))
                                    state.Advance();
                                else
                                    break;
                            }
                            string code = state.Source.Substring(codeStart, state.Pos - codeStart);
                            hashValue = string.Format(CultureInfo.InvariantCulture, "{0}:{1}", hashValue, code);
                        }
                        return new Token(TokenType.CurrencyPrefix, start, state.Pos, startLine, startCol, hashValue);
                    }
                    case '%':
                    {
                        state.Advance();
                        var num = ScanNumber(state);
                        return new Token(TokenType.PercentPrefix, start, num.End, startLine, startCol, num.Value);
                    }
                    default:
                        if ((next >= '0' && next <= '9') || next == '-' || next == '.')
                        {
                            var num = ScanNumber(state);
                            return new Token(TokenType.NumberPrefix, start, num.End, startLine, startCol, num.Value);
                        }
                        // Bare '#' with no valid follower
                        throw new OdinParseException(
                            ParseErrorCode.InvalidTypePrefix,
                            startLine, startCol,
                            "expected number after '#'");
                }
            }

            // Bare '#' at end of input
            throw new OdinParseException(
                ParseErrorCode.InvalidTypePrefix,
                startLine, startCol,
                "expected number after '#'");
        }

        private static Token ScanVerb(TokenizerState state)
        {
            int start = state.Pos;
            int startLine = state.Line;
            int startCol = state.Column;
            state.Advance(); // skip '%'

            int nameStart = state.Pos;
            // Check for custom verb prefix '&'
            if (!state.IsAtEnd && state.Peek() == '&')
            {
                state.Advance();
            }
            while (!state.IsAtEnd)
            {
                char c = state.Peek();
                if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') ||
                    (c >= '0' && c <= '9') || c == '_' || c == '.')
                {
                    state.Advance();
                }
                else
                {
                    break;
                }
            }
            string verbName = state.Source.Substring(nameStart, state.Pos - nameStart);
            return new Token(TokenType.VerbPrefix, start, state.Pos, startLine, startCol, verbName);
        }

        private static Token ScanDash(TokenizerState state)
        {
            int start = state.Pos;
            int startLine = state.Line;
            int startCol = state.Column;

            // Check for document separator '---'
            if (state.HasCharAt(1) && state.PeekAt(1) == '-' &&
                state.HasCharAt(2) && state.PeekAt(2) == '-')
            {
                state.Advance();
                state.Advance();
                state.Advance();
                return new Token(TokenType.DocumentSeparator, start, state.Pos, startLine, startCol, "---");
            }

            // Check for deprecated modifier before a date: -YYYY-MM-DD
            if (state.HasCharAt(1) && state.PeekAt(1) >= '0' && state.PeekAt(1) <= '9' &&
                state.HasCharAt(2) && state.PeekAt(2) >= '0' && state.PeekAt(2) <= '9' &&
                state.HasCharAt(3) && state.PeekAt(3) >= '0' && state.PeekAt(3) <= '9' &&
                state.HasCharAt(4) && state.PeekAt(4) >= '0' && state.PeekAt(4) <= '9' &&
                state.HasCharAt(5) && state.PeekAt(5) == '-')
            {
                // This is a deprecated modifier followed by a date like 2024-01-15
                state.Advance();
                return new Token(TokenType.Modifier, start, state.Pos, startLine, startCol, "-");
            }

            // Check if this is a negative number (followed by digit)
            if (state.HasCharAt(1))
            {
                char next = state.PeekAt(1);
                if (next >= '0' && next <= '9')
                {
                    // This is a bare negative number - scan as bare value
                    return ScanBareValue(state);
                }
            }

            // Otherwise it's a deprecated modifier
            state.Advance();
            return new Token(TokenType.Modifier, start, state.Pos, startLine, startCol, "-");
        }

        private static bool LooksLikeDate(TokenizerState state)
        {
            // Need at least 10 chars: YYYY-MM-DD
            if (state.Pos + 10 > state.Source.Length)
                return false;

            string s = state.Source;
            int p = state.Pos;

            for (int i = 0; i < 4; i++)
            {
                if (s[p + i] < '0' || s[p + i] > '9')
                    return false;
            }

            return s[p + 4] == '-'
                && s[p + 5] >= '0' && s[p + 5] <= '9'
                && s[p + 6] >= '0' && s[p + 6] <= '9'
                && s[p + 7] == '-'
                && s[p + 8] >= '0' && s[p + 8] <= '9'
                && s[p + 9] >= '0' && s[p + 9] <= '9';
        }

        private static bool IsIdentifierStart(char ch)
        {
            return (ch >= 'a' && ch <= 'z') || (ch >= 'A' && ch <= 'Z') || ch == '_' || ch == '$';
        }

        private static bool IsAllDigits(string s)
        {
            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] < '0' || s[i] > '9')
                    return false;
            }
            return true;
        }

        private static bool IsValidFieldList(string s)
        {
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (!((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') ||
                      (c >= '0' && c <= '9') || c == '_' || c == ',' || c == ' '))
                {
                    return false;
                }
            }
            return true;
        }

        private static Token ScanExtensionPath(TokenizerState state)
        {
            int start = state.Pos;
            int startLine = state.Line;
            int startCol = state.Column;
            state.Advance(); // skip '&'

            int wordStart = state.Pos;
            while (!state.IsAtEnd)
            {
                char c = state.Peek();
                if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') ||
                    (c >= '0' && c <= '9') || c == '_' || c == '.')
                {
                    state.Advance();
                }
                else
                {
                    break;
                }
            }
            string word = state.Source.Substring(wordStart, state.Pos - wordStart);
            return state.MakeToken(TokenType.Path, start, startLine, startCol, "&" + word);
        }

        /// <summary>
        /// Find the start of a comment (;) in a string, respecting quotes.
        /// Returns -1 if no comment found.
        /// </summary>
        private static int FindCommentStart(string s)
        {
            bool inQuotes = false;
            for (int i = 0; i < s.Length; i++)
            {
                char ch = s[i];
                if (ch == '"')
                    inQuotes = !inQuotes;
                else if (ch == ';' && !inQuotes)
                    return i;
            }
            return -1;
        }
    }
}
