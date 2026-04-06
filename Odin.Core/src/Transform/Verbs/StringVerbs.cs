#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Odin.Core.Types;

using Odin.Core.Transform;

namespace Odin.Core.Transform.Verbs;

/// <summary>
/// String and text analysis verb implementations for the transform engine.
/// Provides 33 string verbs and 3 text analysis verbs: capitalize, titleCase,
/// contains, startsWith, endsWith, replace, replaceRegex, padLeft, padRight,
/// pad, truncate, split, join, mask, reverseString, repeat, substring, length,
/// camelCase, snakeCase, kebabCase, pascalCase, slugify, match, extract,
/// normalizeSpace, leftOf, rightOf, wrap, center, matches, stripAccents, clean,
/// wordCount, tokenize, levenshtein, soundex.
/// </summary>
internal static class StringVerbs
{
    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private static string ToStr(DynValue v)
    {
        switch (v.Type)
        {
            case DynValueType.String:
            case DynValueType.Date:
            case DynValueType.Timestamp:
            case DynValueType.Time:
            case DynValueType.Duration:
            case DynValueType.Reference:
            case DynValueType.Binary:
            case DynValueType.FloatRaw:
            case DynValueType.CurrencyRaw:
                return v.AsString() ?? "";
            case DynValueType.Integer:
                return v.AsInt64()?.ToString(CultureInfo.InvariantCulture) ?? "";
            case DynValueType.Float:
            case DynValueType.Currency:
            case DynValueType.Percent:
                return v.AsDouble()?.ToString(CultureInfo.InvariantCulture) ?? "";
            case DynValueType.Bool:
                return v.AsBool() == true ? "true" : "false";
            case DynValueType.Null:
                return "";
            default:
                return "";
        }
    }

