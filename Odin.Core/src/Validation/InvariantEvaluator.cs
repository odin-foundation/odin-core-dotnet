using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using Odin.Core.Types;

namespace Odin.Core.Validation
{
    /// <summary>
    /// Recursive-descent evaluator over the invariant grammar:
    ///   expression     = logic_or
    ///   logic_or       = logic_and , { "||" , logic_and }
    ///   logic_and      = equality , { "&amp;&amp;" , equality }
    ///   equality       = comparison , { ( "==" | "!=" | "=" ) , comparison }
    ///   comparison     = additive , { ( "&gt;" | "&lt;" | "&gt;=" | "&lt;=" ) , additive }
    ///   additive       = multiplicative , { ( "+" | "-" ) , multiplicative }
    ///   multiplicative = unary , { ( "*" | "/" | "%" ) , unary }
    ///   unary          = [ "!" ] , primary
    ///   primary        = path | number | string | "(" , expression , ")"
    /// </summary>
    internal static class InvariantEvaluator
    {
        private const double Epsilon = 1e-9;

        private static readonly HashSet<string> MultiCharOps =
            new HashSet<string> { "==", "!=", ">=", "<=", "&&", "||" };
        private static readonly HashSet<char> SingleCharOps =
            new HashSet<char> { '+', '-', '*', '/', '%', '>', '<', '=', '!' };

        /// <summary>Resolves a document value at a field name, or null if absent.</summary>
        public delegate OdinValue? FieldResolver(string name);

        /// <summary>Outcome of evaluating an invariant expression.</summary>
        public readonly struct InvariantResult
        {
            /// <summary>True/false when fully evaluable; null when an operand field is absent.</summary>
            public bool? Value { get; }

            /// <summary>True if any referenced field is present but null.</summary>
            public bool NullOperand { get; }

            internal InvariantResult(bool? value, bool nullOperand)
            {
                Value = value;
                NullOperand = nullOperand;
            }
        }

        private enum TokenKind { Op, Number, String, Ident, LParen, RParen }

        private readonly struct Token
        {
            public TokenKind Kind { get; }
            public string Text { get; }
            public Token(TokenKind kind, string text) { Kind = kind; Text = text; }
        }

        /// <summary>An operand value: double, string, bool, or null.</summary>
        private readonly struct Operand
        {
            public enum OpKind { Number, String, Bool, Null }
            public OpKind Kind { get; }
            public double Num { get; }
            public string? Str { get; }
            public bool BoolVal { get; }

            private Operand(OpKind kind, double num, string? str, bool b)
            {
                Kind = kind; Num = num; Str = str; BoolVal = b;
            }

            public static Operand Number(double v) => new Operand(OpKind.Number, v, null, false);
            public static Operand String(string v) => new Operand(OpKind.String, 0, v, false);
            public static Operand Bool(bool v) => new Operand(OpKind.Bool, 0, null, v);
            public static readonly Operand Null = new Operand(OpKind.Null, 0, null, false);
        }

        // Parsed expression ASTs cached per expression source string. Parsing is
        // data-independent; evaluation against a resolver runs per document.
        private static readonly ConcurrentDictionary<string, Node> AstCache =
            new ConcurrentDictionary<string, Node>();

        // A parse failure is cached so the FormatException is reproduced without re-parsing.
        private sealed class ParseErrorNode : Node
        {
            public string Message { get; }
            public ParseErrorNode(string message) { Message = message; }
        }

        private static Node ParseToAst(string expr)
        {
            try
            {
                var tokens = Tokenize(expr);
                var parser = new Parser(tokens);
                var node = parser.ParseExpression();
                if (parser.HasMore)
                    throw new FormatException("Unexpected trailing tokens in invariant expression");
                return node;
            }
            catch (FormatException ex)
            {
                return new ParseErrorNode(ex.Message);
            }
        }

        /// <summary>Parse and evaluate an invariant expression against document field values.</summary>
        public static InvariantResult Evaluate(string expr, FieldResolver resolve)
        {
            var ast = AstCache.GetOrAdd(expr, ParseToAst);
            if (ast is ParseErrorNode err)
                throw new FormatException(err.Message);

            var eval = new Evaluator(resolve);
            var final = eval.Eval(ast);

            bool? result;
            if (eval.NullOperand)
                result = false;
            else if (eval.AbsentOperand)
                result = null;
            else
                result = ToBool(final);

            return new InvariantResult(result, eval.NullOperand);
        }

