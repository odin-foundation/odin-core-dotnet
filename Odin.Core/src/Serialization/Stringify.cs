using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Odin.Core.Types;

namespace Odin.Core.Serialization;

/// <summary>
/// Serializes an <see cref="OdinDocument"/> back to ODIN text format.
/// </summary>
public static class Stringify
{
    /// <summary>
    /// Serialize an <see cref="OdinDocument"/> to ODIN text.
    /// </summary>
    /// <param name="doc">The document to serialize.</param>
    /// <param name="options">Optional serialization options. Uses defaults if null.</param>
    /// <returns>The ODIN text representation of the document.</returns>
    public static string Serialize(OdinDocument doc, StringifyOptions? options = null)
    {
        var opts = options ?? StringifyOptions.Default;
        // Pre-allocate capacity: ~32 chars per assignment + metadata
        int estimated = doc.Assignments.Count * 32 + doc.Metadata.Count * 32 + 64;
        var sb = new StringBuilder(estimated);

        // Metadata section
        if (opts.IncludeMetadata && doc.Metadata.Count > 0)
        {
            if (opts.Canonical)
            {
                // Canonical mode: metadata emitted as $.key entries, merged below
            }
            else
            {
                sb.Append("{$}\n");
                foreach (var entry in doc.Metadata)
                {
                    sb.Append(entry.Key);
                    sb.Append(" = ");
                    WriteValue(sb, entry.Value, false);
                    sb.Append('\n');
                }
                sb.Append('\n');
            }
        }

        // Build entries list — for sorted/canonical we need a copy; for preserve-order we iterate directly
        IEnumerable<KeyValuePair<string, OdinValue>> entries;
        bool needsSort = !opts.PreserveOrder;
        bool hasMetaDollar = false; // track if any $.key entries exist

        if (opts.Canonical || needsSort)
        {
            var entryList = new List<KeyValuePair<string, OdinValue>>(doc.Assignments.Count + doc.Metadata.Count);
            foreach (var e in doc.Assignments.Entries)
            {
                if (!(e.Key.Length > 1 && e.Key[0] == '$' && e.Key[1] == '.'))
                    entryList.Add(e);
            }
            if (opts.Canonical && opts.IncludeMetadata && doc.Metadata.Count > 0)
            {
                foreach (var entry in doc.Metadata)
                    entryList.Add(new KeyValuePair<string, OdinValue>("$." + entry.Key, entry.Value));
            }
            if (needsSort)
            {
                if (opts.Canonical)
                    entryList.Sort((a, b) => CanonicalPathCompare(a.Key, b.Key));
                else
                    entryList.Sort((a, b) => string.Compare(a.Key, b.Key, StringComparison.Ordinal));
            }
            entries = entryList;
        }
        else
        {
            // Fast path: iterate assignments directly, no copy
            entries = doc.Assignments.Entries;
            hasMetaDollar = true; // need to skip $.key inline
        }

        // Group assignments by header sections
        string? currentSection = null;
        bool currentSectionSet = false;

        foreach (var entry in entries)
        {
            var path = entry.Key;
            // Skip $.key entries when iterating directly
            if (hasMetaDollar && path.Length > 1 && path[0] == '$' && path[1] == '.')
                continue;

            var value = entry.Value;

            // Determine section from path
            SplitPath(path, out var section, out var field);

            if (!currentSectionSet || section != currentSection)
            {
                if (section != null)
                {
                    if (sb.Length > 0 && sb[sb.Length - 1] != '\n')
                    {
                        sb.Append('\n');
                    }
                    sb.Append('{');
                    sb.Append(section);
                    sb.Append("}\n");
                }
                currentSection = section;
                currentSectionSet = true;
            }

            // Write field assignment
            sb.Append(field);
            sb.Append(" = ");

            // Write modifiers before the value
            if (value.Modifiers != null)
            {
                WriteModifiers(sb, value.Modifiers);
            }

            WriteValue(sb, value, opts.Canonical);
            sb.Append('\n');
        }

        return sb.ToString();
    }

    /// <summary>
    /// Split a dotted path into an optional section name and a field name.
    /// Only treats the first segment as a section if it starts with an uppercase letter.
    /// </summary>
    internal static void SplitPath(string path, out string? section, out string field)
    {
        int dotPos = path.IndexOf('.');
        if (dotPos > 0 && char.IsUpper(path[0]))
        {
            section = path.Substring(0, dotPos);
            field = path.Substring(dotPos + 1);
            return;
        }
        section = null;
        field = path;
    }

    /// <summary>
    /// Write modifier prefixes in canonical order: ! (required), * (confidential), - (deprecated).
    /// </summary>
    internal static void WriteModifiers(StringBuilder sb, OdinModifiers mods)
    {
        if (mods.Required)
            sb.Append('!');
        if (mods.Confidential)
            sb.Append('*');
        if (mods.Deprecated)
            sb.Append('-');
    }