    private static int ToInt(DynValue v, int fallback = 0)
    {
        var d = v.AsDouble();
        if (d.HasValue) return (int)d.Value;
        var i = v.AsInt64();
        if (i.HasValue) return (int)i.Value;
        var s = v.AsString();
        if (s != null && int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
            return parsed;
        return fallback;
    }

    /// <summary>
    /// Split a string into word boundaries for case conversion.
    /// Splits on whitespace, hyphens, underscores, and camelCase boundaries.
    /// </summary>
    private static List<string> SplitWords(string s)
    {
        var words = new List<string>();
        var current = new StringBuilder();
        var chars = s.ToCharArray();
        int len = chars.Length;

        for (int i = 0; i < len; i++)
        {
            char ch = chars[i];
            if (ch == ' ' || ch == '\t' || ch == '_' || ch == '-')
            {
                if (current.Length > 0)
                {
                    words.Add(current.ToString());
                    current.Clear();
                }
            }
            else if (char.IsUpper(ch) && i > 0)
            {
                char prev = chars[i - 1];
                if (char.IsLower(prev) || char.IsDigit(prev))
                {
                    if (current.Length > 0)
                    {
                        words.Add(current.ToString());
                        current.Clear();
                    }
                }
                else if (char.IsUpper(prev))
                {
                    bool nextIsLower = i + 1 < len && char.IsLower(chars[i + 1]);
                    if (nextIsLower && current.Length > 0)
                    {
                        words.Add(current.ToString());
                        current.Clear();
                    }
                }
                current.Append(ch);
            }
            else
            {
                current.Append(ch);
            }
        }
        if (current.Length > 0)
            words.Add(current.ToString());

        return words;
    }

    private static Regex CreateRegex(string pattern)
    {
        return new Regex(pattern, RegexOptions.None, TimeSpan.FromSeconds(1));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Registration
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Registers all string and text analysis verbs into the given dictionary.
    /// </summary>
    public static void Register(Dictionary<string, Func<DynValue[], VerbContext, DynValue>> reg)
    {
        reg["capitalize"] = Capitalize;
        reg["titleCase"] = TitleCase;
        reg["contains"] = Contains;
        reg["startsWith"] = StartsWith;
        reg["endsWith"] = EndsWith;
        reg["replace"] = Replace;
        reg["replaceRegex"] = ReplaceRegex;
        reg["padLeft"] = PadLeft;
        reg["padRight"] = PadRight;
        reg["pad"] = Pad;
        reg["truncate"] = Truncate;
        reg["split"] = Split;
        reg["join"] = Join;
        reg["mask"] = Mask;
        reg["reverseString"] = ReverseString;
        reg["repeat"] = Repeat;
        reg["substring"] = Substring;
        reg["length"] = Length;
        reg["camelCase"] = CamelCase;
        reg["snakeCase"] = SnakeCase;
        reg["kebabCase"] = KebabCase;
        reg["pascalCase"] = PascalCase;
        reg["slugify"] = Slugify;
        reg["match"] = Match;
        reg["extract"] = Extract;
        reg["normalizeSpace"] = NormalizeSpace;
        reg["leftOf"] = LeftOf;
        reg["rightOf"] = RightOf;
        reg["wrap"] = Wrap;
        reg["center"] = Center;
        reg["matches"] = Matches;
        reg["stripAccents"] = StripAccents;
        reg["clean"] = Clean;
        reg["wordCount"] = WordCount;
        reg["tokenize"] = Tokenize;
        reg["levenshtein"] = Levenshtein;
        reg["soundex"] = Soundex;
        reg["formatPhone"] = FormatPhone;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // String Verbs (33)
    // ─────────────────────────────────────────────────────────────────────────

    private static DynValue Capitalize(DynValue[] args, VerbContext ctx)
    {
        if (args.Length < 1) return DynValue.Null();
        if (args[0].IsNull) return DynValue.Null();
        string s = ToStr(args[0]);
        if (s.Length == 0) return DynValue.String("");
        return DynValue.String(char.ToUpperInvariant(s[0]) + s.Substring(1).ToLowerInvariant());
    }

    private static DynValue TitleCase(DynValue[] args, VerbContext ctx)
    {
        if (args.Length < 1) return DynValue.Null();
        if (args[0].IsNull) return DynValue.Null();
        string s = ToStr(args[0]);
        if (s.Length == 0) return DynValue.String("");

        var parts = s.Split(new[] { ' ', '\t' }, StringSplitOptions.None);
        var sb = new StringBuilder();
        for (int i = 0; i < parts.Length; i++)
        {
            if (i > 0) sb.Append(' ');
            string word = parts[i];
            if (word.Length == 0) continue;
            sb.Append(char.ToUpperInvariant(word[0]));
            if (word.Length > 1)
                sb.Append(word.Substring(1));
        }
        return DynValue.String(sb.ToString());
    }

    private static DynValue Contains(DynValue[] args, VerbContext ctx)
    {
        if (args.Length < 2) return DynValue.Bool(false);
        if (args[0].IsNull) return DynValue.Bool(false);
        string s = ToStr(args[0]);
        string sub = ToStr(args[1]);
        return DynValue.Bool(s.IndexOf(sub, StringComparison.Ordinal) >= 0);
    }

    private static DynValue StartsWith(DynValue[] args, VerbContext ctx)
    {
        if (args.Length < 2) return DynValue.Bool(false);
        if (args[0].IsNull) return DynValue.Bool(false);
        string s = ToStr(args[0]);
        string prefix = ToStr(args[1]);
        return DynValue.Bool(s.StartsWith(prefix, StringComparison.Ordinal));
    }

    private static DynValue EndsWith(DynValue[] args, VerbContext ctx)
    {
        if (args.Length < 2) return DynValue.Bool(false);
        if (args[0].IsNull) return DynValue.Bool(false);
        string s = ToStr(args[0]);
        string suffix = ToStr(args[1]);
        return DynValue.Bool(s.EndsWith(suffix, StringComparison.Ordinal));
    }

    private static DynValue Replace(DynValue[] args, VerbContext ctx)
    {
        if (args.Length < 3) return args.Length > 0 ? args[0] : DynValue.Null();
        if (args[0].IsNull) return DynValue.Null();
        string s = ToStr(args[0]);
        string search = ToStr(args[1]);
        string replacement = ToStr(args[2]);
        return DynValue.String(s.Replace(search, replacement));
    }

    private static DynValue ReplaceRegex(DynValue[] args, VerbContext ctx)
    {
        if (args.Length < 3) return args.Length > 0 ? args[0] : DynValue.Null();
        if (args[0].IsNull) return DynValue.Null();
        string s = ToStr(args[0]);
        string pattern = ToStr(args[1]);
        string replacement = ToStr(args[2]);
        try
        {
            var regex = CreateRegex(pattern);
            return DynValue.String(regex.Replace(s, replacement));
        }
        catch (Exception)
        {
            // If regex is invalid, return original string
            return DynValue.String(s);
        }
    }

    private static DynValue PadLeft(DynValue[] args, VerbContext ctx)
    {
        if (args.Length < 2) return args.Length > 0 ? args[0] : DynValue.Null();
        if (args[0].IsNull) return DynValue.Null();
        string s = ToStr(args[0]);
        int width = ToInt(args[1]);
        char padChar = args.Length >= 3 ? GetPadChar(args[2]) : ' ';
        return DynValue.String(s.PadLeft(width, padChar));
    }

    private static DynValue PadRight(DynValue[] args, VerbContext ctx)
    {
        if (args.Length < 2) return args.Length > 0 ? args[0] : DynValue.Null();
        if (args[0].IsNull) return DynValue.Null();
        string s = ToStr(args[0]);
        int width = ToInt(args[1]);
        char padChar = args.Length >= 3 ? GetPadChar(args[2]) : ' ';
        return DynValue.String(s.PadRight(width, padChar));
    }

    private static DynValue Pad(DynValue[] args, VerbContext ctx)
    {
        // pad: center-pad (left+right) to width
        if (args.Length < 2) return args.Length > 0 ? args[0] : DynValue.Null();
        if (args[0].IsNull) return DynValue.Null();
        string s = ToStr(args[0]);
        int width = ToInt(args[1]);
        char padChar = args.Length >= 3 ? GetPadChar(args[2]) : ' ';
        if (s.Length >= width) return DynValue.String(s);
        int totalPad = width - s.Length;
        int leftPad = totalPad / 2;
        int rightPad = totalPad - leftPad;
        return DynValue.String(new string(padChar, leftPad) + s + new string(padChar, rightPad));
    }

    private static char GetPadChar(DynValue v)
    {
        string s = ToStr(v);
        return s.Length > 0 ? s[0] : ' ';
    }

    private static DynValue Truncate(DynValue[] args, VerbContext ctx)
    {
        if (args.Length < 2) return args.Length > 0 ? args[0] : DynValue.Null();
        if (args[0].IsNull) return DynValue.Null();
        string s = ToStr(args[0]);
        int maxLen = ToInt(args[1]);
        string ellipsis = args.Length >= 3 ? ToStr(args[2]) : "";
        if (s.Length <= maxLen) return DynValue.String(s);
        if (ellipsis.Length > 0 && maxLen > ellipsis.Length)
            return DynValue.String(s.Substring(0, maxLen - ellipsis.Length) + ellipsis);
        return DynValue.String(s.Substring(0, maxLen));
    }

    private static DynValue Split(DynValue[] args, VerbContext ctx)
    {
        if (args.Length < 2) return DynValue.Null();
        if (args[0].IsNull) return DynValue.Null();
        string s = ToStr(args[0]);
        string delimiter = ToStr(args[1]);
        string[] parts;
        if (delimiter.Length == 0)
        {
            // Split each character
            parts = new string[s.Length];
            for (int i = 0; i < s.Length; i++)
                parts[i] = s[i].ToString();
        }
        else
        {
            parts = s.Split(new[] { delimiter }, StringSplitOptions.None);
        }

        // If a third argument (index) is provided, return the element at that index
        if (args.Length >= 3)
        {
            int index = ToInt(args[2]);
            // Handle negative index
            if (index < 0) index = parts.Length + index;
            if (index < 0 || index >= parts.Length) return DynValue.Null();
            return DynValue.String(parts[index]);
        }

        var items = new List<DynValue>(parts.Length);
        for (int i = 0; i < parts.Length; i++)
            items.Add(DynValue.String(parts[i]));
        return DynValue.Array(items);
    }

    private static DynValue Join(DynValue[] args, VerbContext ctx)
    {
        if (args.Length < 2) return DynValue.Null();
        var arr = args[0].AsArray() ?? args[0].ExtractArray();
        if (arr == null) return DynValue.Null();
        string delimiter = ToStr(args[1]);
        var sb = new StringBuilder();
        for (int i = 0; i < arr.Count; i++)
        {
            if (i > 0) sb.Append(delimiter);
            sb.Append(ToStr(arr[i]));
        }
        return DynValue.String(sb.ToString());
    }

    private static DynValue Mask(DynValue[] args, VerbContext ctx)
    {
        if (args.Length < 2) return DynValue.Null();
        if (args[0].IsNull) return DynValue.Null();
        string s = ToStr(args[0]);
        string pattern = ToStr(args[1]);

        // If the pattern contains '#', 'A', or '*' placeholders, treat it as a format mask
        // Walk through the pattern: '#'/'A'/'*' consume the next character from the input,
        // any other character is output literally.
        var sb = new System.Text.StringBuilder();
        int valueIndex = 0;
        for (int i = 0; i < pattern.Length && valueIndex < s.Length; i++)
        {
            char maskChar = pattern[i];
            if (maskChar == '#' || maskChar == 'A' || maskChar == '*')
            {
                sb.Append(s[valueIndex]);
                valueIndex++;
            }
            else
            {
                sb.Append(maskChar);
            }
        }
        return DynValue.String(sb.ToString());
    }

    private static DynValue ReverseString(DynValue[] args, VerbContext ctx)
    {
        if (args.Length < 1) return DynValue.Null();
        if (args[0].IsNull) return DynValue.Null();
        string s = ToStr(args[0]);
        char[] chars = s.ToCharArray();
        System.Array.Reverse(chars);
        return DynValue.String(new string(chars));
    }

    private static DynValue Repeat(DynValue[] args, VerbContext ctx)
    {
        if (args.Length < 2) return DynValue.Null();
        if (args[0].IsNull) return DynValue.Null();
        string s = ToStr(args[0]);
        int count = ToInt(args[1]);
        if (count <= 0) return DynValue.String("");
        var sb = new StringBuilder(s.Length * count);
        for (int i = 0; i < count; i++)
            sb.Append(s);
        return DynValue.String(sb.ToString());
    }

    private static DynValue Substring(DynValue[] args, VerbContext ctx)
    {
        if (args.Length < 2) return DynValue.Null();
        if (args[0].IsNull) return DynValue.Null();
        string s = ToStr(args[0]);
        int start = ToInt(args[1]);
        if (start < 0) start = 0;
        if (start >= s.Length) return DynValue.String("");
        if (args.Length >= 3)
        {
            int length = ToInt(args[2]);
            if (length <= 0) return DynValue.String("");
            if (start + length > s.Length) length = s.Length - start;
            return DynValue.String(s.Substring(start, length));
        }
        return DynValue.String(s.Substring(start));
    }

    private static DynValue Length(DynValue[] args, VerbContext ctx)
    {
        if (args.Length < 1) return DynValue.Integer(0);
        if (args[0].IsNull) return DynValue.Integer(0);

        // For arrays, return array length
        var arr = args[0].AsArray();
        if (arr != null) return DynValue.Integer(arr.Count);

        // For objects, return field count
        var obj = args[0].AsObject();
        if (obj != null) return DynValue.Integer(obj.Count);

        // For strings, return string length
        string s = ToStr(args[0]);
        return DynValue.Integer(s.Length);
    }

    private static DynValue CamelCase(DynValue[] args, VerbContext ctx)
    {
        if (args.Length < 1) return DynValue.Null();
        if (args[0].IsNull) return DynValue.Null();
        string s = ToStr(args[0]);
        var words = SplitWords(s);
        if (words.Count == 0) return DynValue.String("");
        var sb = new StringBuilder();
        for (int i = 0; i < words.Count; i++)
        {
            string word = words[i];
            if (word.Length == 0) continue;
            if (i == 0)
            {
                sb.Append(word.ToLowerInvariant());
            }
            else
            {
                sb.Append(char.ToUpperInvariant(word[0]));
                if (word.Length > 1)
                    sb.Append(word.Substring(1).ToLowerInvariant());
            }
        }
        return DynValue.String(sb.ToString());
    }

    private static DynValue SnakeCase(DynValue[] args, VerbContext ctx)
    {
        if (args.Length < 1) return DynValue.Null();
        if (args[0].IsNull) return DynValue.Null();
        string s = ToStr(args[0]);
        var words = SplitWords(s);
        var sb = new StringBuilder();
        for (int i = 0; i < words.Count; i++)
        {
            if (i > 0) sb.Append('_');
            sb.Append(words[i].ToLowerInvariant());
        }
        return DynValue.String(sb.ToString());
    }

    private static DynValue KebabCase(DynValue[] args, VerbContext ctx)
    {
        if (args.Length < 1) return DynValue.Null();
        if (args[0].IsNull) return DynValue.Null();
        string s = ToStr(args[0]);
        var words = SplitWords(s);
        var sb = new StringBuilder();
        for (int i = 0; i < words.Count; i++)
        {
            if (i > 0) sb.Append('-');
            sb.Append(words[i].ToLowerInvariant());
        }
        return DynValue.String(sb.ToString());
    }

    private static DynValue PascalCase(DynValue[] args, VerbContext ctx)
    {
        if (args.Length < 1) return DynValue.Null();
        if (args[0].IsNull) return DynValue.Null();
        string s = ToStr(args[0]);
        var words = SplitWords(s);
        if (words.Count == 0) return DynValue.String("");
        var sb = new StringBuilder();
        for (int i = 0; i < words.Count; i++)
        {
            string word = words[i];
            if (word.Length == 0) continue;
            sb.Append(char.ToUpperInvariant(word[0]));
            if (word.Length > 1)
                sb.Append(word.Substring(1).ToLowerInvariant());
        }
        return DynValue.String(sb.ToString());
    }

    private static DynValue Slugify(DynValue[] args, VerbContext ctx)
    {
        if (args.Length < 1) return DynValue.Null();
        if (args[0].IsNull) return DynValue.Null();
        string s = ToStr(args[0]).ToLowerInvariant();
        // Strip accents first
        s = RemoveAccents(s);
        // Replace non-alphanumeric with hyphens
        var sb = new StringBuilder(s.Length);
        bool lastWasHyphen = false;
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (char.IsLetterOrDigit(c))
            {
                sb.Append(c);
                lastWasHyphen = false;
            }
            else if (!lastWasHyphen && sb.Length > 0)
            {
                sb.Append('-');
                lastWasHyphen = true;
            }
        }
        // Trim trailing hyphen
        string result = sb.ToString().TrimEnd('-');
        return DynValue.String(result);
    }

    private static DynValue Match(DynValue[] args, VerbContext ctx)
    {
        if (args.Length < 2) return DynValue.Null();
        if (args[0].IsNull) return DynValue.Null();
        string s = ToStr(args[0]);
        string pattern = ToStr(args[1]);
        try
        {
            var regex = CreateRegex(pattern);
            var m = regex.Match(s);
            if (!m.Success) return DynValue.Null();
            return DynValue.String(m.Value);
        }
        catch (Exception)
        {
            return DynValue.Null();
        }
    }

    private static DynValue Extract(DynValue[] args, VerbContext ctx)
    {
        if (args.Length < 2) return DynValue.Null();
        if (args[0].IsNull) return DynValue.Null();
        string s = ToStr(args[0]);
        string pattern = ToStr(args[1]);
        try
        {
            var regex = CreateRegex(pattern);
            var m = regex.Match(s);
            if (!m.Success) return DynValue.Null();
            if (m.Groups.Count > 1)
            {
                // Return capture groups as array
                var items = new List<DynValue>();
                for (int i = 1; i < m.Groups.Count; i++)
                    items.Add(m.Groups[i].Success ? DynValue.String(m.Groups[i].Value) : DynValue.Null());
                return DynValue.Array(items);
            }
            return DynValue.String(m.Value);
        }
        catch (Exception)
        {
            return DynValue.Null();
        }
    }

    private static DynValue NormalizeSpace(DynValue[] args, VerbContext ctx)
    {
        if (args.Length < 1) return DynValue.Null();
        if (args[0].IsNull) return DynValue.Null();
        string s = ToStr(args[0]).Trim();
        // Collapse runs of whitespace to single space
        var sb = new StringBuilder(s.Length);
        bool lastWasSpace = false;
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (char.IsWhiteSpace(c))
            {
                if (!lastWasSpace)
                {
                    sb.Append(' ');
                    lastWasSpace = true;
                }
            }
            else
            {
                sb.Append(c);
                lastWasSpace = false;
            }
        }
        return DynValue.String(sb.ToString());
    }

    private static DynValue LeftOf(DynValue[] args, VerbContext ctx)
    {
        if (args.Length < 2) return DynValue.Null();
        if (args[0].IsNull) return DynValue.Null();
        string s = ToStr(args[0]);
        string delimiter = ToStr(args[1]);
        int idx = s.IndexOf(delimiter, StringComparison.Ordinal);
        if (idx < 0) return DynValue.String(s);
        return DynValue.String(s.Substring(0, idx));
    }

    private static DynValue RightOf(DynValue[] args, VerbContext ctx)
    {
        if (args.Length < 2) return DynValue.Null();
        if (args[0].IsNull) return DynValue.Null();
        string s = ToStr(args[0]);
        string delimiter = ToStr(args[1]);
        int idx = s.IndexOf(delimiter, StringComparison.Ordinal);
        if (idx < 0) return DynValue.String(s);
        return DynValue.String(s.Substring(idx + delimiter.Length));
    }

    private static DynValue Wrap(DynValue[] args, VerbContext ctx)
    {
        if (args.Length < 2) return args.Length > 0 ? args[0] : DynValue.Null();
        if (args[0].IsNull) return DynValue.Null();
        string s = ToStr(args[0]);
        string prefix = ToStr(args[1]);
        string suffix = args.Length >= 3 ? ToStr(args[2]) : prefix;
        return DynValue.String(prefix + s + suffix);
    }

    private static DynValue Center(DynValue[] args, VerbContext ctx)
    {
        if (args.Length < 2) return args.Length > 0 ? args[0] : DynValue.Null();
        if (args[0].IsNull) return DynValue.Null();
        string s = ToStr(args[0]);
        int width = ToInt(args[1]);
        char padChar = args.Length >= 3 ? GetPadChar(args[2]) : ' ';
        if (s.Length >= width) return DynValue.String(s);
        int totalPad = width - s.Length;
        int leftPad = totalPad / 2;
        int rightPad = totalPad - leftPad;
        return DynValue.String(new string(padChar, leftPad) + s + new string(padChar, rightPad));
    }

    private static DynValue Matches(DynValue[] args, VerbContext ctx)
    {
        if (args.Length < 2) return DynValue.Bool(false);
        if (args[0].IsNull) return DynValue.Bool(false);
        string s = ToStr(args[0]);
        string pattern = ToStr(args[1]);
        try
        {
            var regex = CreateRegex(pattern);
            return DynValue.Bool(regex.IsMatch(s));
        }
        catch (Exception)
        {
            return DynValue.Bool(false);
        }
    }

    private static DynValue StripAccents(DynValue[] args, VerbContext ctx)
    {
        if (args.Length < 1) return DynValue.Null();
        if (args[0].IsNull) return DynValue.Null();
        string s = ToStr(args[0]);
        return DynValue.String(RemoveAccents(s));
    }

    private static string RemoveAccents(string s)
    {
        string normalized = s.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);
        for (int i = 0; i < normalized.Length; i++)
        {
            var uc = CharUnicodeInfo.GetUnicodeCategory(normalized[i]);
            if (uc != UnicodeCategory.NonSpacingMark)
                sb.Append(normalized[i]);
        }
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    private static DynValue Clean(DynValue[] args, VerbContext ctx)
    {
        if (args.Length < 1) return DynValue.Null();
        if (args[0].IsNull) return DynValue.Null();
        string s = ToStr(args[0]);
        var sb = new StringBuilder(s.Length);
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            // Keep printable characters (space and above), tabs, newlines, carriage returns
            if (c >= ' ' || c == '\t' || c == '\n' || c == '\r')
                sb.Append(c);
        }
        return DynValue.String(sb.ToString());
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Text Analysis Verbs (3)
    // ─────────────────────────────────────────────────────────────────────────

    private static DynValue WordCount(DynValue[] args, VerbContext ctx)
    {
        if (args.Length < 1) return DynValue.Integer(0);
        if (args[0].IsNull) return DynValue.Integer(0);
        string s = ToStr(args[0]).Trim();
        if (s.Length == 0) return DynValue.Integer(0);
        int count = 0;
        bool inWord = false;
        for (int i = 0; i < s.Length; i++)
        {
            if (char.IsWhiteSpace(s[i]))
            {
                inWord = false;
            }
            else if (!inWord)
            {
                inWord = true;
                count++;
            }
        }
        return DynValue.Integer(count);
    }

    private static DynValue Tokenize(DynValue[] args, VerbContext ctx)
    {
        if (args.Length < 1) return DynValue.Array(new List<DynValue>());
        if (args[0].IsNull) return DynValue.Array(new List<DynValue>());
        string s = ToStr(args[0]);
        // Split on whitespace into word tokens
        var items = new List<DynValue>();
        var current = new StringBuilder();
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (char.IsWhiteSpace(c))
            {
                if (current.Length > 0)
                {
                    items.Add(DynValue.String(current.ToString()));
                    current.Clear();
                }
            }
            else
            {
                current.Append(c);
            }
        }
        if (current.Length > 0)
            items.Add(DynValue.String(current.ToString()));
        return DynValue.Array(items);
    }

