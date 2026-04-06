#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using Odin.Core.Types;

using Odin.Core.Transform;

namespace Odin.Core.Transform.Verbs;

/// <summary>
/// Generation verbs: uuid, sequence, resetSequence, nanoid.
/// These verbs produce new unique or sequential values during transform execution.
/// </summary>
internal static class GenerationVerbs
{
    /// <summary>
    /// Registers all generation verbs into the provided dictionary.
    /// </summary>
    /// <param name="reg">The verb registration dictionary.</param>
    public static void Register(Dictionary<string, Func<DynValue[], VerbContext, DynValue>> reg)
    {
        reg["uuid"] = Uuid;
        reg["sequence"] = Sequence;
        reg["resetSequence"] = ResetSequence;
        reg["nanoid"] = Nanoid;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private const string NanoidAlphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz_-";

    [ThreadStatic]
    private static Random? _threadRandom;

    private static Random GetRandom()
    {
        if (_threadRandom == null)
            _threadRandom = new Random();
        return _threadRandom;
    }

    private static double? ToDouble(DynValue v)
    {
        if (v.IsNull) return null;
        var d = v.AsDouble();
        if (d.HasValue) return d.Value;
        var s = v.AsString();
        if (s != null && double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
            return parsed;
        return null;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Verb Implementations
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Generates a UUID v4 string (lowercase, with hyphens).
    /// If a seed string argument is provided, produces a deterministic UUID v5
    /// using dual DJB2 hashes to match TypeScript behavior.
    /// </summary>
    private static DynValue Uuid(DynValue[] args, VerbContext ctx)
    {
        if (args.Length >= 1 && args[0].Type == DynValueType.String)
        {
            var seedStr = args[0].AsString();
            if (seedStr != null)
            {
                return DynValue.String(GenerateSeededUuid(seedStr));
            }
        }

        return DynValue.String(Guid.NewGuid().ToString());
    }

    private static string GenerateSeededUuid(string seed)
    {
        // Two DJB2 hashes
        int hash1 = 5381;
        int hash2 = 52711;
        foreach (char c in seed)
        {
            hash1 = unchecked(((hash1 << 5) + hash1) ^ (int)c);
            hash2 = unchecked(((hash2 << 5) + hash2) ^ (int)c);
        }

        // Generate 16 bytes using signed right shift to match JavaScript's >> behavior
        var bytes = new byte[16];
        for (int i = 0; i < 8; i++)
        {
            bytes[i] = (byte)((hash1 >> (i * 4)) & 0xFF);
            bytes[i + 8] = (byte)((hash2 >> (i * 4)) & 0xFF);
        }

        // Version 5 and variant
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x50);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);

        // Format as hex
        return string.Format(
            CultureInfo.InvariantCulture,
            "{0:x2}{1:x2}{2:x2}{3:x2}-{4:x2}{5:x2}-{6:x2}{7:x2}-{8:x2}{9:x2}-{10:x2}{11:x2}{12:x2}{13:x2}{14:x2}{15:x2}",
            bytes[0], bytes[1], bytes[2], bytes[3],
            bytes[4], bytes[5], bytes[6], bytes[7],
            bytes[8], bytes[9], bytes[10], bytes[11],
            bytes[12], bytes[13], bytes[14], bytes[15]);
    }

    /// <summary>
    /// Reads and increments a named sequence counter stored in ctx.Accumulators.
    /// args[0]=sequence name (optional, defaults to "default").
    /// Returns the current value before incrementing.
    /// </summary>
    private static DynValue Sequence(DynValue[] args, VerbContext ctx)
    {
        string name = "default";
        if (args.Length > 0)
        {
            var n = args[0].AsString();
            if (n != null) name = n;
        }

        string key = "__seq_" + name;
        long current = 0;

        if (ctx.Accumulators.TryGetValue(key, out var existing))
        {
            var val = existing.AsInt64();
            if (val.HasValue) current = val.Value;
            else
            {
                var dVal = existing.AsDouble();
                if (dVal.HasValue) current = (long)dVal.Value;
            }
        }

        long result = current;
        ctx.Accumulators[key] = DynValue.Integer(current + 1);
        return DynValue.Integer(result);
    }

    /// <summary>
    /// Resets a named sequence counter. args[0]=sequence name, args[1]=optional reset value (default 0).
    /// </summary>
    private static DynValue ResetSequence(DynValue[] args, VerbContext ctx)
    {
        string name = "default";
        if (args.Length > 0)
        {
            var n = args[0].AsString();
            if (n != null) name = n;
        }

        long resetTo = 0;
        if (args.Length > 1)
        {
            var v = ToDouble(args[1]);
            if (v.HasValue) resetTo = (long)v.Value;
        }

        string key = "__seq_" + name;
        ctx.Accumulators[key] = DynValue.Integer(resetTo);
        return DynValue.Integer(resetTo);
    }

    /// <summary>
    /// Generates a NanoID-like random string. args[0]=optional length (default 21).
    /// Uses the URL-safe alphabet: 0-9A-Za-z_-.
    /// </summary>
    private static DynValue Nanoid(DynValue[] args, VerbContext ctx)
    {
        int length = 21;
        if (args.Length > 0)
        {
            var v = ToDouble(args[0]);
            if (v.HasValue && v.Value > 0)
                length = (int)v.Value;
        }

        var rng = GetRandom();
        var chars = new char[length];
        for (int i = 0; i < length; i++)
        {
            chars[i] = NanoidAlphabet[rng.Next(NanoidAlphabet.Length)];
        }

        return DynValue.String(new string(chars));
    }
}
