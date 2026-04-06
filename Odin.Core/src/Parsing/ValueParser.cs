using System;
using System.Collections.Generic;
using System.Globalization;
using Odin.Core.Types;

namespace Odin.Core.Parsing
{
    /// <summary>
    /// Value parser for ODIN tokens. Detects and converts typed values from token streams.
    /// </summary>
    public static class ValueParser
    {
        /// <summary>
        /// Parse a value from a sequence of tokens starting at the given position.
        /// </summary>
        /// <param name="tokens">The token list.</param>
        /// <param name="pos">The position to start parsing from.</param>
        /// <returns>A tuple of the parsed value and the number of tokens consumed.</returns>
        /// <exception cref="OdinParseException">Thrown on invalid value syntax.</exception>
        public static (OdinValue Value, int Consumed) ParseValue(IReadOnlyList<Token> tokens, int pos)
        {
            if (pos >= tokens.Count)
            {
                throw new OdinParseException(ParseErrorCode.UnexpectedCharacter, 0, 0);
            }

            var token = tokens[pos];

            switch (token.TokenType)
            {
                case TokenType.Null:
                    return (OdinValues.Null(), 1);

                case TokenType.BooleanLiteral:
                    return (OdinValues.Boolean(token.Value == "true"), 1);

                case TokenType.BooleanPrefix:
                    // ?true or ?false - consume next token
                    if (pos + 1 < tokens.Count)
                    {
                        var next = tokens[pos + 1];
                        if (next.Value == "true")
                            return (OdinValues.Boolean(true), 2);
                        if (next.Value == "false")
                            return (OdinValues.Boolean(false), 2);
                    }
                    // Just '?' alone - treat as boolean true
                    return (OdinValues.Boolean(true), 1);

                case TokenType.QuotedString:
                    return (OdinValues.String(token.Value), 1);

                case TokenType.BareWord:
                    if (token.Value == "true")
                        return (OdinValues.Boolean(true), 1);
                    if (token.Value == "false")
                        return (OdinValues.Boolean(false), 1);
                    // Bare strings are not allowed in ODIN
                    throw new OdinParseException(
                        ParseErrorCode.BareStringNotAllowed,
                        token.Line, token.Column,
                        string.Format(CultureInfo.InvariantCulture, "Unquoted string \"{0}\" - use double quotes", token.Value));

                case TokenType.NumberPrefix:
                    return (ParseNumber(token.Value, token.Line, token.Column), 1);

                case TokenType.IntegerPrefix:
                    return (ParseInteger(token.Value, token.Line, token.Column), 1);

                case TokenType.CurrencyPrefix:
                    return (ParseCurrency(token.Value, token.Line, token.Column), 1);

                case TokenType.PercentPrefix:
                    return (ParsePercent(token.Value, token.Line, token.Column), 1);

                case TokenType.ReferencePrefix:
                    return (OdinValues.Reference(token.Value), 1);

                case TokenType.BinaryPrefix:
                    return (ParseBinary(token.Value, token.Line, token.Column), 1);

                case TokenType.DateLiteral:
                    return (ParseDateValue(token.Value, token.Line, token.Column), 1);

                case TokenType.TimeLiteral:
                    return (OdinValues.Time(token.Value), 1);

                case TokenType.DurationLiteral:
                    return (OdinValues.Duration(token.Value), 1);

                case TokenType.TimestampLiteral:
                    return (OdinValues.Timestamp(0, token.Value), 1);

                case TokenType.Path:
                    // Path tokens in value position can be temporal values
                    if (IsDateLike(token.Value))
                    {
                        try
                        {
                            return (ParseDateValue(token.Value, token.Line, token.Column), 1);
                        }
                        catch (OdinParseException)
                        {
                            // Fall through to bare string error
                        }
                    }
                    if (token.Value.Length > 0 && token.Value[0] == 'T' && token.Value.IndexOf(':') >= 0)
                    {
                        return (OdinValues.Time(token.Value), 1);
                    }
                    if (token.Value.Length > 1 && token.Value[0] == 'P')
                    {
                        char second = token.Value[1];
                        if ((second >= '0' && second <= '9') || second == 'T')
                        {
                            return (OdinValues.Duration(token.Value), 1);
                        }
                    }
                    // Bare string - not allowed
                    throw new OdinParseException(
                        ParseErrorCode.BareStringNotAllowed,
                        token.Line, token.Column,
                        string.Format(CultureInfo.InvariantCulture, "Unquoted string \"{0}\" - use double quotes", token.Value));

                case TokenType.VerbPrefix:
                {
                    // Unquoted verb expression: %verbName args... - collect rest of line
                    string verbName = token.Value;
                    bool isCustom = verbName.Length > 0 && verbName[0] == '&';
                    string prefix = "%" + verbName;
                    var parts = new List<string> { prefix };
                    int consumed = 1;
                    int i = pos + 1;
                    while (i < tokens.Count)
                    {
                        var t = tokens[i];
                        if (t.TokenType == TokenType.Newline || t.TokenType == TokenType.Comment)
                            break;

                        string text;
                        switch (t.TokenType)
                        {
                            case TokenType.ReferencePrefix:
                                text = "@" + t.Value;
                                break;
                            case TokenType.IntegerPrefix:
                                text = "##" + t.Value;
                                break;
                            case TokenType.NumberPrefix:
                                text = "#" + t.Value;
                                break;
                            case TokenType.CurrencyPrefix:
                                text = "#$" + t.Value;
                                break;
                            case TokenType.PercentPrefix:
                                text = "#%" + t.Value;
                                break;
                            case TokenType.BooleanPrefix:
                                text = "?";
                                break;
                            case TokenType.QuotedString:
                                text = "\"" + t.Value + "\"";
                                break;
                            case TokenType.Null:
                                text = "~";
                                break;
                            case TokenType.Directive:
                                text = ":" + t.Value;
                                break;
                            case TokenType.VerbPrefix:
                                text = "%" + t.Value;
                                break;
                            default:
                                text = t.Value;
                                break;
                        }
                        parts.Add(text);
                        consumed++;
                        i++;
                    }
                    string rawExpr = string.Join(" ", parts);
                    var verb = new OdinVerb(rawExpr, Array.Empty<OdinValue>()) { IsCustom = isCustom };
                    return (verb, consumed);
                }

                case TokenType.NumericLiteral:
                    // Numeric literal in value position
                    return (ParseNumber(token.Value, token.Line, token.Column), 1);

                default:
                    throw new OdinParseException(
                        ParseErrorCode.UnexpectedCharacter,
                        token.Line, token.Column,
                        string.Format(CultureInfo.InvariantCulture, "unexpected token type {0} for value", token.TokenType));
            }
        }

