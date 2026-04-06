using System;
using System.Collections.Generic;
using System.Text;

namespace Odin.Core.Utils;

/// <summary>Utility methods for ODIN path manipulation.</summary>
public static class PathUtils
{
    /// <summary>Build a dotted path from segments.</summary>
    public static string BuildPath(params string[] segments)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < segments.Length; i++)
        {
            if (i > 0) sb.Append('.');
            sb.Append(segments[i]);
        }
        return sb.ToString();
    }

    /// <summary>Build a path with optional array indices.</summary>
    public static string BuildPathWithIndices(params (string name, int? index)[] segments)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < segments.Length; i++)
        {
            if (i > 0) sb.Append('.');
            sb.Append(segments[i].name);
            var idx = segments[i].index;
            if (idx.HasValue)
            {
                sb.Append('[');
                sb.Append(idx.Value);
                sb.Append(']');
            }
        }
        return sb.ToString();
    }

    /// <summary>Split a dotted path into segments, respecting array indices.</summary>
    public static List<string> SplitPath(string path)
    {
        var segments = new List<string>();
        var current = new StringBuilder();

        for (int i = 0; i < path.Length; i++)
        {
            char c = path[i];
            if (c == '.' && current.Length > 0)
            {
                segments.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        if (current.Length > 0)
            segments.Add(current.ToString());

        return segments;
    }

    /// <summary>Get the parent path (everything before the last dot).</summary>
    public static string? ParentPath(string path)
    {
        int lastDot = path.LastIndexOf('.');
        return lastDot > 0 ? path.Substring(0, lastDot) : null;
    }

    /// <summary>Get the leaf name (everything after the last dot).</summary>
    public static string LeafName(string path)
    {
        int lastDot = path.LastIndexOf('.');
        return lastDot >= 0 ? path.Substring(lastDot + 1) : path;
    }

    /// <summary>Check if a path starts with a given prefix.</summary>
    public static bool StartsWith(string path, string prefix)
    {
        if (!path.StartsWith(prefix, StringComparison.Ordinal))
            return false;
        return path.Length == prefix.Length || path[prefix.Length] == '.' || path[prefix.Length] == '[';
    }

    /// <summary>Extract array index from a segment like "items[0]".</summary>
    public static (string name, int? index) ParseSegment(string segment)
    {
        int bracketPos = segment.IndexOf('[');
        if (bracketPos < 0)
            return (segment, null);

        var name = segment.Substring(0, bracketPos);
        var indexStr = segment.Substring(bracketPos + 1, segment.Length - bracketPos - 2);
        if (int.TryParse(indexStr, out var index))
            return (name, index);
        return (segment, null);
    }
}