    /// <summary>
    /// Write an <see cref="OdinValue"/> to the string builder in ODIN text format.
    /// </summary>
    internal static void WriteValue(StringBuilder sb, OdinValue value, bool canonical = false)
    {
        switch (value)
        {
            case OdinNull:
                sb.Append('~');
                break;

            case OdinBoolean b:
                sb.Append(b.Value ? "true" : "false");
                break;

            case OdinString s:
                sb.Append('"');
                WriteEscapedString(sb, s.Value);
                sb.Append('"');
                break;

            case OdinInteger i:
                sb.Append("##");
                if (i.Raw != null)
                    sb.Append(i.Raw);
                else
                    sb.Append(i.Value.ToString(CultureInfo.InvariantCulture));
                break;

            case OdinNumber n:
                sb.Append('#');
                if (canonical)
                {
                    sb.Append(FormatCanonicalNumber(n.Value));
                }
                else if (n.Raw != null)
                {
                    sb.Append(n.Raw);
                }
                else if (n.DecimalPlaces.HasValue)
                {
                    sb.Append(n.Value.ToString("F" + n.DecimalPlaces.Value, CultureInfo.InvariantCulture));
                }
                else
                {
                    sb.Append(n.Value.ToString(CultureInfo.InvariantCulture));
                }
                break;

            case OdinCurrency c:
                sb.Append("#$");
                if (canonical)
                {
                    int dp = Math.Max((int)c.DecimalPlaces, 2);
                    sb.Append(c.Value.ToString("F" + dp, CultureInfo.InvariantCulture));
                    if (c.CurrencyCode != null)
                    {
                        sb.Append(':');
                        sb.Append(c.CurrencyCode.ToUpperInvariant());
                    }
                }
                else if (c.Raw != null)
                {
                    int colonPos = c.Raw.IndexOf(':');
                    if (colonPos >= 0)
                    {
                        sb.Append(c.Raw.Substring(0, colonPos));
                        sb.Append(':');
                        sb.Append(c.Raw.Substring(colonPos + 1).ToUpperInvariant());
                    }
                    else
                    {
                        sb.Append(c.Raw);
                        if (c.CurrencyCode != null)
                        {
                            sb.Append(':');
                            sb.Append(c.CurrencyCode);
                        }
                    }
                }
                else
                {
                    sb.Append(c.Value.ToString("F" + c.DecimalPlaces, CultureInfo.InvariantCulture));
                    if (c.CurrencyCode != null)
                    {
                        sb.Append(':');
                        sb.Append(c.CurrencyCode);
                    }
                }
                break;

            case OdinPercent p:
                sb.Append("#%");
                if (p.Raw != null)
                    sb.Append(p.Raw);
                else
                    sb.Append(p.Value.ToString(CultureInfo.InvariantCulture));
                break;

            case OdinDate d:
                sb.Append(d.Raw);
                break;

            case OdinTimestamp ts:
                sb.Append(ts.Raw);
                break;

            case OdinTime t:
                sb.Append(t.Value);
                break;

            case OdinDuration dur:
                sb.Append(dur.Value);
                break;

            case OdinReference r:
                sb.Append('@');
                sb.Append(r.Path);
                break;

            case OdinBinary bin:
                sb.Append('^');
                if (bin.Algorithm != null)
                {
                    sb.Append(bin.Algorithm);
                    sb.Append(':');
                }
                sb.Append(Convert.ToBase64String(bin.Data));
                break;

            case OdinVerb v:
                sb.Append('%');
                if (v.IsCustom)
                    sb.Append('&');
                sb.Append(v.Name);
                foreach (var arg in v.Args)
                {
                    sb.Append(' ');
                    WriteValue(sb, arg);
                }
                break;

            case OdinArray arr:
                for (int idx = 0; idx < arr.Items.Count; idx++)
                {
                    if (idx > 0)
                        sb.Append(", ");

                    var item = arr.Items[idx];
                    var itemValue = item.AsValue();
                    if (itemValue != null)
                    {
                        WriteValue(sb, itemValue);
                    }
                    else
                    {
                        sb.Append("{...}");
                    }
                }
                break;

            case OdinObject:
                sb.Append("{...}");
                break;

            default:
                sb.Append(value.ToString());
                break;
        }

        // Write trailing directives
        foreach (var directive in value.Directives)
        {
            WriteDirective(sb, directive);
        }
    }

