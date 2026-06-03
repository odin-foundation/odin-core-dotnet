#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Odin.Core.Types;

namespace Odin.Core.Transform;

/// <summary>
/// Thrown when a %expr formula cannot be compiled. Carries the T015 code.
/// </summary>
public sealed class ExprSyntaxException : Exception
{
    /// <summary>Stable transform error code for malformed formulas.</summary>
    public string Code { get; } = "T015";

    /// <summary>Creates a new formula syntax exception.</summary>
    public ExprSyntaxException(string message) : base($"Invalid %expr formula: {message}") { }
}

/// <summary>
/// Compiles an infix arithmetic formula string into a tree of existing transform
/// verbs at parse time. No runtime evaluator: the result is an ordinary verb call,
/// so the arithmetic is performed by the deterministic numeric verbs. Variables
/// resolve under an explicit bindings path passed as the second argument.
///
/// Precedence, high to low: parentheses and function calls; ^ (right-associative);
/// unary -/+ (looser than ^); * / %; then + -.
/// </summary>
internal static class ExprMacro
{
    private static readonly Dictionary<string, string> BinaryOp = new()
    {
        ["+"] = "add",
        ["-"] = "subtract",
        ["*"] = "multiply",
        ["/"] = "divide",
        ["%"] = "mod",
    };

    private readonly struct FnSpec
    {
        public readonly string Verb;
        public readonly int Min;
        public readonly int Max;
        public FnSpec(string verb, int min, int max) { Verb = verb; Min = min; Max = max; }
    }

    private static readonly Dictionary<string, FnSpec> Functions = new()
    {
        ["abs"] = new FnSpec("abs", 1, 1),
        ["floor"] = new FnSpec("floor", 1, 1),
        ["ceil"] = new FnSpec("ceil", 1, 1),
        ["trunc"] = new FnSpec("trunc", 1, 1),
        ["sqrt"] = new FnSpec("sqrt", 1, 1),
        ["round"] = new FnSpec("round", 1, 2),
        ["pow"] = new FnSpec("pow", 2, 2),
        ["min"] = new FnSpec("minOf", 1, int.MaxValue),
        ["max"] = new FnSpec("maxOf", 1, int.MaxValue),
    };

    private enum TokKind { Num, Ident, Op, LParen, RParen, Comma }

    private readonly struct Token
    {
        public readonly TokKind Kind;
        public readonly string Value;
        public readonly bool IsFloat;
        public Token(TokKind kind, string value, bool isFloat = false) { Kind = kind; Value = value; IsFloat = isFloat; }
    }

    /// <summary>
    /// Compiles a formula into a verb call. <paramref name="bindingPath"/> is the path of
    /// the bindings object (e.g. ".vars"); a variable used with no bindings path is an error.
    /// </summary>
    public static VerbCall Compile(string formula, string? bindingPath)
    {
        var tokens = Tokenize(formula);
        var parser = new ExprParser(tokens, bindingPath);
        var arg = parser.Parse();
        // The top-level result must be a verb call; wrap a bare literal/ref in an identity.
        if (arg is VerbCallArg call) return call.NestedCall;
        return new VerbCall { Verb = "coerceNumber", IsCustom = false, Args = new List<VerbArg> { arg } };
    }

    private static bool IsDigit(char c) => c >= '0' && c <= '9';
    private static bool IsIdentStart(char c) => (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || c == '_';
    private static bool IsIdentPart(char c) => IsIdentStart(c) || IsDigit(c) || c == '.';

    private static List<Token> Tokenize(string src)
    {
        var tokens = new List<Token>();
        int i = 0;
        while (i < src.Length)
        {
            char c = src[i];
            if (char.IsWhiteSpace(c)) { i++; continue; }
            if (IsDigit(c))
            {
                int j = i;
                bool isFloat = false;
                while (j < src.Length && IsDigit(src[j])) j++;
                if (j < src.Length && src[j] == '.') { isFloat = true; j++; while (j < src.Length && IsDigit(src[j])) j++; }
                if (j < src.Length && (src[j] == 'e' || src[j] == 'E'))
                {
                    isFloat = true; j++;
                    if (j < src.Length && (src[j] == '+' || src[j] == '-')) j++;
                    while (j < src.Length && IsDigit(src[j])) j++;
                }
                tokens.Add(new Token(TokKind.Num, src.Substring(i, j - i), isFloat));
                i = j;
                continue;
            }
            if (IsIdentStart(c))
            {
                int j = i;
                while (j < src.Length && IsIdentPart(src[j])) j++;
                tokens.Add(new Token(TokKind.Ident, src.Substring(i, j - i)));
                i = j;
                continue;
            }
            if (c == '(') { tokens.Add(new Token(TokKind.LParen, "(")); i++; continue; }
            if (c == ')') { tokens.Add(new Token(TokKind.RParen, ")")); i++; continue; }
            if (c == ',') { tokens.Add(new Token(TokKind.Comma, ",")); i++; continue; }
            if ("+-*/%^".IndexOf(c) >= 0) { tokens.Add(new Token(TokKind.Op, c.ToString())); i++; continue; }
            throw new ExprSyntaxException($"unexpected character '{c}'");
        }
        return tokens;
    }

    private static VerbArg Literal(string text, bool isFloat)
    {
        if (isFloat)
        {
            double d = double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture);
            int dot = text.IndexOf('.');
            byte? dp = dot >= 0 ? (byte?)(text.Length - dot - 1) : null;
            return VerbArg.Lit(new OdinNumber(d) { DecimalPlaces = dp, Raw = text });
        }
        long v = long.Parse(text, NumberStyles.Integer, CultureInfo.InvariantCulture);
        return VerbArg.Lit(new OdinInteger(v));
    }

