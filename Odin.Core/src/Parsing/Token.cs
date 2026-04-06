using System;

namespace Odin.Core.Parsing
{
    /// <summary>
    /// Types of tokens produced by the ODIN tokenizer.
    /// </summary>
    public enum TokenType
    {
        /// <summary>A path segment (e.g., <c>name</c>, <c>policy.number</c>, <c>items[0]</c>).</summary>
        Path,
        /// <summary>The <c>=</c> assignment operator.</summary>
        Equals,
        /// <summary>A quoted string value (e.g., <c>"hello"</c>).</summary>
        QuotedString,
        /// <summary>A bare word value (unquoted string).</summary>
        BareWord,
        /// <summary>A number prefix <c>#</c>.</summary>
        NumberPrefix,
        /// <summary>An integer prefix <c>##</c>.</summary>
        IntegerPrefix,
        /// <summary>A currency prefix <c>#$</c>.</summary>
        CurrencyPrefix,
        /// <summary>A percent prefix <c>#%</c>.</summary>
        PercentPrefix,
        /// <summary>A boolean prefix <c>?</c> (optional).</summary>
        BooleanPrefix,
        /// <summary>A null value <c>~</c>.</summary>
        Null,
        /// <summary>A reference prefix <c>@</c>.</summary>
        ReferencePrefix,
        /// <summary>A binary prefix <c>^</c>.</summary>
        BinaryPrefix,
        /// <summary>A verb prefix <c>%</c>.</summary>
        VerbPrefix,
        /// <summary>A section header (e.g., <c>{Policy}</c>, <c>{$}</c>).</summary>
        Header,
        /// <summary>A comment (<c>;</c> to end of line).</summary>
        Comment,
        /// <summary>A directive (e.g., <c>:pos</c>, <c>:len</c>, <c>:format</c>).</summary>
        Directive,
        /// <summary>An <c>@import</c> directive.</summary>
        Import,
        /// <summary>An <c>@schema</c> directive.</summary>
        Schema,
        /// <summary>A newline.</summary>
        Newline,
        /// <summary>End of file.</summary>
        Eof,
        /// <summary>A numeric literal (the digits following a prefix).</summary>
        NumericLiteral,
        /// <summary>A boolean literal (<c>true</c> or <c>false</c>).</summary>
        BooleanLiteral,
        /// <summary>A date literal (e.g., <c>2024-06-15</c>).</summary>
        DateLiteral,
        /// <summary>A timestamp literal (e.g., <c>2024-06-15T14:30:00Z</c>).</summary>
        TimestampLiteral,
        /// <summary>A time literal (e.g., <c>T14:30:00</c>).</summary>
        TimeLiteral,
        /// <summary>A duration literal (e.g., <c>P1Y6M</c>).</summary>
        DurationLiteral,
        /// <summary>A modifier prefix (<c>!</c>, <c>*</c>, <c>-</c>).</summary>
        Modifier,
        /// <summary>Tabular column separator <c>|</c>.</summary>
        Pipe,
        /// <summary>Document separator <c>---</c>.</summary>
        DocumentSeparator,
        /// <summary>An <c>@if</c> conditional directive.</summary>
        Conditional,
        /// <summary>Comma separator <c>,</c>.</summary>
        Comma,
    }

    /// <summary>
    /// A token produced by the ODIN tokenizer.
    /// </summary>
    public sealed class Token
    {
        /// <summary>The token type.</summary>
        public TokenType TokenType { get; }

        /// <summary>Character offset in the source text where the token starts.</summary>
        public int Start { get; }

        /// <summary>Character offset in the source text where the token ends (exclusive).</summary>
        public int End { get; }

        /// <summary>Line number (1-based).</summary>
        public int Line { get; }

        /// <summary>Column number (1-based).</summary>
        public int Column { get; }

        /// <summary>The token's text content.</summary>
        public string Value { get; }

        /// <summary>Creates a new token.</summary>
        /// <param name="tokenType">The token type.</param>
        /// <param name="start">Start offset in source.</param>
        /// <param name="end">End offset in source (exclusive).</param>
        /// <param name="line">Line number (1-based).</param>
        /// <param name="column">Column number (1-based).</param>
        /// <param name="value">The token text content.</param>
        public Token(TokenType tokenType, int start, int end, int line, int column, string value)
        {
            TokenType = tokenType;
            Start = start;
            End = end;
            Line = line;
            Column = column;
            Value = value ?? string.Empty;
        }

        /// <inheritdoc/>
        public override string ToString() => $"{TokenType}({Value}) [{Line}:{Column}]";
    }
}