    /// <summary>
    /// Write a single directive to the string builder.
    /// </summary>
    internal static void WriteDirective(StringBuilder sb, OdinDirective directive)
    {
        sb.Append(" :");
        sb.Append(directive.Name);
        if (directive.Value != null)
        {
            sb.Append(' ');
            var strVal = directive.Value.AsString();
            if (strVal != null)
            {
                sb.Append(strVal);
            }
            else
            {
                var numVal = directive.Value.AsNumber();
                if (numVal.HasValue)
                {
                    sb.Append(numVal.Value.ToString(CultureInfo.InvariantCulture));
                }
            }
        }
    }

    /// <summary>
    /// Write a string with proper escape sequences for ODIN format.
    /// </summary>
    internal static void WriteEscapedString(StringBuilder sb, string s)
    {
        // Fast path: if no special chars, append the whole string at once
        bool needsEscape = false;
        for (int i = 0; i < s.Length; i++)
        {
            char ch = s[i];
            if (ch == '"' || ch == '\\' || ch < ' ')
            {
                needsEscape = true;
                break;
            }
        }
        if (!needsEscape)
        {
            sb.Append(s);
            return;
        }

        // Slow path: scan for runs of safe chars and copy in bulk
        int last = 0;
        for (int i = 0; i < s.Length; i++)
        {
            string? esc;
            char ch = s[i];
            switch (ch)
            {
                case '"': esc = "\\\""; break;
                case '\\': esc = "\\\\"; break;
                case '\n': esc = "\\n"; break;
                case '\r': esc = "\\r"; break;
                case '\t': esc = "\\t"; break;
                default:
                    if (char.IsControl(ch))
                    {
                        sb.Append(s, last, i - last);
                        sb.Append("\\u");
                        sb.Append(((int)ch).ToString("x4", CultureInfo.InvariantCulture));
                        last = i + 1;
                    }
                    continue;
            }
            sb.Append(s, last, i - last);
            sb.Append(esc);
            last = i + 1;
        }
        sb.Append(s, last, s.Length - last);
    }

    /// <summary>
    /// Format a number for canonical output: strip trailing zeros.
    /// </summary>
    private static string FormatCanonicalNumber(double value)
    {
        if (value == Math.Floor(value) && Math.Abs(value) < 1e15)
        {
            return ((long)value).ToString(CultureInfo.InvariantCulture);
        }
        string s = value.ToString("R", CultureInfo.InvariantCulture);
        // If it has a decimal point, strip trailing zeros
        if (s.IndexOf('.') >= 0 && s.IndexOf('E') < 0 && s.IndexOf('e') < 0)
        {
            s = s.TrimEnd('0').TrimEnd('.');
        }
        return s;
    }

    /// <summary>
    /// Compare two paths for canonical sorting with numeric array index support.
    /// </summary>
    private static int CanonicalPathCompare(string a, string b)
    {
        // Fast path: no array brackets means plain lexicographic comparison suffices
        if (a.IndexOf('[') < 0 && b.IndexOf('[') < 0)
            return string.Compare(a, b, StringComparison.Ordinal);

        // Segment-by-segment comparison with numeric array index support
        int aPos = 0, bPos = 0;
        while (aPos < a.Length && bPos < b.Length)
        {
            // Skip leading dots
            if (aPos < a.Length && a[aPos] == '.') aPos++;
            if (bPos < b.Length && b[bPos] == '.') bPos++;
            if (aPos >= a.Length || bPos >= b.Length) break;

            int aStart = aPos, bStart = bPos;

            if (a[aPos] == '[' && b[bPos] == '[')
            {
                // Both are array indices — compare numerically
                int aClose = a.IndexOf(']', aPos);
                int bClose = b.IndexOf(']', bPos);
                if (aClose > aPos + 1 && bClose > bPos + 1)
                {
                    long aVal = 0, bVal = 0;
                    for (int i = aPos + 1; i < aClose; i++) aVal = aVal * 10 + (a[i] - '0');
                    for (int i = bPos + 1; i < bClose; i++) bVal = bVal * 10 + (b[i] - '0');
                    if (aVal != bVal) return aVal.CompareTo(bVal);
                    aPos = aClose + 1;
                    bPos = bClose + 1;
                    continue;
                }
            }

            // Regular segments: find end (dot or bracket)
            while (aPos < a.Length && a[aPos] != '.' && a[aPos] != '[') aPos++;
            while (bPos < b.Length && b[bPos] != '.' && b[bPos] != '[') bPos++;

            int aLen = aPos - aStart, bLen = bPos - bStart;
            int minLen = Math.Min(aLen, bLen);
            for (int i = 0; i < minLen; i++)
            {
                if (a[aStart + i] != b[bStart + i])
                    return a[aStart + i].CompareTo(b[bStart + i]);
            }
            if (aLen != bLen) return aLen.CompareTo(bLen);
        }

        return a.Length.CompareTo(b.Length);
    }
}
