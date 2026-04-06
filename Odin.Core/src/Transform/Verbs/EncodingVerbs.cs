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
}
