#nullable enable
using System;
using System.Text.Json;
using Odin.Core.Types;

namespace Odin.Core.Transform
{
    /// <summary>
    /// Parses a JSON string into a <see cref="DynValue"/> tree.
    /// Uses <see cref="System.Text.Json.JsonDocument"/> for parsing and maps JSON types
    /// to the closest DynValue variant.
    /// </summary>
    public static class JsonSourceParser
    {
        /// <summary>
        /// Parse a JSON string into a <see cref="DynValue"/>.
        /// </summary>
        /// <param name="input">The JSON string to parse.</param>
        /// <returns>A <see cref="DynValue"/> representing the parsed JSON.</returns>
        /// <exception cref="ArgumentException">Thrown when the input is null or empty.</exception>
        /// <exception cref="FormatException">Thrown when the input is not valid JSON.</exception>
        public static DynValue Parse(string input)
        {
            if (string.IsNullOrEmpty(input))
                throw new ArgumentException("JSON input is null or empty.", nameof(input));

            try
            {
                using (var doc = JsonDocument.Parse(input))
                {
                    return DynValue.FromJsonElement(doc.RootElement);
                }
            }
            catch (JsonException ex)
            {
                throw new FormatException("Invalid JSON: " + ex.Message, ex);
            }
        }
    }
}
