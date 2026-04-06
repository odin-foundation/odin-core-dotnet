#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using Odin.Core.Types;

namespace Odin.Core.Transform
{
    /// <summary>
    /// Parses flat key=value text (one pair per line) into a <see cref="DynValue"/> object.
    /// Supports dot notation for nesting (a.b.c = val), bracket notation for arrays
    /// (items[0].name = foo), comment lines starting with # or ;, and value type inference.
    /// </summary>
    public static class FlatSourceParser
    {
        /// <summary>
        /// Parse flat key=value text into a <see cref="DynValue"/> object.
        /// </summary>
        /// <param name="input">The key=value text to parse, one pair per line.</param>
        /// <returns>A <see cref="DynValue"/> object with nested structure derived from dotted paths.</returns>
        public static DynValue Parse(string input)
        {
            if (string.IsNullOrEmpty(input))
                return DynValue.Object(new List<KeyValuePair<string, DynValue>>());

            var root = new List<KeyValuePair<string, DynValue>>();

            int lineStart = 0;
            while (lineStart <= input.Length)
            {
                int lineEnd = input.IndexOf('\n', lineStart);
                if (lineEnd < 0) lineEnd = input.Length;

                string line;
                if (lineEnd > lineStart && lineEnd - 1 < input.Length && input[lineEnd - 1] == '\r')
                    line = input.Substring(lineStart, lineEnd - lineStart - 1);
                else
                    line = input.Substring(lineStart, lineEnd - lineStart);

                lineStart = lineEnd + 1;

                string trimmed = line.Trim();
                if (trimmed.Length == 0) continue;
                if (trimmed[0] == '#' || trimmed[0] == ';') continue;

                int eqPos = trimmed.IndexOf('=');
                if (eqPos < 0) continue;

                string key = trimmed.Substring(0, eqPos).Trim();
                string rawValue = trimmed.Substring(eqPos + 1).Trim();

                DynValue dynVal;
                if (rawValue.Length == 0 || rawValue == "~")
                {
                    dynVal = DynValue.Null();
                }
                else if (rawValue.Length >= 2 && rawValue[0] == '"' && rawValue[rawValue.Length - 1] == '"')
                {
                    // Quoted string: literal content, no backslash escaping
                    dynVal = DynValue.String(rawValue.Substring(1, rawValue.Length - 2));
                }
                else
                {
                    dynVal = InferType(rawValue);
                }

                SetPath(root, key, dynVal);

                if (lineEnd >= input.Length) break;
            }

            return DynValue.Object(root);
        }

        private static DynValue InferType(string s)
        {
            string trimmed = s.Trim();

            if (trimmed.Length == 0)
                return DynValue.String("");

            if (string.Equals(trimmed, "true", StringComparison.OrdinalIgnoreCase))
                return DynValue.Bool(true);
            if (string.Equals(trimmed, "false", StringComparison.OrdinalIgnoreCase))
                return DynValue.Bool(false);
            if (string.Equals(trimmed, "null", StringComparison.OrdinalIgnoreCase))
                return DynValue.Null();

            if (long.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out long intVal))
                return DynValue.Integer(intVal);

            if (double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out double dblVal))
            {
                if (trimmed.IndexOf('.') >= 0 || trimmed.IndexOf('e') >= 0 || trimmed.IndexOf('E') >= 0)
                    return DynValue.Float(dblVal);
            }

            return DynValue.String(trimmed);
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Path Parsing and Setting
        // ─────────────────────────────────────────────────────────────────────────

        private static void SetPath(List<KeyValuePair<string, DynValue>> root, string path, DynValue value)
        {
            var segments = ParsePath(path);
            if (segments.Count == 0) return;
            SetSegments(root, segments, 0, value);
        }

        private static List<PathSegment> ParsePath(string path)
        {
            var segments = new List<PathSegment>();
            var current = new System.Text.StringBuilder();

            for (int i = 0; i < path.Length; i++)
            {
                char ch = path[i];
                if (ch == '.')
                {
                    if (current.Length > 0)
                    {
                        segments.Add(new PathSegment(current.ToString()));
                        current.Clear();
                    }
                }
                else if (ch == '[')
                {
                    if (current.Length > 0)
                    {
                        segments.Add(new PathSegment(current.ToString()));
                        current.Clear();
                    }
                    var idxStr = new System.Text.StringBuilder();
                    i++;
                    while (i < path.Length && path[i] != ']')
                    {
                        idxStr.Append(path[i]);
                        i++;
                    }
                    // i now points at ']' or end

                    if (int.TryParse(idxStr.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int idx))
                        segments.Add(new PathSegment(idx));
                    else
                        segments.Add(new PathSegment(idxStr.ToString()));
                }
                else
                {
                    current.Append(ch);
                }
            }

            if (current.Length > 0)
                segments.Add(new PathSegment(current.ToString()));

            return segments;
        }

