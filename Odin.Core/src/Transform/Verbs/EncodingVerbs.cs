#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Odin.Core.Types;

using Odin.Core.Transform;

namespace Odin.Core.Transform.Verbs;

/// <summary>
/// Encoding and hashing verbs: base64, URL, hex, JSON encoding/decoding,
/// SHA-256, SHA-1, SHA-512, MD5, CRC32, and basic JSON path queries.
/// </summary>
internal static class EncodingVerbs
{
    /// <summary>
    /// Registers all encoding and hashing verbs into the provided dictionary.
    /// </summary>
    /// <param name="reg">The verb registration dictionary.</param>
    public static void Register(Dictionary<string, Func<DynValue[], VerbContext, DynValue>> reg)
    {
        reg["base64Encode"] = Base64Encode;
        reg["base64Decode"] = Base64Decode;
        reg["urlEncode"] = UrlEncode;
        reg["urlDecode"] = UrlDecode;
        reg["jsonEncode"] = JsonEncode;
        reg["jsonDecode"] = JsonDecode;
        reg["hexEncode"] = HexEncode;
        reg["hexDecode"] = HexDecode;
        reg["sha256"] = Sha256;
        reg["sha1"] = Sha1;
        reg["sha512"] = Sha512;
        reg["md5"] = Md5;
        reg["crc32"] = Crc32;
        reg["jsonPath"] = JsonPath;
        reg["base64urlEncode"] = Base64UrlEncode;
        reg["base64urlDecode"] = Base64UrlDecode;
        reg["hmac"] = Hmac;
        reg["parseUrl"] = ParseUrl;
        reg["buildUrl"] = BuildUrl;
        reg["parseQuery"] = ParseQuery;
        reg["buildQuery"] = BuildQuery;
        reg["stableStringify"] = StableStringify;
        reg["canonicalHash"] = CanonicalHash;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private static string? CoerceStr(DynValue v)
    {
        if (v.IsNull) return null;
        var s = v.AsString();
        if (s != null) return s;
        var i = v.AsInt64();
        if (i.HasValue) return i.Value.ToString(CultureInfo.InvariantCulture);
        var d = v.AsDouble();
        if (d.HasValue) return d.Value.ToString(CultureInfo.InvariantCulture);
        var b = v.AsBool();
        if (b.HasValue) return b.Value ? "true" : "false";
        return v.ToString();
    }

    private static string BytesToHex(byte[] bytes)
    {
        var sb = new StringBuilder(bytes.Length * 2);
        for (int i = 0; i < bytes.Length; i++)
            sb.Append(bytes[i].ToString("x2", CultureInfo.InvariantCulture));
        return sb.ToString();
    }

    private static byte[] HexToBytes(string hex)
    {
        if (hex.Length % 2 != 0)
            hex = "0" + hex;
        var bytes = new byte[hex.Length / 2];
        for (int i = 0; i < bytes.Length; i++)
        {
            bytes[i] = byte.Parse(hex.Substring(i * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        }
        return bytes;
    }

    private static string HashWith(System.Security.Cryptography.HashAlgorithm alg, string input)
    {
        using (alg)
        {
            var bytes = Encoding.UTF8.GetBytes(input);
            var hash = alg.ComputeHash(bytes);
            return BytesToHex(hash);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // CRC32 lookup table (for netstandard2.0 compatibility)
    // ─────────────────────────────────────────────────────────────────────────

    private static readonly uint[] Crc32Table = InitCrc32Table();

    private static uint[] InitCrc32Table()
    {
        var table = new uint[256];
        const uint polynomial = 0xEDB88320u;
        for (uint i = 0; i < 256; i++)
        {
            uint crc = i;
            for (int j = 0; j < 8; j++)
            {
                if ((crc & 1) != 0)
                    crc = (crc >> 1) ^ polynomial;
                else
                    crc >>= 1;
            }
            table[i] = crc;
        }
        return table;
    }

    private static uint ComputeCrc32(byte[] data)
    {
#if NET8_0_OR_GREATER
        return System.IO.Hashing.Crc32.HashToUInt32(data);
#else
        uint crc = 0xFFFFFFFFu;
        for (int i = 0; i < data.Length; i++)
        {
            byte index = (byte)((crc ^ data[i]) & 0xFF);
            crc = (crc >> 8) ^ Crc32Table[index];
        }
        return crc ^ 0xFFFFFFFFu;
#endif
    }

    // ─────────────────────────────────────────────────────────────────────────
    // JSON serialization helpers
    // ─────────────────────────────────────────────────────────────────────────

    private static string SerializeDynValue(DynValue v)
    {
        using var stream = new System.IO.MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            WriteDynValue(writer, v);
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void WriteDynValue(Utf8JsonWriter writer, DynValue v)
    {
        switch (v.Type)
        {
            case DynValueType.Null:
                writer.WriteNullValue();
                break;
            case DynValueType.Bool:
                writer.WriteBooleanValue(v.AsBool() ?? false);
                break;
            case DynValueType.Integer:
                writer.WriteNumberValue(v.AsInt64() ?? 0);
                break;
            case DynValueType.Float:
            case DynValueType.Currency:
            case DynValueType.Percent:
                writer.WriteNumberValue(v.AsDouble() ?? 0.0);
                break;
            case DynValueType.Array:
                writer.WriteStartArray();
                var items = v.AsArray();
                if (items != null)
                {
                    for (int i = 0; i < items.Count; i++)
                        WriteDynValue(writer, items[i]);
                }
                writer.WriteEndArray();
                break;
            case DynValueType.Object:
                writer.WriteStartObject();
                var entries = v.AsObject();
                if (entries != null)
                {
                    for (int i = 0; i < entries.Count; i++)
                    {
                        writer.WritePropertyName(entries[i].Key);
                        WriteDynValue(writer, entries[i].Value);
                    }
                }
                writer.WriteEndObject();
                break;
            default:
                var s = v.AsString() ?? v.ToString();
                writer.WriteStringValue(s);
                break;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Verb Implementations
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Encodes a string to Base64.
    /// </summary>
    private static DynValue Base64Encode(DynValue[] args, VerbContext ctx)
    {
        if (args.Length == 0) return DynValue.Null();
        var s = CoerceStr(args[0]);
        if (s == null) return DynValue.Null();
        var bytes = Encoding.UTF8.GetBytes(s);
        return DynValue.String(Convert.ToBase64String(bytes));
    }

    /// <summary>
    /// Decodes a Base64 string back to its original UTF-8 string.
    /// </summary>
    private static DynValue Base64Decode(DynValue[] args, VerbContext ctx)
    {
        if (args.Length == 0) return DynValue.Null();
        var s = CoerceStr(args[0]);
        if (s == null) return DynValue.Null();
        try
        {
            var bytes = Convert.FromBase64String(s);
            return DynValue.String(Encoding.UTF8.GetString(bytes));
        }
        catch (FormatException)
        {
            return DynValue.Null();
        }
    }

    /// <summary>
    /// URL-encodes a string using percent-encoding.
    /// </summary>
    private static DynValue UrlEncode(DynValue[] args, VerbContext ctx)
    {
        if (args.Length == 0) return DynValue.Null();
        var s = CoerceStr(args[0]);
        if (s == null) return DynValue.Null();
        return DynValue.String(Uri.EscapeDataString(s));
    }

    /// <summary>
    /// Decodes a URL-encoded (percent-encoded) string.
    /// </summary>
    private static DynValue UrlDecode(DynValue[] args, VerbContext ctx)
    {
        if (args.Length == 0) return DynValue.Null();
        var s = CoerceStr(args[0]);
        if (s == null) return DynValue.Null();
        return DynValue.String(Uri.UnescapeDataString(s));
    }

    /// <summary>
    /// Serializes a DynValue to its JSON string representation.
    /// </summary>
    private static DynValue JsonEncode(DynValue[] args, VerbContext ctx)
    {
        if (args.Length == 0) return DynValue.Null();
        if (args[0].IsNull) return DynValue.String("null");
        return DynValue.String(SerializeDynValue(args[0]));
    }

    /// <summary>
    /// Parses a JSON string into a DynValue.
    /// </summary>
    private static DynValue JsonDecode(DynValue[] args, VerbContext ctx)
    {
        if (args.Length == 0) return DynValue.Null();
        var s = CoerceStr(args[0]);
        if (s == null) return DynValue.Null();
        try
        {
            using var doc = JsonDocument.Parse(s);
            return DynValue.FromJsonElement(doc.RootElement);
        }
        catch (JsonException)
        {
            return DynValue.Null();
        }
    }

    /// <summary>
    /// Encodes a string's UTF-8 bytes as a hexadecimal string.
    /// </summary>
    private static DynValue HexEncode(DynValue[] args, VerbContext ctx)
    {
        if (args.Length == 0) return DynValue.Null();
        var s = CoerceStr(args[0]);
        if (s == null) return DynValue.Null();
        var bytes = Encoding.UTF8.GetBytes(s);
        return DynValue.String(BytesToHex(bytes));
    }

    /// <summary>
    /// Decodes a hexadecimal string back to its UTF-8 string representation.
    /// </summary>
    private static DynValue HexDecode(DynValue[] args, VerbContext ctx)
    {
        if (args.Length == 0) return DynValue.Null();
        var s = CoerceStr(args[0]);
        if (s == null) return DynValue.Null();
        try
        {
            var bytes = HexToBytes(s);
            return DynValue.String(Encoding.UTF8.GetString(bytes));
        }
        catch (FormatException)
        {
            return DynValue.Null();
        }
    }

    /// <summary>
    /// Computes the SHA-256 hash of a string, returned as a lowercase hex string.
    /// </summary>
    private static DynValue Sha256(DynValue[] args, VerbContext ctx)
    {
        if (args.Length == 0) return DynValue.Null();
        var s = CoerceStr(args[0]);
        if (s == null) return DynValue.Null();
        return DynValue.String(HashWith(System.Security.Cryptography.SHA256.Create(), s));
    }

    /// <summary>
    /// Computes the SHA-1 hash of a string, returned as a lowercase hex string.
    /// </summary>
    private static DynValue Sha1(DynValue[] args, VerbContext ctx)
    {
        if (args.Length == 0) return DynValue.Null();
        var s = CoerceStr(args[0]);
        if (s == null) return DynValue.Null();
        return DynValue.String(HashWith(System.Security.Cryptography.SHA1.Create(), s));
    }

    /// <summary>
    /// Computes the SHA-512 hash of a string, returned as a lowercase hex string.
    /// </summary>
    private static DynValue Sha512(DynValue[] args, VerbContext ctx)
    {
        if (args.Length == 0) return DynValue.Null();
        var s = CoerceStr(args[0]);
        if (s == null) return DynValue.Null();
        return DynValue.String(HashWith(System.Security.Cryptography.SHA512.Create(), s));
    }

    /// <summary>
    /// Computes the MD5 hash of a string, returned as a lowercase hex string.
    /// </summary>
    private static DynValue Md5(DynValue[] args, VerbContext ctx)
    {
        if (args.Length == 0) return DynValue.Null();
        var s = CoerceStr(args[0]);
        if (s == null) return DynValue.Null();
        return DynValue.String(HashWith(System.Security.Cryptography.MD5.Create(), s));
    }

    /// <summary>
    /// Computes the CRC32 checksum of a string, returned as a lowercase hex string.
    /// Uses System.IO.Hashing on .NET 8+, fallback lookup table on netstandard2.0.
    /// </summary>
    private static DynValue Crc32(DynValue[] args, VerbContext ctx)
    {
        if (args.Length == 0) return DynValue.Null();
        var s = CoerceStr(args[0]);
        if (s == null) return DynValue.Null();
        var bytes = Encoding.UTF8.GetBytes(s);
        var crc = ComputeCrc32(bytes);
        return DynValue.String(crc.ToString("x8", CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// Performs a basic JSON path lookup. args[0]=JSON string or DynValue,
    /// args[1]=dot-notation path (e.g., "user.name").
    /// </summary>
    private static DynValue JsonPath(DynValue[] args, VerbContext ctx)
    {
        if (args.Length < 2) return DynValue.Null();

        DynValue root;
        if (args[0].Type == DynValueType.Object || args[0].Type == DynValueType.Array)
        {
            root = args[0];
        }
        else
        {
            var jsonStr = CoerceStr(args[0]);
            if (jsonStr == null) return DynValue.Null();
            try
            {
                using var doc = JsonDocument.Parse(jsonStr);
                root = DynValue.FromJsonElement(doc.RootElement);
            }
            catch (JsonException)
            {
                return DynValue.Null();
            }
        }

        var path = CoerceStr(args[1]);
        if (path == null) return DynValue.Null();

        // Walk dot-separated path segments
        var segments = path.Split('.');
        var current = root;
        for (int i = 0; i < segments.Length; i++)
        {
            var seg = segments[i];
            if (string.IsNullOrEmpty(seg)) continue;
            // Strip the leading root anchor ($ or $[...]).
            if (i == 0 && seg == "$") continue;
            if (i == 0 && seg.Length > 0 && seg[0] == '$') seg = seg.Substring(1);

            // Check for array index: segment[N]
            int bracketPos = seg.IndexOf('[');
            if (bracketPos >= 0 && seg.Length > bracketPos + 1 && seg[seg.Length - 1] == ']')
            {
                var fieldPart = seg.Substring(0, bracketPos);
                var indexStr = seg.Substring(bracketPos + 1, seg.Length - bracketPos - 2);

                if (fieldPart.Length > 0)
                {
                    var fieldVal = current.Get(fieldPart);
                    if (fieldVal == null) return DynValue.Null();
                    current = fieldVal;
                }

                if (int.TryParse(indexStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out int idx))
                {
                    var elem = current.GetIndex(idx);
                    if (elem == null) return DynValue.Null();
                    current = elem;
                }
                else
                {
                    return DynValue.Null();
                }
            }
            else
            {
                var next = current.Get(seg);
                if (next == null) return DynValue.Null();
                current = next;
            }
        }

        return current;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // URL-safe base64, HMAC, URL/query parsing, canonical serialization
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>URL-safe base64 (no padding). args[0]=string.</summary>
    private static DynValue Base64UrlEncode(DynValue[] args, VerbContext ctx)
    {
        if (args.Length == 0) return DynValue.Null();
        var s = CoerceStr(args[0]);
        if (s == null) return DynValue.Null();
        var b64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(s));
        return DynValue.String(b64.Replace('+', '-').Replace('/', '_').TrimEnd('='));
    }

    /// <summary>Decode URL-safe base64. args[0]=string.</summary>
    private static DynValue Base64UrlDecode(DynValue[] args, VerbContext ctx)
    {
        if (args.Length == 0) return DynValue.Null();
        var s = CoerceStr(args[0]);
        if (s == null) return DynValue.Null();
        var padded = s.Replace('-', '+').Replace('_', '/');
        switch (padded.Length % 4)
        {
            case 2: padded += "=="; break;
            case 3: padded += "="; break;
        }
        try
        {
            return DynValue.String(Encoding.UTF8.GetString(Convert.FromBase64String(padded)));
        }
        catch (FormatException)
        {
            return DynValue.Null();
        }
    }

    /// <summary>Keyed hash (default sha256), hex output. args[0]=message, args[1]=key, args[2]=algorithm (optional).</summary>
    private static DynValue Hmac(DynValue[] args, VerbContext ctx)
    {
        if (args.Length < 2) return DynValue.Null();
        var message = CoerceStr(args[0]);
        var key = CoerceStr(args[1]);
        if (message == null || key == null) return DynValue.Null();
        var alg = args.Length >= 3 ? (CoerceStr(args[2]) ?? "sha256").ToLowerInvariant() : "sha256";

        var keyBytes = Encoding.UTF8.GetBytes(key);
        System.Security.Cryptography.HMAC? hmac = alg switch
        {
            "sha256" => new System.Security.Cryptography.HMACSHA256(keyBytes),
            "sha1" => new System.Security.Cryptography.HMACSHA1(keyBytes),
            "sha512" => new System.Security.Cryptography.HMACSHA512(keyBytes),
            "md5" => new System.Security.Cryptography.HMACMD5(keyBytes),
            _ => null,
        };
        if (hmac == null) return DynValue.Null();
        using (hmac)
        {
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(message));
            return DynValue.String(BytesToHex(hash));
        }
    }

    // Build an object from query pairs with keys sorted for canonical output.
    private static DynValue SortedQuery(IEnumerable<KeyValuePair<string, string>> pairs)
    {
        var map = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var p in pairs)
            map[p.Key] = p.Value; // last value wins per key
        var entries = new List<KeyValuePair<string, DynValue>>();
        foreach (var kv in map)
            entries.Add(new KeyValuePair<string, DynValue>(kv.Key, DynValue.String(kv.Value)));
        return DynValue.Object(entries);
    }

    private static List<KeyValuePair<string, string>> ParseQueryPairs(string raw)
    {
        var result = new List<KeyValuePair<string, string>>();
        if (raw.Length == 0) return result;
        foreach (var part in raw.Split('&'))
        {
            if (part.Length == 0) continue;
            int eq = part.IndexOf('=');
            string k = eq >= 0 ? part.Substring(0, eq) : part;
            string v = eq >= 0 ? part.Substring(eq + 1) : "";
            result.Add(new KeyValuePair<string, string>(Uri.UnescapeDataString(k), Uri.UnescapeDataString(v)));
        }
        return result;
    }

    /// <summary>Parse a URL into {scheme, host, port, path, query, fragment}; query keys sorted. args[0]=url.</summary>
    private static DynValue ParseUrl(DynValue[] args, VerbContext ctx)
    {
        if (args.Length == 0) return DynValue.Null();
        var s = CoerceStr(args[0]);
        if (s == null) return DynValue.Null();
        if (!Uri.TryCreate(s, UriKind.Absolute, out var u)) return DynValue.Null();

        var query = u.Query.Length > 0 && u.Query[0] == '?' ? u.Query.Substring(1) : u.Query;
        var entries = new List<KeyValuePair<string, DynValue>>
        {
            new("scheme", DynValue.String(u.Scheme)),
            new("host", DynValue.String(u.Host)),
            new("port", u.IsDefaultPort && !HasExplicitPort(s) ? DynValue.Null() : DynValue.Integer(u.Port)),
            new("path", DynValue.String(u.AbsolutePath)),
            new("query", SortedQuery(ParseQueryPairs(query))),
            new("fragment", DynValue.String(u.Fragment.Length > 0 && u.Fragment[0] == '#' ? u.Fragment.Substring(1) : u.Fragment)),
        };
        return DynValue.Object(entries);
    }

    private static bool HasExplicitPort(string url)
    {
        int schemeEnd = url.IndexOf("://", StringComparison.Ordinal);
        if (schemeEnd < 0) return false;
        int authStart = schemeEnd + 3;
        int authEnd = url.IndexOfAny(new[] { '/', '?', '#' }, authStart);
        var authority = authEnd < 0 ? url.Substring(authStart) : url.Substring(authStart, authEnd - authStart);
        int colon = authority.LastIndexOf(':');
        return colon >= 0 && colon < authority.Length - 1;
    }

    /// <summary>Parse a query string into an object; keys sorted. args[0]=queryString.</summary>
    private static DynValue ParseQuery(DynValue[] args, VerbContext ctx)
    {
        if (args.Length == 0) return DynValue.Null();
        var s = CoerceStr(args[0]);
        if (s == null) return DynValue.Null();
        if (s.Length > 0 && s[0] == '?') s = s.Substring(1);
        return SortedQuery(ParseQueryPairs(s));
    }

    /// <summary>Serialize an object to a query string with keys sorted; null values skipped. args[0]=object.</summary>
    private static DynValue BuildQuery(DynValue[] args, VerbContext ctx)
    {
        if (args.Length == 0) return DynValue.Null();
        var src = args[0].AsObject();
        if (src == null) return DynValue.Null();

        var keys = new List<string>();
        var lookup = new Dictionary<string, DynValue>();
        for (int i = 0; i < src.Count; i++) { keys.Add(src[i].Key); lookup[src[i].Key] = src[i].Value; }
        keys.Sort(StringComparer.Ordinal);

        var sb = new StringBuilder();
        foreach (var key in keys)
        {
            var v = lookup[key];
            if (v.IsNull) continue;
            if (sb.Length > 0) sb.Append('&');
            sb.Append(Uri.EscapeDataString(key)).Append('=').Append(Uri.EscapeDataString(VerbHelpers.CoerceStr(v)));
        }
        return DynValue.String(sb.ToString());
    }

    /// <summary>Inverse of parseUrl; query keys sorted. args[0]=object. Missing scheme/host -> null.</summary>
    private static DynValue BuildUrl(DynValue[] args, VerbContext ctx)
    {
        if (args.Length == 0) return DynValue.Null();
        var src = args[0].AsObject();
        if (src == null) return DynValue.Null();

        string Get(string k)
        {
            for (int i = 0; i < src.Count; i++)
                if (src[i].Key == k)
                    return src[i].Value.IsNull ? "" : VerbHelpers.CoerceStr(src[i].Value);
            return "";
        }

        var scheme = Get("scheme");
        var host = Get("host");
        if (scheme.Length == 0 || host.Length == 0) return DynValue.Null();
        var port = Get("port");
        var path = Get("path");
        var fragment = Get("fragment");

        var sb = new StringBuilder();
        sb.Append(scheme).Append("://").Append(host);
        if (port.Length > 0) sb.Append(':').Append(port);
        if (path.Length > 0) sb.Append(path[0] == '/' ? path : "/" + path);

        DynValue? query = null;
        for (int i = 0; i < src.Count; i++)
            if (src[i].Key == "query") query = src[i].Value;
        if (query != null && query.Type == DynValueType.Object)
        {
            var q = BuildQuery(new[] { query }, ctx);
            var qs = q.AsString();
            if (!string.IsNullOrEmpty(qs)) sb.Append('?').Append(qs);
        }
        if (fragment.Length > 0) sb.Append('#').Append(fragment);
        return DynValue.String(sb.ToString());
    }

    // Recursively serialize to JSON with object keys sorted, for a canonical representation.
    private static void StableJson(DynValue v, StringBuilder sb)
    {
        switch (v.Type)
        {
            case DynValueType.Null:
                sb.Append("null");
                break;
            case DynValueType.Bool:
                sb.Append(v.AsBool()!.Value ? "true" : "false");
                break;
            case DynValueType.Integer:
                sb.Append(v.AsInt64()!.Value.ToString(CultureInfo.InvariantCulture));
                break;
            case DynValueType.Float:
            case DynValueType.Currency:
            case DynValueType.Percent:
            case DynValueType.FloatRaw:
            case DynValueType.CurrencyRaw:
                sb.Append(JsonNumber(v.AsDouble() ?? 0.0));
                break;
            case DynValueType.Array:
            {
                sb.Append('[');
                var items = v.AsArray();
                if (items != null)
                    for (int i = 0; i < items.Count; i++)
                    {
                        if (i > 0) sb.Append(',');
                        StableJson(items[i], sb);
                    }
                sb.Append(']');
                break;
            }
            case DynValueType.Object:
            {
                sb.Append('{');
                var entries = v.AsObject();
                if (entries != null)
                {
                    var keys = new List<string>();
                    var lookup = new Dictionary<string, DynValue>();
                    for (int i = 0; i < entries.Count; i++) { keys.Add(entries[i].Key); lookup[entries[i].Key] = entries[i].Value; }
                    keys.Sort(StringComparer.Ordinal);
                    for (int i = 0; i < keys.Count; i++)
                    {
                        if (i > 0) sb.Append(',');
                        sb.Append(JsonString(keys[i])).Append(':');
                        StableJson(lookup[keys[i]], sb);
                    }
                }
                sb.Append('}');
                break;
            }
            default:
                sb.Append(JsonString(v.AsString() ?? v.ToString()));
                break;
        }
    }

    private static string JsonNumber(double d)
    {
        if (double.IsNaN(d) || double.IsInfinity(d)) return "null";
        if (d == Math.Floor(d) && Math.Abs(d) < 1e15)
            return ((long)d).ToString(CultureInfo.InvariantCulture);
        return d.ToString("R", CultureInfo.InvariantCulture);
    }

    private static string JsonString(string s) => JsonSerializer.Serialize(s);

    /// <summary>Canonical JSON with object keys sorted recursively. args[0]=value.</summary>
    private static DynValue StableStringify(DynValue[] args, VerbContext ctx)
    {
        if (args.Length == 0) return DynValue.Null();
        var sb = new StringBuilder();
        StableJson(args[0], sb);
        return DynValue.String(sb.ToString());
    }

    /// <summary>SHA-256 hex of the canonical (sorted-key) JSON form. args[0]=value.</summary>
    private static DynValue CanonicalHash(DynValue[] args, VerbContext ctx)
    {
        if (args.Length == 0) return DynValue.Null();
        var sb = new StringBuilder();
        StableJson(args[0], sb);
        return DynValue.String(HashWith(System.Security.Cryptography.SHA256.Create(), sb.ToString()));
    }
}