    private static VerbArg VerbNode(string verb, List<VerbArg> args)
        => VerbArg.NestedCall(new VerbCall { Verb = verb, IsCustom = false, Args = args });

    private sealed class ExprParser
    {
        private readonly List<Token> _tokens;
        private readonly string? _bindingPath;
        private int _pos;

        public ExprParser(List<Token> tokens, string? bindingPath)
        {
            _tokens = tokens;
            _bindingPath = bindingPath;
        }

        private Token? Peek() => _pos < _tokens.Count ? _tokens[_pos] : (Token?)null;
        private Token? Next() => _pos < _tokens.Count ? _tokens[_pos++] : (Token?)null;

        public VerbArg Parse()
        {
            if (_tokens.Count == 0) throw new ExprSyntaxException("empty formula");
            var expr = ParseAdditive();
            if (_pos < _tokens.Count)
                throw new ExprSyntaxException($"unexpected token '{_tokens[_pos].Value}'");
            return expr;
        }

        private VerbArg ParseAdditive()
        {
            var left = ParseMultiplicative();
            while (Peek() is { Kind: TokKind.Op } p && (p.Value == "+" || p.Value == "-"))
            {
                var op = Next()!.Value.Value;
                var right = ParseMultiplicative();
                left = VerbNode(BinaryOp[op], new List<VerbArg> { left, right });
            }
            return left;
        }

        private VerbArg ParseMultiplicative()
        {
            var left = ParseUnary();
            while (Peek() is { Kind: TokKind.Op } p && (p.Value == "*" || p.Value == "/" || p.Value == "%"))
            {
                var op = Next()!.Value.Value;
                var right = ParseUnary();
                left = VerbNode(BinaryOp[op], new List<VerbArg> { left, right });
            }
            return left;
        }

        private VerbArg ParseUnary()
        {
            if (Peek() is { Kind: TokKind.Op } t && (t.Value == "-" || t.Value == "+"))
            {
                Next();
                var operand = ParseUnary();
                return t.Value == "-" ? VerbNode("negate", new List<VerbArg> { operand }) : operand;
            }
            return ParsePower();
        }

        private VerbArg ParsePower()
        {
            var baseArg = ParsePrimary();
            if (Peek() is { Kind: TokKind.Op } p && p.Value == "^")
            {
                Next();
                var exponent = ParseUnary();
                return VerbNode("pow", new List<VerbArg> { baseArg, exponent });
            }
            return baseArg;
        }

        private VerbArg ParsePrimary()
        {
            var t = Next();
            if (t == null) throw new ExprSyntaxException("unexpected end of formula");
            var tok = t.Value;
            if (tok.Kind == TokKind.Num) return Literal(tok.Value, tok.IsFloat);
            if (tok.Kind == TokKind.LParen)
            {
                var inner = ParseAdditive();
                var close = Next();
                if (close == null || close.Value.Kind != TokKind.RParen)
                    throw new ExprSyntaxException("missing closing parenthesis");
                return inner;
            }
            if (tok.Kind == TokKind.Ident)
            {
                if (Peek() is { Kind: TokKind.LParen }) return ParseCall(tok.Value);
                if (_bindingPath == null)
                    throw new ExprSyntaxException(
                        $"variable '{tok.Value}' requires a bindings object, e.g. %expr \"...\" @.vars");
                return VerbArg.Ref(_bindingPath + "." + tok.Value);
            }
            throw new ExprSyntaxException($"unexpected token '{tok.Value}'");
        }

        private VerbArg ParseCall(string name)
        {
            if (!Functions.TryGetValue(name, out var fn))
                throw new ExprSyntaxException($"unknown function '{name}'");
            Next(); // consume '('
            var args = new List<VerbArg>();
            if (!(Peek() is { Kind: TokKind.RParen }))
            {
                args.Add(ParseAdditive());
                while (Peek() is { Kind: TokKind.Comma })
                {
                    Next();
                    args.Add(ParseAdditive());
                }
            }
            var close = Next();
            if (close == null || close.Value.Kind != TokKind.RParen)
                throw new ExprSyntaxException($"missing ) after {name}(");
            if (args.Count < fn.Min || args.Count > fn.Max)
            {
                string range = fn.Min == fn.Max ? fn.Min.ToString(CultureInfo.InvariantCulture)
                    : $"{fn.Min}-{fn.Max}";
                throw new ExprSyntaxException($"{name}() takes {range} arguments, got {args.Count}");
            }
            if (name == "round" && args.Count == 1)
                args.Add(VerbArg.Lit(new OdinInteger(0)));
            return VerbNode(fn.Verb, args);
        }
    }
}
