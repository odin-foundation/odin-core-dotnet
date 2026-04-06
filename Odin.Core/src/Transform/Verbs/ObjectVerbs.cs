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
        if (args.Length == 0) return DynValue.Array(new List<DynValue>());
        var obj = args[0].AsObject();
        if (obj == null)
        {
            // Try extracting from string-encoded object
            obj = args[0].ExtractObject();
            if (obj == null) return DynValue.Array(new List<DynValue>());
        }

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
        if (args.Length == 0) return DynValue.Array(new List<DynValue>());
        var obj = args[0].AsObject();
        if (obj == null)
        {
            obj = args[0].ExtractObject();
            if (obj == null) return DynValue.Array(new List<DynValue>());
        }

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
        if (args.Length == 0) return DynValue.Array(new List<DynValue>());
        var obj = args[0].AsObject();
        if (obj == null)
        {
            obj = args[0].ExtractObject();
            if (obj == null) return DynValue.Array(new List<DynValue>());
        }

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
        var obj = args[0].AsObject();
        if (obj == null)
        {
            obj = args[0].ExtractObject();
            if (obj == null) return DynValue.Bool(false);
        }

        var key = args[1].AsString();
        if (key == null) return DynValue.Bool(false);

        for (int i = 0; i < obj.Count; i++)
        {
            if (obj[i].Key == key)
                return DynValue.Bool(true);
        }

        return DynValue.Bool(false);
    }

    /// <summary>
    /// Gets a value from an object by key. args[0]=object, args[1]=key string.
    /// Returns null if the key is not found or the argument is not an object.
    /// </summary>
    private static DynValue Get(DynValue[] args, VerbContext ctx)
    {
        if (args.Length < 2) return DynValue.Null();
        var key = args[1].AsString();
        if (key == null) return DynValue.Null();

        var result = args[0].Get(key);
        if (result != null) return result;

        // Try extracting from string-encoded object
        var obj = args[0].ExtractObject();
        if (obj == null) return DynValue.Null();

        for (int i = 0; i < obj.Count; i++)
        {
            if (obj[i].Key == key)
                return obj[i].Value;
        }

        return DynValue.Null();
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
}
