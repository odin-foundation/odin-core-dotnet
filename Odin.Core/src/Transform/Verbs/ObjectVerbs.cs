#nullable enable

using System;
using System.Collections.Generic;
using Odin.Core.Types;

using Odin.Core.Transform;

namespace Odin.Core.Transform.Verbs;

/// <summary>
/// Object manipulation verbs. Provides 6 verbs for inspecting and transforming
/// <see cref="DynValue"/> objects (ordered key-value pair collections).
/// </summary>
internal static class ObjectVerbs
{
    /// <summary>
    /// Registers all object verbs into the provided dictionary.
    /// </summary>
    /// <param name="reg">The verb registration dictionary.</param>
    public static void Register(Dictionary<string, Func<DynValue[], VerbContext, DynValue>> reg)
    {
        reg["keys"] = Keys;
        reg["values"] = Values;
        reg["entries"] = Entries;
        reg["has"] = Has;
        reg["get"] = Get;
        reg["merge"] = Merge;
        reg["pick"] = Pick;
        reg["omit"] = Omit;
        reg["fromEntries"] = FromEntries;
        reg["invert"] = Invert;
        reg["defaults"] = Defaults;
        reg["renameKeys"] = RenameKeys;
        reg["compactObject"] = CompactObject;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Prototype-pollution guard
    // ─────────────────────────────────────────────────────────────────────────

    private static bool IsSafeKey(string key)
    {
        var k = key.ToLowerInvariant();
        return k != "__proto__" && k != "constructor" && k != "prototype";
    }

    private static List<KeyValuePair<string, DynValue>>? AsObj(DynValue v)
        => v.AsObject() ?? v.ExtractObject();

    // Walk a dot-separated path of object keys / array indices. Returns null on miss.
    private static DynValue GetNested(DynValue root, string path)
    {
        DynValue current = root;
        foreach (var part in path.Split('.'))
        {
            if (current.Type == DynValueType.Object)
            {
                var next = current.Get(part);
                if (next == null) return DynValue.Null();
                current = next;
            }
            else if (current.Type == DynValueType.Array)
            {
                if (int.TryParse(part, out var idx))
                {
                    var next = current.GetIndex(idx);
                    if (next == null) return DynValue.Null();
                    current = next;
                }
                else return DynValue.Null();
            }
            else return DynValue.Null();
        }
        return current;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Verb Implementations
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns an array of all keys in an object. args[0]=object.
    /// Returns an empty array if the argument is not an object.
    /// </summary>
    private static DynValue Keys(DynValue[] args, VerbContext ctx)
    {
        if (args.Length == 0) return DynValue.Null();
        var obj = args[0].AsObject();
        if (obj == null) return DynValue.Null();

        var result = new List<DynValue>();
        for (int i = 0; i < obj.Count; i++)
            result.Add(DynValue.String(obj[i].Key));

        return DynValue.Array(result);
    }

    /// <summary>
    /// Returns an array of all values in an object. args[0]=object.
    /// Returns an empty array if the argument is not an object.
    /// </summary>
    private static DynValue Values(DynValue[] args, VerbContext ctx)
    {
        if (args.Length == 0) return DynValue.Null();
        var obj = args[0].AsObject();
        if (obj == null) return DynValue.Null();

        var result = new List<DynValue>();
        for (int i = 0; i < obj.Count; i++)
            result.Add(obj[i].Value);

        return DynValue.Array(result);
    }

    /// <summary>
    /// Returns an array of [key, value] pair arrays for each entry in an object. args[0]=object.
    /// Returns an empty array if the argument is not an object.
    /// </summary>
    private static DynValue Entries(DynValue[] args, VerbContext ctx)
    {
        if (args.Length == 0) return DynValue.Null();
        var obj = args[0].AsObject();
        if (obj == null) return DynValue.Null();

        var result = new List<DynValue>();
        for (int i = 0; i < obj.Count; i++)
        {
            var pair = new List<DynValue>
            {
                DynValue.String(obj[i].Key),
                obj[i].Value
            };
            result.Add(DynValue.Array(pair));
        }

        return DynValue.Array(result);
    }

    /// <summary>
    /// Returns true if an object has the specified key. args[0]=object, args[1]=key string.
    /// </summary>
    private static DynValue Has(DynValue[] args, VerbContext ctx)
    {
        if (args.Length < 2) return DynValue.Bool(false);
        if (args[0].Type != DynValueType.Object) return DynValue.Bool(false);

        var key = VerbHelpers.CoerceStr(args[1]);
        if (key.Length == 0 || !IsSafeKey(key)) return DynValue.Bool(false);

        var result = GetNested(args[0], key);
        return DynValue.Bool(!result.IsNull);
    }

    /// <summary>
    /// Gets a value from an object by key. args[0]=object, args[1]=key string.
    /// Returns null if the key is not found or the argument is not an object.
    /// </summary>
    private static DynValue Get(DynValue[] args, VerbContext ctx)
    {
        if (args.Length < 2) return DynValue.Null();
        var fallback = args.Length >= 3 ? args[2] : DynValue.Null();
        if (args[0].Type != DynValueType.Object) return fallback;

        var key = VerbHelpers.CoerceStr(args[1]);
        if (key.Length == 0 || !IsSafeKey(key)) return fallback;

        var result = GetNested(args[0], key);
        if (result.IsNull) return fallback;
        return result;
    }

    /// <summary>
    /// Merges multiple objects into one. Later arguments override earlier ones for
    /// duplicate keys. All arguments should be objects. args = object1, object2, ...
    /// </summary>
    private static DynValue Merge(DynValue[] args, VerbContext ctx)
    {
        if (args.Length == 0) return DynValue.Object(new List<KeyValuePair<string, DynValue>>());

        // Use a dictionary to track keys for override behavior, plus ordered list for output
        var merged = new Dictionary<string, int>();
        var entries = new List<KeyValuePair<string, DynValue>>();

        for (int a = 0; a < args.Length; a++)
        {
            var obj = args[a].AsObject();
            if (obj == null)
            {
                obj = args[a].ExtractObject();
                if (obj == null) continue;
            }

            for (int i = 0; i < obj.Count; i++)
            {
                var key = obj[i].Key;
                if (merged.TryGetValue(key, out var existingIdx))
                {
                    // Override existing entry
                    entries[existingIdx] = new KeyValuePair<string, DynValue>(key, obj[i].Value);
                }
                else
                {
                    merged[key] = entries.Count;
                    entries.Add(new KeyValuePair<string, DynValue>(key, obj[i].Value));
                }
            }
        }

        return DynValue.Object(entries);
    }

    /// <summary>
    /// Returns a new object with only the named keys, in argument order. args[0]=object, args[1..]=keys.
    /// Returns null when the first argument is not an object.
    /// </summary>
    private static DynValue Pick(DynValue[] args, VerbContext ctx)
    {
        if (args.Length == 0) return DynValue.Null();
        var src = AsObj(args[0]);
        if (src == null) return DynValue.Null();

        var lookup = new Dictionary<string, DynValue>();
        for (int i = 0; i < src.Count; i++) lookup[src[i].Key] = src[i].Value;

        var result = new List<KeyValuePair<string, DynValue>>();
        for (int i = 1; i < args.Length; i++)
        {
            var key = VerbHelpers.CoerceStr(args[i]);
            if (IsSafeKey(key) && lookup.TryGetValue(key, out var v))
                result.Add(new KeyValuePair<string, DynValue>(key, v));
        }
        return DynValue.Object(result);
    }

    /// <summary>
    /// Returns a new object without the named keys, preserving source order. args[0]=object, args[1..]=keys.
    /// </summary>
    private static DynValue Omit(DynValue[] args, VerbContext ctx)
    {
        if (args.Length == 0) return DynValue.Null();
        var src = AsObj(args[0]);
        if (src == null) return DynValue.Null();

        var drop = new HashSet<string>();
        for (int i = 1; i < args.Length; i++) drop.Add(VerbHelpers.CoerceStr(args[i]));

        var result = new List<KeyValuePair<string, DynValue>>();
        for (int i = 0; i < src.Count; i++)
        {
            var k = src[i].Key;
            if (IsSafeKey(k) && !drop.Contains(k))
                result.Add(src[i]);
        }
        return DynValue.Object(result);
    }

    /// <summary>
    /// Builds an object from an array of [key, value] pairs (pair order). args[0]=pairs.
    /// </summary>
    private static DynValue FromEntries(DynValue[] args, VerbContext ctx)
    {
        if (args.Length == 0) return DynValue.Null();
        var pairs = args[0].AsArray() ?? args[0].ExtractArray();
        if (pairs == null) return DynValue.Null();

        var result = new List<KeyValuePair<string, DynValue>>();
        var index = new Dictionary<string, int>();
        for (int i = 0; i < pairs.Count; i++)
        {
            var pair = pairs[i].AsArray() ?? pairs[i].ExtractArray();
            if (pair == null || pair.Count < 2) continue;
            var key = VerbHelpers.CoerceStr(pair[0]);
            if (!IsSafeKey(key)) continue;
            if (index.TryGetValue(key, out var at))
                result[at] = new KeyValuePair<string, DynValue>(key, pair[1]);
            else
            {
                index[key] = result.Count;
                result.Add(new KeyValuePair<string, DynValue>(key, pair[1]));
            }
        }
        return DynValue.Object(result);
    }

    /// <summary>
    /// Swaps keys and values; values become string keys (last wins on duplicates). args[0]=object.
    /// </summary>
    private static DynValue Invert(DynValue[] args, VerbContext ctx)
    {
        if (args.Length == 0) return DynValue.Null();
        var src = AsObj(args[0]);
        if (src == null) return DynValue.Null();

        var result = new List<KeyValuePair<string, DynValue>>();
        var index = new Dictionary<string, int>();
        for (int i = 0; i < src.Count; i++)
        {
            var newKey = VerbHelpers.CoerceStr(src[i].Value);
            if (!IsSafeKey(newKey)) continue;
            var val = DynValue.String(src[i].Key);
            if (index.TryGetValue(newKey, out var at))
                result[at] = new KeyValuePair<string, DynValue>(newKey, val);
            else
            {
                index[newKey] = result.Count;
                result.Add(new KeyValuePair<string, DynValue>(newKey, val));
            }
        }
        return DynValue.Object(result);
    }

    /// <summary>
    /// Fills keys missing from the object using a defaults object (object wins).
    /// args[0]=object, args[1]=defaults.
    /// </summary>
    private static DynValue Defaults(DynValue[] args, VerbContext ctx)
    {
        if (args.Length < 2) return DynValue.Null();
        var src = AsObj(args[0]);
        var def = AsObj(args[1]);
        if (src == null) return def != null ? args[1] : DynValue.Null();
        if (def == null) return args[0];

        var result = new List<KeyValuePair<string, DynValue>>();
        var present = new HashSet<string>();
        for (int i = 0; i < src.Count; i++)
        {
            if (!IsSafeKey(src[i].Key)) continue;
            result.Add(src[i]);
            present.Add(src[i].Key);
        }
        for (int i = 0; i < def.Count; i++)
        {
            if (!IsSafeKey(def[i].Key)) continue;
            if (!present.Contains(def[i].Key))
            {
                result.Add(def[i]);
                present.Add(def[i].Key);
            }
        }
        return DynValue.Object(result);
    }

    /// <summary>
    /// Renames keys named in the mapping (old -> new), keeping position.
    /// args[0]=object, args[1]=mapping object.
    /// </summary>
    private static DynValue RenameKeys(DynValue[] args, VerbContext ctx)
    {
        if (args.Length < 2) return DynValue.Null();
        var src = AsObj(args[0]);
        if (src == null) return DynValue.Null();
        var map = AsObj(args[1]);
        if (map == null) return args[0];

        var rename = new Dictionary<string, string>();
        for (int i = 0; i < map.Count; i++)
            rename[map[i].Key] = VerbHelpers.CoerceStr(map[i].Value);

        var result = new List<KeyValuePair<string, DynValue>>();
        for (int i = 0; i < src.Count; i++)
        {
            var newKey = rename.TryGetValue(src[i].Key, out var nk) ? nk : src[i].Key;
            if (IsSafeKey(newKey))
                result.Add(new KeyValuePair<string, DynValue>(newKey, src[i].Value));
        }
        return DynValue.Object(result);
    }

    /// <summary>
    /// Drops entries whose value is null, empty string, empty array, or empty object. args[0]=object.
    /// </summary>
    private static DynValue CompactObject(DynValue[] args, VerbContext ctx)
    {
        if (args.Length == 0) return DynValue.Null();
        var src = AsObj(args[0]);
        if (src == null) return DynValue.Null();

        var result = new List<KeyValuePair<string, DynValue>>();
        for (int i = 0; i < src.Count; i++)
        {
            var v = src[i].Value;
            if (v.IsNull) continue;
            if (v.Type == DynValueType.String && v.AsString() == "") continue;
            if (v.Type == DynValueType.Array && (v.AsArray()?.Count ?? 0) == 0) continue;
            if (v.Type == DynValueType.Object && (v.AsObject()?.Count ?? 0) == 0) continue;
            result.Add(src[i]);
        }
        return DynValue.Object(result);
    }
}