        /// <summary>
        /// Parse modifiers preceding a value (! required, * confidential, - deprecated).
        /// </summary>
        /// <param name="tokens">The token list.</param>
        /// <param name="pos">The position to start parsing from.</param>
        /// <returns>A tuple of the parsed modifiers and the number of tokens consumed.</returns>
        public static (OdinModifiers Modifiers, int Consumed) ParseModifiers(IReadOnlyList<Token> tokens, int pos)
        {
            bool required = false;
            bool confidential = false;
            bool deprecated = false;
            int consumed = 0;

            while (pos + consumed < tokens.Count && tokens[pos + consumed].TokenType == TokenType.Modifier)
            {
                string val = tokens[pos + consumed].Value;
                if (val == "!")
                    required = true;
                else if (val == "*")
                    confidential = true;
                else if (val == "-")
                    deprecated = true;
                else
                    break;
                consumed++;
            }

            var modifiers = new OdinModifiers
            {
                Required = required,
                Confidential = confidential,
                Deprecated = deprecated
            };

            return (modifiers, consumed);
        }

        /// <summary>
        /// Parse a number value from a raw string.
        /// </summary>
        internal static OdinValue ParseNumber(string raw, int line, int col)
        {
            if (string.IsNullOrEmpty(raw))
            {
                throw new OdinParseException(
                    ParseErrorCode.InvalidTypePrefix, line, col,
                    "empty number after '#'");
            }

            // Check for double negatives
            if (raw.StartsWith("--", StringComparison.Ordinal))
            {
                throw new OdinParseException(
                    ParseErrorCode.InvalidTypePrefix, line, col,
                    string.Format(CultureInfo.InvariantCulture, "invalid number: {0}", raw));
            }

            double value;
            if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
            {
                throw new OdinParseException(
                    ParseErrorCode.InvalidTypePrefix, line, col,
                    string.Format(CultureInfo.InvariantCulture, "invalid number: {0}", raw));
            }

            byte? decimalPlaces = null;
            if (raw.Contains("."))
            {
                // Find the decimal part (before any 'e'/'E')
                string lower = raw.ToLowerInvariant();
                int ePos = lower.IndexOf('e');
                string numPart = ePos >= 0 ? raw.Substring(0, ePos) : raw;
                int dotPos = numPart.IndexOf('.');
                if (dotPos >= 0)
                {
                    decimalPlaces = (byte)(numPart.Length - dotPos - 1);
                }
            }

            return new OdinNumber(value) { DecimalPlaces = decimalPlaces, Raw = raw };
        }