        private static void SetSegments(List<KeyValuePair<string, DynValue>> entries, List<PathSegment> segments, int segIdx, DynValue value)
        {
            if (segIdx >= segments.Count) return;

            var seg = segments[segIdx];

            if (!seg.IsIndex)
            {
                string key = seg.Key!;

                if (segIdx == segments.Count - 1)
                {
                    // Leaf: set value
                    int existing = FindEntry(entries, key);
                    if (existing >= 0)
                    {
                        entries[existing] = new KeyValuePair<string, DynValue>(key, value);
                    }
                    else
                    {
                        entries.Add(new KeyValuePair<string, DynValue>(key, value));
                    }
                }
                else
                {
                    // Non-leaf: descend
                    var nextSeg = segments[segIdx + 1];
                    int existing = FindEntry(entries, key);

                    if (nextSeg.IsIndex)
                    {
                        // Next is array index: ensure this key maps to an array
                        List<DynValue> arr;
                        if (existing >= 0 && entries[existing].Value.AsArray() != null)
                        {
                            arr = entries[existing].Value.AsArray()!;
                        }
                        else
                        {
                            arr = new List<DynValue>();
                            var newVal = DynValue.Array(arr);
                            if (existing >= 0)
                                entries[existing] = new KeyValuePair<string, DynValue>(key, newVal);
                            else
                                entries.Add(new KeyValuePair<string, DynValue>(key, newVal));
                        }
                        SetInArray(arr, segments, segIdx + 1, value);
                    }
                    else
                    {
                        // Next is a key: ensure this key maps to an object
                        List<KeyValuePair<string, DynValue>> obj;
                        if (existing >= 0 && entries[existing].Value.AsObject() != null)
                        {
                            obj = entries[existing].Value.AsObject()!;
                        }
                        else
                        {
                            obj = new List<KeyValuePair<string, DynValue>>();
                            var newVal = DynValue.Object(obj);
                            if (existing >= 0)
                                entries[existing] = new KeyValuePair<string, DynValue>(key, newVal);
                            else
                                entries.Add(new KeyValuePair<string, DynValue>(key, newVal));
                        }
                        SetSegments(obj, segments, segIdx + 1, value);
                    }
                }
            }
        }

        private static void SetInArray(List<DynValue> arr, List<PathSegment> segments, int segIdx, DynValue value)
        {
            if (segIdx >= segments.Count) return;
            var seg = segments[segIdx];
            if (!seg.IsIndex) return;

            int idx = seg.Index;

            // Extend array if needed
            while (arr.Count <= idx)
                arr.Add(DynValue.Null());

            if (segIdx == segments.Count - 1)
            {
                arr[idx] = value;
            }
            else
            {
                var nextSeg = segments[segIdx + 1];
                if (!nextSeg.IsIndex)
                {
                    // Next is a key: ensure element is an object
                    var obj = arr[idx].AsObject();
                    if (obj == null)
                    {
                        obj = new List<KeyValuePair<string, DynValue>>();
                        arr[idx] = DynValue.Object(obj);
                    }
                    SetSegments(obj, segments, segIdx + 1, value);
                }
                else
                {
                    // Next is also an index: ensure element is an array
                    var inner = arr[idx].AsArray();
                    if (inner == null)
                    {
                        inner = new List<DynValue>();
                        arr[idx] = DynValue.Array(inner);
                    }
                    SetInArray(inner, segments, segIdx + 1, value);
                }
            }
        }

        private static int FindEntry(List<KeyValuePair<string, DynValue>> entries, string key)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].Key == key)
                    return i;
            }
            return -1;
        }

        private struct PathSegment
        {
            public readonly string? Key;
            public readonly int Index;
            public readonly bool IsIndex;

            public PathSegment(string key)
            {
                Key = key;
                Index = -1;
                IsIndex = false;
            }

            public PathSegment(int index)
            {
                Key = null;
                Index = index;
                IsIndex = true;
            }
        }
    }
}