        private static List<Token> Tokenize(string expr)
        {
            var tokens = new List<Token>();
            int i = 0;
            while (i < expr.Length)
            {
                char c = expr[i];
                if (c == ' ' || c == '\t') { i++; continue; }
                if (c == '(') { tokens.Add(new Token(TokenKind.LParen, "(")); i++; continue; }
                if (c == ')') { tokens.Add(new Token(TokenKind.RParen, ")")); i++; continue; }
                if (c == '"' || c == '\'')
                {
                    char quote = c;
                    int j = i + 1;
                    var sb = new System.Text.StringBuilder();
                    while (j < expr.Length && expr[j] != quote) { sb.Append(expr[j]); j++; }
                    if (j >= expr.Length) throw new FormatException("Unterminated string literal");
                    tokens.Add(new Token(TokenKind.String, sb.ToString()));
                    i = j + 1;
                    continue;
                }
                if (i + 1 < expr.Length)
                {
                    var two = expr.Substring(i, 2);
                    if (MultiCharOps.Contains(two)) { tokens.Add(new Token(TokenKind.Op, two)); i += 2; continue; }
                }
                if (SingleCharOps.Contains(c)) { tokens.Add(new Token(TokenKind.Op, c.ToString())); i++; continue; }
                if (c >= '0' && c <= '9')
                {
                    int j = i;
                    while (j < expr.Length && (char.IsDigit(expr[j]) || expr[j] == '.')) j++;
                    tokens.Add(new Token(TokenKind.Number, expr.Substring(i, j - i)));
                    i = j;
                    continue;
                }
                if (char.IsLetter(c) || c == '_')
                {
                    int j = i;
                    while (j < expr.Length && (char.IsLetterOrDigit(expr[j]) || expr[j] == '_' || expr[j] == '.')) j++;
                    tokens.Add(new Token(TokenKind.Ident, expr.Substring(i, j - i)));
                    i = j;
                    continue;
                }
                throw new FormatException(
                    string.Format(CultureInfo.InvariantCulture, "Unexpected character '{0}' in invariant expression", c));
            }
            return tokens;
        }

        // ─────────────────────────────────────────────────────────────────────
        // AST
        // ─────────────────────────────────────────────────────────────────────

        private abstract class Node { }

        private enum NodeOp { LogicOr, LogicAnd, Equality, Comparison, Additive, Multiplicative }

        private sealed class BinaryNode : Node
        {
            public NodeOp Group { get; }
            public string Op { get; }
            public Node Left { get; }
            public Node Right { get; }
            public BinaryNode(NodeOp group, string op, Node left, Node right)
            {
                Group = group; Op = op; Left = left; Right = right;
            }
        }

        private sealed class NotNode : Node
        {
            public Node Operand { get; }
            public NotNode(Node operand) { Operand = operand; }
        }

        private sealed class NumberNode : Node
        {
            public double Value { get; }
            public NumberNode(double value) { Value = value; }
        }

        private sealed class StringNode : Node
        {
            public string Value { get; }
            public StringNode(string value) { Value = value; }
        }

        private sealed class BoolNode : Node
        {
            public bool Value { get; }
            public BoolNode(bool value) { Value = value; }
        }

        private sealed class IdentNode : Node
        {
            public string Name { get; }
            public IdentNode(string name) { Name = name; }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Parser (token stream -> AST)
        // ─────────────────────────────────────────────────────────────────────

        private sealed class Parser
        {
            private readonly List<Token> _tokens;
            private int _pos;

            public bool HasMore => _pos < _tokens.Count;

            public Parser(List<Token> tokens)
            {
                _tokens = tokens;
            }

            private Token Next() => _tokens[_pos++];
            private string? PeekText() => _pos < _tokens.Count ? _tokens[_pos].Text : null;

            public Node ParseExpression() => ParseLogicOr();

            private Node ParseLogicOr()
            {
                var left = ParseLogicAnd();
                while (PeekText() == "||")
                {
                    var op = Next().Text;
                    var right = ParseLogicAnd();
                    left = new BinaryNode(NodeOp.LogicOr, op, left, right);
                }
                return left;
            }

            private Node ParseLogicAnd()
            {
                var left = ParseEquality();
                while (PeekText() == "&&")
                {
                    var op = Next().Text;
                    var right = ParseEquality();
                    left = new BinaryNode(NodeOp.LogicAnd, op, left, right);
                }
                return left;
            }