    private static DynValue Levenshtein(DynValue[] args, VerbContext ctx)
    {
        if (args.Length < 2) return DynValue.Integer(0);
        if (args[0].IsNull || args[1].IsNull) return DynValue.Integer(0);
        string a = ToStr(args[0]);
        string b = ToStr(args[1]);

        if (a.Length == 0) return DynValue.Integer(b.Length);
        if (b.Length == 0) return DynValue.Integer(a.Length);

        // Wagner-Fischer algorithm with two-row optimization
        int lenA = a.Length;
        int lenB = b.Length;
        var prev = new int[lenB + 1];
        var curr = new int[lenB + 1];

        for (int j = 0; j <= lenB; j++)
            prev[j] = j;

        for (int i = 1; i <= lenA; i++)
        {
            curr[0] = i;
            for (int j = 1; j <= lenB; j++)
            {
                int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                int del = prev[j] + 1;
                int ins = curr[j - 1] + 1;
                int sub = prev[j - 1] + cost;
                curr[j] = Math.Min(Math.Min(del, ins), sub);
            }
            var tmp = prev;
            prev = curr;
            curr = tmp;
        }
        return DynValue.Integer(prev[lenB]);
    }

    private static DynValue Soundex(DynValue[] args, VerbContext ctx)
    {
        if (args.Length < 1) return DynValue.Null();
        if (args[0].IsNull) return DynValue.Null();
        string s = ToStr(args[0]).Trim();
        if (s.Length == 0) return DynValue.String("");

        // American Soundex algorithm
        var result = new StringBuilder(4);
        char firstLetter = char.ToUpperInvariant(s[0]);
        if (!char.IsLetter(firstLetter))
            return DynValue.String("0000");
        result.Append(firstLetter);

        char prevCode = SoundexCode(firstLetter);
        for (int i = 1; i < s.Length && result.Length < 4; i++)
        {
            char c = char.ToUpperInvariant(s[i]);
            if (!char.IsLetter(c)) continue;
            char code = SoundexCode(c);
            if (code != '0' && code != prevCode)
            {
                result.Append(code);
            }
            prevCode = code;
        }

        while (result.Length < 4)
            result.Append('0');

        return DynValue.String(result.ToString());
    }

