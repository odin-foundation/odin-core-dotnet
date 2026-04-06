using System;

namespace Odin.Core.Validation
{
    /// <summary>
    /// Result of ReDoS (Regular Expression Denial of Service) analysis.
    /// </summary>
    public sealed class RedosAnalysis
    {
        /// <summary>Whether the pattern is considered safe to use.</summary>
        public bool Safe { get; }

        /// <summary>Description of the issue if unsafe; null if safe.</summary>
        public string Reason { get; }

        /// <summary>Estimated complexity score (higher = more complex).</summary>
        public int Complexity { get; }

        /// <summary>Creates a new ReDoS analysis result.</summary>
        public RedosAnalysis(bool safe, string reason, int complexity)
        {
            Safe = safe;
            Reason = reason;
            Complexity = complexity;
        }
    }

    /// <summary>
    /// Analyzes regex patterns for potential ReDoS (Regular Expression Denial of Service) risks.
    /// .NET has built-in regex timeout support via <see cref="System.Text.RegularExpressions.Regex"/>
    /// constructor, so the primary concern is detecting overly complex patterns before compilation.
    /// </summary>
    public static class ReDoSProtection
    {
        /// <summary>Maximum allowed length for a regex pattern.</summary>
        public const int MaxPatternLength = 1024;

        /// <summary>Maximum allowed nesting depth for groups.</summary>
        public const int MaxNestingDepth = 10;

        /// <summary>Maximum number of quantifiers in a pattern.</summary>
        public const int MaxQuantifiers = 20;

        /// <summary>
        /// Analyze a regex pattern for potential ReDoS vulnerabilities.
        /// </summary>
        /// <param name="pattern">The regex pattern to analyze.</param>
        /// <returns>A <see cref="RedosAnalysis"/> indicating whether the pattern is safe.</returns>
        public static RedosAnalysis AnalyzePattern(string pattern)
        {
            if (pattern == null)
                throw new ArgumentNullException(nameof(pattern));

            // Check length
            if (pattern.Length > MaxPatternLength)
            {
                return new RedosAnalysis(
                    false,
                    string.Format(
                        System.Globalization.CultureInfo.InvariantCulture,
                        "Pattern exceeds maximum length ({0} > {1})",
                        pattern.Length, MaxPatternLength),
                    pattern.Length);
            }

            // Check nesting depth
            int maxDepth = CountMaxNesting(pattern);
            if (maxDepth > MaxNestingDepth)
            {
                return new RedosAnalysis(
                    false,
                    string.Format(
                        System.Globalization.CultureInfo.InvariantCulture,
                        "Pattern nesting depth exceeds maximum ({0} > {1})",
                        maxDepth, MaxNestingDepth),
                    maxDepth * 10);
            }

            // Count quantifiers
            int quantifierCount = CountQuantifiers(pattern);
            if (quantifierCount > MaxQuantifiers)
            {
                return new RedosAnalysis(
                    false,
                    string.Format(
                        System.Globalization.CultureInfo.InvariantCulture,
                        "Pattern has too many quantifiers ({0} > {1})",
                        quantifierCount, MaxQuantifiers),
                    quantifierCount * 5);
            }

            // Detect nested quantifiers (a common ReDoS trigger in backtracking engines)
            string nestedReason = DetectNestedQuantifiers(pattern);
            if (nestedReason != null)
            {
                return new RedosAnalysis(false, nestedReason, 50);
            }

            int complexity = quantifierCount * 2 + maxDepth * 3 + pattern.Length / 10;
            return new RedosAnalysis(true, null, complexity);
        }

        /// <summary>
        /// Count maximum nesting depth of groups in a pattern.
        /// </summary>
        internal static int CountMaxNesting(string pattern)
        {
            int depth = 0;
            int maxDepth = 0;
            bool inCharClass = false;
            bool escaped = false;

            for (int i = 0; i < pattern.Length; i++)
            {
                char ch = pattern[i];

                if (escaped)
                {
                    escaped = false;
                    continue;
                }
                if (ch == '\\')
                {
                    escaped = true;
                    continue;
                }
                if (ch == '[' && !inCharClass)
                {
                    inCharClass = true;
                    continue;
                }
                if (ch == ']' && inCharClass)
                {
                    inCharClass = false;
                    continue;
                }
                if (inCharClass)
                    continue;

                if (ch == '(')
                {
                    depth++;
                    if (depth > maxDepth)
                        maxDepth = depth;
                }
                else if (ch == ')')
                {
                    if (depth > 0) depth--;
                }
            }

            return maxDepth;
        }

        /// <summary>
        /// Count quantifiers (+, *, ?, {n,m}) in a pattern.
        /// </summary>
        internal static int CountQuantifiers(string pattern)
        {
            int count = 0;
            bool escaped = false;
            bool inCharClass = false;

            for (int i = 0; i < pattern.Length; i++)
            {
                char ch = pattern[i];

                if (escaped)
                {
                    escaped = false;
                    continue;
                }
                if (ch == '\\')
                {
                    escaped = true;
                    continue;
                }
                if (ch == '[' && !inCharClass)
                {
                    inCharClass = true;
                    continue;
                }
                if (ch == ']' && inCharClass)
                {
                    inCharClass = false;
                    continue;
                }
                if (inCharClass)
                    continue;

                if (ch == '+' || ch == '*' || ch == '?')
                    count++;
                if (ch == '{')
                    count++;
            }

            return count;
        }

        /// <summary>
        /// Detect nested quantifiers (e.g., (a+)+, (a*)*).
        /// These cause catastrophic backtracking in PCRE/.NET engines.
        /// </summary>
        internal static string DetectNestedQuantifiers(string pattern)
        {
            int len = pattern.Length;
            int i = 0;

            // Track whether each group depth has seen a quantifier inside
            var groupHasQuantifier = new System.Collections.Generic.Stack<bool>();
            bool inCharClass = false;

            while (i < len)
            {
                char ch = pattern[i];

                if (ch == '\\')
                {
                    i += 2;
                    continue;
                }
                if (ch == '[' && !inCharClass)
                {
                    inCharClass = true;
                    i++;
                    continue;
                }
                if (ch == ']' && inCharClass)
                {
                    inCharClass = false;
                    i++;
                    continue;
                }
                if (inCharClass)
                {
                    i++;
                    continue;
                }

                if (ch == '(')
                {
                    groupHasQuantifier.Push(false);
                }
                else if (ch == ')')
                {
                    bool innerHasQuant = groupHasQuantifier.Count > 0 && groupHasQuantifier.Pop();
                    // Check if this group is followed by a quantifier
                    if (innerHasQuant && i + 1 < len)
                    {
                        char next = pattern[i + 1];
                        if (next == '+' || next == '*' || next == '{')
                        {
                            return "Pattern contains nested quantifiers (e.g., (a+)+) - flagged for cross-SDK compatibility";
                        }
                    }
                }
                else if (ch == '+' || ch == '*' || ch == '?' || ch == '{')
                {
                    if (groupHasQuantifier.Count > 0)
                    {
                        groupHasQuantifier.Pop();
                        groupHasQuantifier.Push(true);
                    }
                }

                i++;
            }

            return null;
        }
    }
}