            private Node ParseEquality()
            {
                var left = ParseComparison();
                while (PeekText() == "==" || PeekText() == "!=" || PeekText() == "=")
                {
                    var op = Next().Text;
                    var right = ParseComparison();
                    left = new BinaryNode(NodeOp.Equality, op, left, right);
                }
                return left;
            }

            private Node ParseComparison()
            {
                var left = ParseAdditive();
                while (PeekText() == ">" || PeekText() == "<" || PeekText() == ">=" || PeekText() == "<=")
                {
                    var op = Next().Text;
                    var right = ParseAdditive();
                    left = new BinaryNode(NodeOp.Comparison, op, left, right);
                }
                return left;
            }

            private Node ParseAdditive()
            {
                var left = ParseMultiplicative();
                while (PeekText() == "+" || PeekText() == "-")
                {
                    var op = Next().Text;
                    var right = ParseMultiplicative();
                    left = new BinaryNode(NodeOp.Additive, op, left, right);
                }
                return left;
            }

            private Node ParseMultiplicative()
            {
                var left = ParseUnary();
                while (PeekText() == "*" || PeekText() == "/" || PeekText() == "%")
                {
                    var op = Next().Text;
                    var right = ParseUnary();
                    left = new BinaryNode(NodeOp.Multiplicative, op, left, right);
                }
                return left;
            }

            private Node ParseUnary()
            {
                if (PeekText() == "!")
                {
                    Next();
                    var operand = ParseUnary();
                    return new NotNode(operand);
                }
                return ParsePrimary();
            }