    private static char SoundexCode(char c)
    {
        switch (char.ToUpperInvariant(c))
        {
            case 'B': case 'F': case 'P': case 'V': return '1';
            case 'C': case 'G': case 'J': case 'K': case 'Q': case 'S': case 'X': case 'Z': return '2';
            case 'D': case 'T': return '3';
            case 'L': return '4';
            case 'M': case 'N': return '5';
            case 'R': return '6';
            default: return '0'; // A, E, I, O, U, H, W, Y
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // FormatPhone
    // ─────────────────────────────────────────────────────────────────────────

    private static DynValue FormatPhone(DynValue[] args, VerbContext ctx)
    {
        if (args.Length < 2) return DynValue.Null();

        var raw = ToStr(args[0]);
        var country = ToStr(args[1]).ToUpperInvariant();

        // Strip non-digit characters
        var digits = System.Text.RegularExpressions.Regex.Replace(raw, @"\D", "");

        switch (country)
        {
            case "US":
            case "CA":
            {
                var d = digits.Length == 11 && digits[0] == '1' ? digits.Substring(1) : digits;
                if (d.Length != 10) return DynValue.String(raw);
                return DynValue.String($"({d.Substring(0, 3)}) {d.Substring(3, 3)}-{d.Substring(6)}");
            }
            case "GB":
            {
                var d = digits.StartsWith("44", StringComparison.Ordinal) ? digits.Substring(2) : digits;
                if (d.Length < 10 || d.Length > 11) return DynValue.String(raw);
                return DynValue.String($"+44 {d.Substring(0, 4)} {d.Substring(4)}");
            }
            case "DE":
            {
                var d = digits.StartsWith("49", StringComparison.Ordinal) ? digits.Substring(2) : digits;
                if (d.Length < 10 || d.Length > 11) return DynValue.String(raw);
                return DynValue.String($"+49 {d.Substring(0, 4)} {d.Substring(4)}");
            }
            case "FR":
            {
                var d = digits.StartsWith("33", StringComparison.Ordinal) ? digits.Substring(2) : digits;
                if (d.Length != 9) return DynValue.String(raw);
                return DynValue.String($"+33 {d[0]} {d.Substring(1, 2)} {d.Substring(3, 2)} {d.Substring(5, 2)} {d.Substring(7)}");
            }
            case "AU":
            {
                var d = digits.StartsWith("61", StringComparison.Ordinal) ? digits.Substring(2) : digits;
                if (d.Length != 9) return DynValue.String(raw);
                return DynValue.String($"+61 {d[0]} {d.Substring(1, 4)} {d.Substring(5)}");
            }
            case "JP":
            {
                var d = digits.StartsWith("81", StringComparison.Ordinal) ? digits.Substring(2) : digits;
                if (d.Length < 10 || d.Length > 11) return DynValue.String(raw);
                return DynValue.String($"+81 {d.Substring(0, 2)}-{d.Substring(2, 4)}-{d.Substring(6)}");
            }
            default:
                return DynValue.String(raw);
        }
    }
}
