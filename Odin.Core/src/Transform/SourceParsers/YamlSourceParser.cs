#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using Odin.Core.Types;

namespace Odin.Core.Transform
{
    /// <summary>
    /// Parses simple YAML text into a <see cref="DynValue"/> tree.
    /// Supports key-value mappings, arrays with <c>-</c> prefix, indentation-based nesting,
    /// <c>#</c> comments, quoted strings, and value type inference.
    /// </summary>
    public static class YamlSourceParser
    {
        /// <summary>
        /// Parse YAML text into a <see cref="DynValue"/>.
        /// </summary>
        /// <param name="input">The YAML text to parse.</param>
        /// <returns>A <see cref="DynValue"/> representing the parsed YAML structure.</returns>
        public static DynValue Parse(string input)
        {
            if (string.IsNullOrEmpty(input))
                return DynValue.Object(new List<KeyValuePair<string, DynValue>>());

            var lines = Preprocess(input);
            if (lines.Count == 0)
                return DynValue.Object(new List<KeyValuePair<string, DynValue>>());

            int pos = 0;
            return ParseBlock(lines, ref pos, 0);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Internal types
        // ─────────────────────────────────────────────────────────────────────

        private struct YamlLine
        {
            public int Indent;
            public string Content; // trimmed content (no leading whitespace)
        }

        // ─────────────────────────────────────────────────────────────────────
        // Preprocessing
        // ─────────────────────────────────────────────────────────────────────

        private static List<YamlLine> Preprocess(string input)
        {
            var result = new List<YamlLine>();
            int lineStart = 0;

            while (lineStart <= input.Length)
            {
                int lineEnd = input.IndexOf('\n', lineStart);
                if (lineEnd < 0) lineEnd = input.Length;

                string rawLine;
                if (lineEnd > lineStart && lineEnd - 1 < input.Length && input[lineEnd - 1] == '\r')
                    rawLine = input.Substring(lineStart, lineEnd - lineStart - 1);
                else
                    rawLine = input.Substring(lineStart, lineEnd - lineStart);

                lineStart = lineEnd + 1;

                // Strip inline comments (respecting quotes)
                string content = StripComment(rawLine);
                string trimmed = content.TrimEnd();
                string trimmedStart = trimmed.TrimStart();

                if (trimmedStart.Length == 0)
                    continue;

                int indent = trimmed.Length - trimmedStart.Length;
                result.Add(new YamlLine { Indent = indent, Content = trimmedStart });

                if (lineEnd >= input.Length) break;
            }

            return result;
        }

        private static string StripComment(string line)
        {
            bool inSingle = false;
            bool inDouble = false;
            var sb = new System.Text.StringBuilder(line.Length);

            for (int i = 0; i < line.Length; i++)
            {
                char ch = line[i];
                if (ch == '\'' && !inDouble)
                {
                    inSingle = !inSingle;
                    sb.Append(ch);
                }
                else if (ch == '"' && !inSingle)
                {
                    inDouble = !inDouble;
                    sb.Append(ch);
                }
                else if (ch == '#' && !inSingle && !inDouble)
                {
                    // # is a comment if preceded by whitespace or at start
                    if (sb.Length == 0 || sb[sb.Length - 1] == ' ' || sb[sb.Length - 1] == '\t')
                        break;
                    sb.Append(ch);
                }
                else
                {
                    sb.Append(ch);
                }
            }

            return sb.ToString();
        }

        // ─────────────────────────────────────────────────────────────────────
        // Block parsing
        // ─────────────────────────────────────────────────────────────────────

        private static DynValue ParseBlock(List<YamlLine> lines, ref int pos, int baseIndent)
        {
            if (pos >= lines.Count)
                return DynValue.Object(new List<KeyValuePair<string, DynValue>>());

            var first = lines[pos];
            if (first.Content.StartsWith("- ", StringComparison.Ordinal) || first.Content == "-")
                return ParseArray(lines, ref pos, baseIndent);
            else
                return ParseMapping(lines, ref pos, baseIndent);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Mapping parsing
        // ─────────────────────────────────────────────────────────────────────

        private static DynValue ParseMapping(List<YamlLine> lines, ref int pos, int baseIndent)
        {
            var entries = new List<KeyValuePair<string, DynValue>>();

            while (pos < lines.Count)
            {
                var line = lines[pos];
                if (line.Indent < baseIndent) break;
                if (line.Indent > baseIndent) break;

                string content = line.Content;
                if (content.StartsWith("- ", StringComparison.Ordinal) || content == "-") break;

                int colonPos = FindColon(content);
                if (colonPos >= 0)
                {
                    string key = content.Substring(0, colonPos).Trim();
                    string afterColon = content.Substring(colonPos + 1).Trim();

                    if (afterColon.Length == 0)
                    {
                        // Nested block on subsequent lines
                        pos++;
                        if (pos < lines.Count && lines[pos].Indent > baseIndent)
                        {
                            int childIndent = lines[pos].Indent;
                            var child = ParseBlock(lines, ref pos, childIndent);
                            entries.Add(new KeyValuePair<string, DynValue>(key, child));
                        }
                        else
                        {
                            entries.Add(new KeyValuePair<string, DynValue>(key, DynValue.Null()));
                        }
                    }
                    else
                    {
                        // Inline value
                        entries.Add(new KeyValuePair<string, DynValue>(key, ParseScalar(afterColon)));
                        pos++;
                    }
                }
                else
                {
                    // No colon found — skip
                    pos++;
                }
            }

            return DynValue.Object(entries);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Array parsing
        // ─────────────────────────────────────────────────────────────────────

        private static DynValue ParseArray(List<YamlLine> lines, ref int pos, int baseIndent)
        {
            var items = new List<DynValue>();

            while (pos < lines.Count)
            {
                var line = lines[pos];
                if (line.Indent < baseIndent) break;
                if (line.Indent > baseIndent) break;

                string content = line.Content;
                if (!content.StartsWith("- ", StringComparison.Ordinal) && content != "-") break;

                string afterDash = content == "-" ? "" : content.Substring(2).Trim();

                if (afterDash.Length == 0)
                {
                    // Nested block after bare `-`
                    pos++;
                    if (pos < lines.Count && lines[pos].Indent > baseIndent)
                    {
                        int childIndent = lines[pos].Indent;
                        var child = ParseBlock(lines, ref pos, childIndent);
                        items.Add(child);
                    }
                    else
                    {
                        items.Add(DynValue.Null());
                    }
                }
                else
                {
                    int colonPos = FindColon(afterDash);
                    if (colonPos >= 0)
                    {
                        // Inline mapping item: `- key: value`
                        string key = afterDash.Substring(0, colonPos).Trim();
                        string valStr = afterDash.Substring(colonPos + 1).Trim();

                        var objEntries = new List<KeyValuePair<string, DynValue>>();

                        if (valStr.Length == 0)
                        {
                            pos++;
                            if (pos < lines.Count && lines[pos].Indent > baseIndent)
                            {
                                int childIndent = lines[pos].Indent;
                                var child = ParseBlock(lines, ref pos, childIndent);
                                objEntries.Add(new KeyValuePair<string, DynValue>(key, child));
                            }
                            else
                            {
                                objEntries.Add(new KeyValuePair<string, DynValue>(key, DynValue.Null()));
                            }
                        }
                        else
                        {
                            objEntries.Add(new KeyValuePair<string, DynValue>(key, ParseScalar(valStr)));
                            pos++;
                        }

                        // Consume continuation lines at greater indent that form more
                        // key-value pairs for this same mapping item.
                        int continuationIndent = baseIndent + 2;
                        while (pos < lines.Count && lines[pos].Indent >= continuationIndent)
                        {
                            var cont = lines[pos];
                            if (cont.Indent > continuationIndent) break;

                            int cp = FindColon(cont.Content);
                            if (cp >= 0)
                            {
                                string ck = cont.Content.Substring(0, cp).Trim();
                                string cv = cont.Content.Substring(cp + 1).Trim();

                                if (cv.Length == 0)
                                {
                                    pos++;
                                    if (pos < lines.Count && lines[pos].Indent > continuationIndent)
                                    {
                                        int ci = lines[pos].Indent;
                                        var child = ParseBlock(lines, ref pos, ci);
                                        objEntries.Add(new KeyValuePair<string, DynValue>(ck, child));
                                    }
                                    else
                                    {
                                        objEntries.Add(new KeyValuePair<string, DynValue>(ck, DynValue.Null()));
                                    }
                                }
                                else
                                {
                                    objEntries.Add(new KeyValuePair<string, DynValue>(ck, ParseScalar(cv)));
                                    pos++;
                                }
                            }
                            else
                            {
                                pos++;
                            }
                        }

                        items.Add(DynValue.Object(objEntries));
                    }
                    else
                    {
                        // Simple scalar array item
                        items.Add(ParseScalar(afterDash));
                        pos++;
                    }
                }
            }

            return DynValue.Array(items);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Colon finder (respects quotes)
        // ─────────────────────────────────────────────────────────────────────

        private static int FindColon(string s)
        {
            bool inSingle = false;
            bool inDouble = false;

            for (int i = 0; i < s.Length; i++)
            {
                char ch = s[i];
                if (ch == '\'' && !inDouble)
                    inSingle = !inSingle;
                else if (ch == '"' && !inSingle)
                    inDouble = !inDouble;
                else if (ch == ':' && !inSingle && !inDouble)
                {
                    // Must be followed by space, tab, or end of string
                    if (i + 1 >= s.Length || s[i + 1] == ' ' || s[i + 1] == '\t')
                        return i;
                }
            }

            return -1;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Scalar value parsing
        // ─────────────────────────────────────────────────────────────────────

        private static DynValue ParseScalar(string s)
        {
            string trimmed = s.Trim();
            if (trimmed.Length == 0)
                return DynValue.Null();

            // Quoted strings
            if (trimmed.Length >= 2)
            {
                if ((trimmed[0] == '"' && trimmed[trimmed.Length - 1] == '"') ||
                    (trimmed[0] == '\'' && trimmed[trimmed.Length - 1] == '\''))
                {
                    return DynValue.String(trimmed.Substring(1, trimmed.Length - 2));
                }
            }

            // Null
            if (trimmed == "null" || trimmed == "~")
                return DynValue.Null();

            // Booleans
            if (string.Equals(trimmed, "true", StringComparison.OrdinalIgnoreCase) ||
                trimmed == "yes" || trimmed == "on")
                return DynValue.Bool(true);
            if (string.Equals(trimmed, "false", StringComparison.OrdinalIgnoreCase) ||
                trimmed == "no" || trimmed == "off")
                return DynValue.Bool(false);

            // Integer
            if (long.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out long intVal))
                return DynValue.Integer(intVal);

            // Float
            if (double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out double dblVal))
            {
                if (trimmed.IndexOf('.') >= 0 || trimmed.IndexOf('e') >= 0 || trimmed.IndexOf('E') >= 0)
                    return DynValue.Float(dblVal);
            }

            return DynValue.String(trimmed);
        }
    }
}