            private Node ParsePrimary()
            {
                if (!HasMore) throw new FormatException("Unexpected end of invariant expression");
                var tok = Next();

                if (tok.Kind == TokenKind.LParen)
                {
                    var inner = ParseExpression();
                    if (!HasMore || Next().Kind != TokenKind.RParen)
                        throw new FormatException("Expected closing parenthesis");
                    return inner;
                }
                if (tok.Kind == TokenKind.Number)
                {
                    if (!double.TryParse(tok.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var n))
                        throw new FormatException(
                            string.Format(CultureInfo.InvariantCulture, "Invalid number '{0}'", tok.Text));
                    return new NumberNode(n);
                }
                if (tok.Kind == TokenKind.String)
                    return new StringNode(tok.Text);
                if (tok.Kind == TokenKind.Ident)
                {
                    if (tok.Text == "true") return new BoolNode(true);
                    if (tok.Text == "false") return new BoolNode(false);
                    return new IdentNode(tok.Text);
                }
                throw new FormatException(
                    string.Format(CultureInfo.InvariantCulture, "Unexpected token '{0}'", tok.Text));
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Evaluator (AST + resolver -> Operand)
        // ─────────────────────────────────────────────────────────────────────

        private sealed class Evaluator
        {
            private readonly FieldResolver _resolve;

            public bool AbsentOperand { get; private set; }
            public bool NullOperand { get; private set; }

            public Evaluator(FieldResolver resolve)
            {
                _resolve = resolve;
            }

            public Operand Eval(Node node)
            {
                switch (node)
                {
                    case NumberNode num: return Operand.Number(num.Value);
                    case StringNode str: return Operand.String(str.Value);
                    case BoolNode b: return Operand.Bool(b.Value);
                    case IdentNode id:
                    {
                        var value = _resolve(id.Name);
                        if (value == null)
                        {
                            AbsentOperand = true;
                            return Operand.Number(double.NaN);
                        }
                        if (value.IsNull)
                        {
                            NullOperand = true;
                            return Operand.Null;
                        }
                        return OperandFromValue(value);
                    }
                    case NotNode notNode:
                        return Operand.Bool(!ToBool(Eval(notNode.Operand)));
                    case BinaryNode bin:
                        return EvalBinary(bin);
                }
                throw new FormatException("Invalid invariant AST node");
            }

            private Operand EvalBinary(BinaryNode bin)
            {
                // Both operands are always evaluated so absent/null flags are observed
                // for every referenced field, matching non-short-circuiting semantics.
                var left = Eval(bin.Left);
                var right = Eval(bin.Right);
                switch (bin.Group)
                {
                    case NodeOp.LogicOr:
                        return Operand.Bool(ToBool(left) || ToBool(right));
                    case NodeOp.LogicAnd:
                        return Operand.Bool(ToBool(left) && ToBool(right));
                    case NodeOp.Equality:
                    {
                        bool eq = LooseEquals(left, right);
                        return Operand.Bool(bin.Op == "!=" ? !eq : eq);
                    }
                    case NodeOp.Comparison:
                        return Operand.Bool(Compare(left, bin.Op, right));
                    case NodeOp.Additive:
                    {
                        var ln = ToNum(left);
                        var rn = ToNum(right);
                        if (!ln.HasValue || !rn.HasValue)
                            return Operand.Number(double.NaN);
                        return Operand.Number(bin.Op == "+" ? ln.Value + rn.Value : ln.Value - rn.Value);
                    }
                    case NodeOp.Multiplicative:
                    {
                        var ln = ToNum(left);
                        var rn = ToNum(right);
                        if (!ln.HasValue || !rn.HasValue)
                            return Operand.Number(double.NaN);
                        if (bin.Op == "*")
                            return Operand.Number(ln.Value * rn.Value);
                        if (bin.Op == "/")
                            return Operand.Number(rn.Value == 0 ? double.NaN : ln.Value / rn.Value);
                        return Operand.Number(rn.Value == 0 ? double.NaN : ln.Value % rn.Value);
                    }
                }
                throw new FormatException("Invalid invariant operator group");
            }
        }

        private static Operand OperandFromValue(OdinValue value)
        {
            switch (value)
            {
                case OdinNumber n: return Operand.Number(n.Value);
                case OdinInteger i: return Operand.Number(i.Value);
                case OdinCurrency c: return Operand.Number(c.Value);
                case OdinPercent p: return Operand.Number(p.Value);
                case OdinString s: return Operand.String(s.Value);
                case OdinBoolean b: return Operand.Bool(b.Value);
                case OdinDate d:
                {
                    var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                    var dt = new DateTime(d.Year, d.Month, d.Day, 0, 0, 0, DateTimeKind.Utc);
                    return Operand.Number((dt - epoch).TotalMilliseconds);
                }
                case OdinTimestamp ts:
                    return Operand.Number(ts.EpochMs);
                default: return Operand.Number(double.NaN);
            }
        }

        private static double? ToNum(Operand v)
        {
            switch (v.Kind)
            {
                case Operand.OpKind.Number: return double.IsNaN(v.Num) ? (double?)null : v.Num;
                case Operand.OpKind.Bool: return v.BoolVal ? 1 : 0;
                default: return null;
            }
        }

        private static bool ToBool(Operand v)
        {
            switch (v.Kind)
            {
                case Operand.OpKind.Bool: return v.BoolVal;
                case Operand.OpKind.Number: return !double.IsNaN(v.Num) && v.Num != 0;
                case Operand.OpKind.String: return v.Str!.Length > 0;
                default: return false;
            }
        }

        private static bool LooseEquals(Operand a, Operand b)
        {
            if (a.Kind == Operand.OpKind.Number && b.Kind == Operand.OpKind.Number)
                return Math.Abs(a.Num - b.Num) < Epsilon;
            if (a.Kind == Operand.OpKind.String && b.Kind == Operand.OpKind.String)
                return a.Str == b.Str;
            if (a.Kind == Operand.OpKind.Bool && b.Kind == Operand.OpKind.Bool)
                return a.BoolVal == b.BoolVal;
            var an = ToNum(a);
            var bn = ToNum(b);
            if (an.HasValue && bn.HasValue)
                return Math.Abs(an.Value - bn.Value) < Epsilon;
            if (a.Kind == Operand.OpKind.Null && b.Kind == Operand.OpKind.Null)
                return true;
            return false;
        }

        private static bool Compare(Operand a, string op, Operand b)
        {
            var an = ToNum(a);
            var bn = ToNum(b);
            if (an.HasValue && bn.HasValue)
            {
                switch (op)
                {
                    case ">": return an.Value > bn.Value;
                    case "<": return an.Value < bn.Value;
                    case ">=": return an.Value >= bn.Value;
                    case "<=": return an.Value <= bn.Value;
                }
            }
            if (a.Kind == Operand.OpKind.String && b.Kind == Operand.OpKind.String)
            {
                int cmp = string.CompareOrdinal(a.Str, b.Str);
                switch (op)
                {
                    case ">": return cmp > 0;
                    case "<": return cmp < 0;
                    case ">=": return cmp >= 0;
                    case "<=": return cmp <= 0;
                }
            }
            return false;
        }
    }
}
