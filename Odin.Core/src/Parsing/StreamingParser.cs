using System;
using Odin.Core.Types;

namespace Odin.Core.Parsing;

/// <summary>Handler interface for streaming parse events.</summary>
public interface IParseHandler
{
    /// <summary>Called when a metadata key-value pair is parsed.</summary>
    void OnMetadata(string key, OdinValue value);

    /// <summary>Called when a section header is encountered.</summary>
    void OnHeader(string path);

    /// <summary>Called when a field assignment is parsed.</summary>
    void OnAssignment(string path, OdinValue value);

    /// <summary>Called when a modifier is encountered.</summary>
    void OnModifier(string path, OdinModifiers modifiers);

    /// <summary>Called when a comment is encountered.</summary>
    void OnComment(string text, int line);

    /// <summary>Called when a document separator (---) is encountered.</summary>
    void OnDocumentSeparator();

    /// <summary>Called when parsing is complete.</summary>
    void OnEnd();

    /// <summary>Called when a parse error occurs.</summary>
    void OnError(OdinParseException exception);
}

/// <summary>
/// Callback-based incremental parser for processing large ODIN documents
/// without building a full in-memory document.
/// </summary>
public sealed class StreamingParser
{
    private readonly IParseHandler _handler;
    private readonly ParseOptions _options;

    /// <summary>Creates a streaming parser with the given handler.</summary>
    public StreamingParser(IParseHandler handler, ParseOptions? options = null)
    {
        _handler = handler;
        _options = options ?? ParseOptions.Default;
    }

    /// <summary>Parse the input text, firing events on the handler.</summary>
    public void Parse(string input)
    {
        try
        {
            var tokens = Tokenizer.Tokenize(input, _options);
            var docs = OdinParser.ParseTokensMulti(tokens, input, _options);

            for (int docIdx = 0; docIdx < docs.Count; docIdx++)
            {
                if (docIdx > 0)
                    _handler.OnDocumentSeparator();

                var doc = docs[docIdx];

                foreach (var entry in doc.Metadata)
                    _handler.OnMetadata(entry.Key, entry.Value);

                foreach (var entry in doc.Assignments)
                    _handler.OnAssignment(entry.Key, entry.Value);

                foreach (var entry in doc.PathModifiers)
                    _handler.OnModifier(entry.Key, entry.Value);

                if (_options.PreserveComments)
                {
                    foreach (var comment in doc.Comments)
                        _handler.OnComment(comment.Text, comment.Line);
                }
            }

            _handler.OnEnd();
        }
        catch (OdinParseException ex)
        {
            _handler.OnError(ex);
        }
    }
}
