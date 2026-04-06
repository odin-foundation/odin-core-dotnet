#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using Odin.Core.Types;

namespace Odin.Core.Transform
{
    /// <summary>
    /// Parses an XML string into a <see cref="DynValue"/> tree.
    /// Uses XmlReader to preserve self-closing vs empty element distinction:
    /// <c>&lt;tag/&gt;</c> → null, <c>&lt;tag&gt;&lt;/tag&gt;</c> → empty string.
    /// Repeated sibling elements with the same name become arrays.
    /// Attributes are prefixed with "@".
    /// </summary>
    public static class XmlSourceParser
    {
        /// <summary>
        /// Parse an XML string into a <see cref="DynValue"/>.
        /// </summary>
        /// <param name="input">The XML string to parse.</param>
        /// <returns>A <see cref="DynValue"/> representing the parsed XML.</returns>
        /// <exception cref="ArgumentException">Thrown when the input is null or empty.</exception>
        /// <exception cref="FormatException">Thrown when the input is not valid XML.</exception>
        public static DynValue Parse(string input)
        {
            if (string.IsNullOrEmpty(input))
                throw new ArgumentException("XML input is null or empty.", nameof(input));

            XNode root;
            try
            {
                root = ReadXml(input);
            }
            catch (XmlException ex)
            {
                throw new FormatException("Invalid XML: " + ex.Message, ex);
            }

            if (root == null)
                throw new FormatException("Empty XML document.");

            var rootValue = NodeToValue(root, 0);
            var entries = new List<KeyValuePair<string, DynValue>>
            {
                new KeyValuePair<string, DynValue>(root.Name, rootValue)
            };
            return DynValue.Object(entries);
        }

        // Internal node representation that preserves self-closing flag
        private sealed class XNode
        {
            public string Name = "";
            public List<KeyValuePair<string, string>> Attributes = new();
            public List<XNode> Children = new();
            public string Text = "";
            public bool SelfClosing;
        }

        private static XNode ReadXml(string input)
        {
            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Ignore,
                IgnoreComments = true,
                IgnoreProcessingInstructions = true,
            };

            using var reader = XmlReader.Create(new StringReader(input), settings);

            // Advance to root element
            while (reader.Read())
            {
                if (reader.NodeType == XmlNodeType.Element)
                    return ReadElement(reader);
            }

            throw new FormatException("No root element found.");
        }

        private static XNode ReadElement(XmlReader reader)
        {
            var node = new XNode
            {
                Name = reader.Name,  // Include namespace prefix (e.g., "soapenv:Envelope")
                SelfClosing = reader.IsEmptyElement,
            };

            // Read attributes
            if (reader.HasAttributes)
            {
                for (int i = 0; i < reader.AttributeCount; i++)
                {
                    reader.MoveToAttribute(i);
                    // Skip namespace declarations (xmlns:...)
                    if (reader.Prefix == "xmlns" || (reader.Prefix == "" && reader.LocalName == "xmlns"))
                        continue;
                    node.Attributes.Add(new KeyValuePair<string, string>(
                        reader.LocalName, reader.Value));
                }
                reader.MoveToElement();
            }

            // Self-closing: no content to read
            if (reader.IsEmptyElement)
                return node;

            // Read children and text content
            var textParts = new List<string>();
            while (reader.Read())
            {
                switch (reader.NodeType)
                {
                    case XmlNodeType.Element:
                        node.Children.Add(ReadElement(reader));
                        break;
                    case XmlNodeType.Text:
                    case XmlNodeType.CDATA:
                    case XmlNodeType.SignificantWhitespace:
                        textParts.Add(reader.Value);
                        break;
                    case XmlNodeType.EndElement:
                        if (textParts.Count > 0)
                            node.Text = string.Join("", textParts).Trim();
                        return node;
                }
            }

            if (textParts.Count > 0)
                node.Text = string.Join("", textParts).Trim();
            return node;
        }

        private static DynValue NodeToValue(XNode node, int depth)
        {
            if (depth > 100)
                throw new FormatException("XML nesting depth limit (100) exceeded.");

            // Check for xsi:nil="true"
            for (int i = 0; i < node.Attributes.Count; i++)
            {
                var attr = node.Attributes[i];
                if (attr.Key == "nil" || attr.Key == "nillable")
                {
                    if (attr.Value == "true" || attr.Value == "1")
                        return DynValue.Null();
                }
            }

            // Filter out nil/nillable from attributes for further processing
            var attrs = new List<KeyValuePair<string, string>>();
            for (int i = 0; i < node.Attributes.Count; i++)
            {
                var attr = node.Attributes[i];
                if (attr.Key != "nil" && attr.Key != "nillable")
                    attrs.Add(attr);
            }

            bool hasAttrs = attrs.Count > 0;
            bool hasChildren = node.Children.Count > 0;
            bool hasText = node.Text.Length > 0;

            // Leaf element with no attributes
            if (!hasAttrs && !hasChildren)
            {
                // Self-closing <tag/> → null; empty <tag></tag> → empty string
                if (node.SelfClosing)
                    return DynValue.Null();
                return DynValue.String(node.Text);
            }

            // Build object
            var entries = new List<KeyValuePair<string, DynValue>>();

            // Attributes first (prefixed with @)
            for (int i = 0; i < attrs.Count; i++)
            {
                entries.Add(new KeyValuePair<string, DynValue>(
                    "@" + attrs[i].Key,
                    DynValue.String(attrs[i].Value)));
            }

            // Text content
            if (hasText && hasChildren)
            {
                // Mixed content: direct text goes into _text
                entries.Add(new KeyValuePair<string, DynValue>("_text", DynValue.String(node.Text)));
            }
            else if (hasText && hasAttrs)
            {
                entries.Add(new KeyValuePair<string, DynValue>("_text", DynValue.String(node.Text)));
            }

            // Child elements - group repeated names into arrays
            if (hasChildren)
            {
                var childGroups = new List<KeyValuePair<string, List<DynValue>>>();
                var seen = new List<string>();

                for (int c = 0; c < node.Children.Count; c++)
                {
                    var child = node.Children[c];
                    DynValue childValue = NodeToValue(child, depth + 1);

                    int idx = seen.IndexOf(child.Name);
                    if (idx >= 0)
                    {
                        childGroups[idx].Value.Add(childValue);
                    }
                    else
                    {
                        seen.Add(child.Name);
                        childGroups.Add(new KeyValuePair<string, List<DynValue>>(
                            child.Name, new List<DynValue> { childValue }));
                    }
                }

                for (int i = 0; i < childGroups.Count; i++)
                {
                    if (childGroups[i].Value.Count == 1)
                    {
                        entries.Add(new KeyValuePair<string, DynValue>(
                            childGroups[i].Key, childGroups[i].Value[0]));
                    }
                    else
                    {
                        entries.Add(new KeyValuePair<string, DynValue>(
                            childGroups[i].Key, DynValue.Array(childGroups[i].Value)));
                    }
                }
            }

            if (entries.Count == 0)
                return DynValue.Null();

            return DynValue.Object(entries);
        }
    }
}