        /// <summary>
        /// Parse an integer value from a raw string.
        /// </summary>
        internal static OdinValue ParseInteger(string raw, int line, int col)
        {
            if (string.IsNullOrEmpty(raw))
            {
                throw new OdinParseException(
                    ParseErrorCode.InvalidTypePrefix, line, col,
                    "empty integer after '##'");
            }

            long value;
            if (!long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
            {
                // For very large integers, store 0 but preserve raw
                value = 0;
            }

            return new OdinInteger(value) { Raw = raw };
        }

        /// <summary>
        /// Parse a currency value from a raw string.
        /// </summary>
        internal static OdinValue ParseCurrency(string raw, int line, int col)
        {
            // Format: "100.00" or "100.00:USD"
            string numPart;
            string? currencyCode = null;
            int colonPos = raw.IndexOf(':');
            if (colonPos >= 0)
            {
                numPart = raw.Substring(0, colonPos);
                currencyCode = raw.Substring(colonPos + 1).ToUpperInvariant();
            }
            else
            {
                numPart = raw;
            }

            double value;
            if (!double.TryParse(numPart, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
            {
                throw new OdinParseException(
                    ParseErrorCode.InvalidTypePrefix, line, col,
                    string.Format(CultureInfo.InvariantCulture, "invalid currency: {0}", raw));
            }

            int dotPos = numPart.IndexOf('.');
            byte decimalPlaces = dotPos >= 0 ? (byte)(numPart.Length - dotPos - 1) : (byte)2;

            return new OdinCurrency(value)
            {
                DecimalPlaces = decimalPlaces,
                CurrencyCode = currencyCode,
                Raw = raw
            };
        }

        /// <summary>
        /// Parse a percent value from a raw string.
        /// </summary>
        internal static OdinValue ParsePercent(string raw, int line, int col)
        {
            double value;
            if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
            {
                throw new OdinParseException(
                    ParseErrorCode.InvalidTypePrefix, line, col,
                    string.Format(CultureInfo.InvariantCulture, "invalid percent: {0}", raw));
            }

            return new OdinPercent(value) { Raw = raw };
        }

        /// <summary>
        /// Parse a binary value from a raw string.
        /// </summary>
        internal static OdinValue ParseBinary(string raw, int line, int col)
        {
            if (string.IsNullOrEmpty(raw))
            {
                return OdinValues.Binary(Array.Empty<byte>());
            }

            // Format: "base64data" or "algorithm:base64data"
            int colonPos = raw.IndexOf(':');
            if (colonPos >= 0)
            {
                string algorithm = raw.Substring(0, colonPos);
                string b64Data = raw.Substring(colonPos + 1);
                ValidateBase64(b64Data, line, col);
                byte[] data = Base64Decode(b64Data);
                return OdinValues.BinaryWithAlgorithm(data, algorithm);
            }
            else
            {
                ValidateBase64(raw, line, col);
                byte[] data = Base64Decode(raw);
                return OdinValues.Binary(data);
            }
        }

        /// <summary>
        /// Validate base64 content for invalid characters and padding.
        /// </summary>
        internal static void ValidateBase64(string input, int line, int col)
        {
            bool paddingStarted = false;
            for (int i = 0; i < input.Length; i++)
            {
                char ch = input[i];
                if ((ch >= 'A' && ch <= 'Z') || (ch >= 'a' && ch <= 'z') ||
                    (ch >= '0' && ch <= '9') || ch == '+' || ch == '/')
                {
                    if (paddingStarted)
                    {
                        throw new OdinParseException(
                            ParseErrorCode.UnexpectedCharacter,
                            line, col,
                            "Invalid Base64 padding");
                    }
                }
                else if (ch == '=')
                {
                    paddingStarted = true;
                }
                else if (ch == '\n' || ch == '\r')
                {
                    // Allow newlines
                }
                else
                {
                    throw new OdinParseException(
                        ParseErrorCode.UnexpectedCharacter,
                        line, col,
                        string.Format(CultureInfo.InvariantCulture, "Invalid Base64 character at position {0}", i));
                }
            }
        }

        /// <summary>
        /// Decode base64 string to bytes.
        /// </summary>
        internal static byte[] Base64Decode(string input)
        {
            try
            {
                return Convert.FromBase64String(input);
            }
            catch (FormatException)
            {
                // Fallback manual decoder for lenient parsing
                var output = new List<byte>(input.Length * 3 / 4);
                uint buffer = 0;
                int bits = 0;

                for (int i = 0; i < input.Length; i++)
                {
                    char ch = input[i];
                    int val;
                    if (ch >= 'A' && ch <= 'Z') val = ch - 'A';
                    else if (ch >= 'a' && ch <= 'z') val = ch - 'a' + 26;
                    else if (ch >= '0' && ch <= '9') val = ch - '0' + 52;
                    else if (ch == '+') val = 62;
                    else if (ch == '/') val = 63;
                    else continue;

                    buffer = (buffer << 6) | (uint)val;
                    bits += 6;
                    if (bits >= 8)
                    {
                        bits -= 8;
                        output.Add((byte)(buffer >> bits));
                        buffer &= (uint)((1 << bits) - 1);
                    }
                }

                return output.ToArray();
            }
        }

        /// <summary>
        /// Parse and validate a date string (YYYY-MM-DD).
        /// </summary>
        internal static OdinValue ParseDateValue(string raw, int line, int col)
        {
            string[] parts = raw.Split('-');
            if (parts.Length != 3)
            {
                throw new OdinParseException(
                    ParseErrorCode.UnexpectedCharacter,
                    line, col,
                    string.Format(CultureInfo.InvariantCulture, "invalid date: {0}", raw));
            }

            int year;
            if (!int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out year))
            {
                throw new OdinParseException(
                    ParseErrorCode.UnexpectedCharacter,
                    line, col,
                    string.Format(CultureInfo.InvariantCulture, "invalid date: {0}", raw));
            }

            byte month;
            if (!byte.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out month))
            {
                throw new OdinParseException(
                    ParseErrorCode.UnexpectedCharacter,
                    line, col,
                    string.Format(CultureInfo.InvariantCulture, "invalid date: {0}", raw));
            }

            byte day;
            if (!byte.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out day))
            {
                throw new OdinParseException(
                    ParseErrorCode.UnexpectedCharacter,
                    line, col,
                    string.Format(CultureInfo.InvariantCulture, "invalid date: {0}", raw));
            }

            // Validate month
            if (month < 1 || month > 12)
            {
                throw new OdinParseException(
                    ParseErrorCode.UnexpectedCharacter,
                    line, col,
                    string.Format(CultureInfo.InvariantCulture, "Invalid month {0} in date {1}", month, raw));
            }

            // Validate day
            byte maxDay = DaysInMonth(year, month);
            if (day < 1 || day > maxDay)
            {
                throw new OdinParseException(
                    ParseErrorCode.UnexpectedCharacter,
                    line, col,
                    string.Format(CultureInfo.InvariantCulture, "Invalid day {0} for month {1} in date {2}", day, month, raw));
            }

            return new OdinDate(year, month, day, raw);
        }

        /// <summary>
        /// Check if a string looks like a date (YYYY-MM-DD pattern).
        /// </summary>
        internal static bool IsDateLike(string s)
        {
            if (s.Length < 10) return false;
            if (s[4] != '-') return false;
            if (s[7] != '-') return false;
            for (int i = 0; i < 4; i++)
            {
                if (s[i] < '0' || s[i] > '9') return false;
            }
            return true;
        }

        /// <summary>
        /// Returns the number of days in the given month.
        /// </summary>
        internal static byte DaysInMonth(int year, byte month)
        {
            switch (month)
            {
                case 1: case 3: case 5: case 7: case 8: case 10: case 12:
                    return 31;
                case 4: case 6: case 9: case 11:
                    return 30;
                case 2:
                    return IsLeapYear(year) ? (byte)29 : (byte)28;
                default:
                    return 0;
            }
        }

        /// <summary>
        /// Check if the given year is a leap year.
        /// </summary>
        internal static bool IsLeapYear(int year)
        {
            return (year % 4 == 0 && year % 100 != 0) || (year % 400 == 0);
        }
    }
}
