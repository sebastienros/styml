using System.Buffers;
using System.Diagnostics;
using System.Text;
using System.Text.Yaml;

namespace Styml.Playground;

/// <summary>What the playground shows of the input.</summary>
public enum PlaygroundView
{
    /// <summary>The documents written back as YAML, in block style.</summary>
    Yaml,

    /// <summary>The documents written as JSON.</summary>
    Json,

    /// <summary>The nodes of each document, with their kind, style, anchor and tag.</summary>
    Tree,

    /// <summary>The tokens of <see cref="Utf8YamlReader"/>.</summary>
    Tokens,
}

/// <summary>An error of the input: a message and, when Styml knows it, a 1-based line and column.</summary>
public sealed record PlaygroundError(string Message, int? Line, int? Column);

/// <summary>A node of the tree view.</summary>
/// <param name="Key">The key of the node in its mapping: the scalar key, or the YAML of a collection key.</param>
/// <param name="ComplexKey">Whether the key is a collection (<c>? [a, b]</c>).</param>
/// <param name="Kind">mapping, sequence or scalar.</param>
/// <param name="Type">What a scalar resolves to under the schema: string, number, boolean or null.</param>
/// <param name="Value">The text of a scalar.</param>
/// <param name="Style">The scalar style, or the collection style (block or flow), as read.</param>
/// <param name="Anchor">The anchor defined on the node, or the anchor an alias refers to.</param>
/// <param name="IsAlias">Whether the node is an alias.</param>
/// <param name="Tag">The tag, expanded.</param>
/// <param name="Children">The entries of a mapping or the items of a sequence.</param>
public sealed record PlaygroundNode(
    string? Key,
    bool ComplexKey,
    string Kind,
    string? Type,
    string? Value,
    string? Style,
    string? Anchor,
    bool IsAlias,
    string? Tag,
    IReadOnlyList<PlaygroundNode> Children);

/// <summary>A token of the token view.</summary>
/// <param name="Type">The token type.</param>
/// <param name="Depth">The depth of the token, for indentation.</param>
/// <param name="Text">The value of the token (a scalar, a key, the name of an alias, a comment).</param>
/// <param name="Style">The scalar style of a scalar or a key.</param>
/// <param name="Anchor">The anchor on the token.</param>
/// <param name="Tag">The tag on the token, expanded.</param>
/// <param name="Line">The 1-based line where the token starts.</param>
/// <param name="Column">The 1-based column, in UTF-8 bytes, where the token starts.</param>
public sealed record PlaygroundToken(YamlTokenType Type, int Depth, string? Text, string? Style, string? Anchor, string? Tag, int Line, int Column);

/// <summary>What the engine made of the input.</summary>
public sealed record PlaygroundResult
{
    /// <summary>The number of documents in the stream.</summary>
    public int DocumentCount { get; init; }

    /// <summary>The YAML or JSON output, for <see cref="PlaygroundView.Yaml"/> and <see cref="PlaygroundView.Json"/>.</summary>
    public string Output { get; init; } = "";

    /// <summary>The root node of each document, for <see cref="PlaygroundView.Tree"/>; null for an empty document.</summary>
    public IReadOnlyList<PlaygroundNode?> Trees { get; init; } = [];

    /// <summary>The tokens, for <see cref="PlaygroundView.Tokens"/>.</summary>
    public IReadOnlyList<PlaygroundToken> Tokens { get; init; } = [];

    /// <summary>The error, if the input could not be read.</summary>
    public PlaygroundError? Error { get; init; }

    /// <summary>The time spent parsing the documents (or reading the tokens).</summary>
    public TimeSpan ParseTime { get; init; }

    /// <summary>The time spent writing the output or building the tree.</summary>
    public TimeSpan WriteTime { get; init; }
}

/// <summary>
/// Reads YAML with Styml the way the options say and shows it in one of the <see cref="PlaygroundView"/> views. A stream
/// of several documents is split with <see cref="Utf8YamlReader"/> (<see cref="YamlReaderOptions.AllowMultipleValues"/>),
/// and each document is parsed with <see cref="YamlDocument.Parse(ReadOnlyMemory{byte}, YamlDocumentOptions)"/>, so that
/// every document option applies to it.
/// </summary>
public static class PlaygroundEngine
{
    /// <summary>The most tokens the token view lists.</summary>
    public const int MaxTokens = 5_000;

    /// <summary>Reads <paramref name="yaml"/> and shows it in <paramref name="view"/>.</summary>
    public static PlaygroundResult Run(string yaml, PlaygroundOptions options, PlaygroundView view)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(yaml);
        if (view == PlaygroundView.Tokens)
        {
            return ReadTokens(utf8, options);
        }

        var parse = Stopwatch.StartNew();
        List<YamlDocument?> documents = [];
        try
        {
            foreach (var (start, length) in SplitDocuments(utf8, options))
            {
                var slice = new ReadOnlyMemory<byte>(utf8, start, length);
                documents.Add(HasContent(slice.Span) ? YamlDocument.Parse(slice, options.ToDocumentOptions()) : null);
            }

            parse.Stop();
            var write = Stopwatch.StartNew();
            var result = view switch
            {
                PlaygroundView.Tree => new PlaygroundResult { Trees = [.. documents.Select(d => d is null ? null : ToNode(d.RootElement, null, false))] },
                _ => new PlaygroundResult { Output = Write(documents, options, view == PlaygroundView.Json) },
            };
            write.Stop();
            return result with { DocumentCount = documents.Count, ParseTime = parse.Elapsed, WriteTime = write.Elapsed };
        }
        catch (Exception e) when (e is YamlException or InvalidOperationException or ArgumentException or NotSupportedException)
        {
            return new PlaygroundResult { Error = ToError(e), DocumentCount = documents.Count, ParseTime = parse.Elapsed };
        }
        finally
        {
            foreach (var document in documents)
            {
                document?.Dispose();
            }
        }
    }

    /// <summary>
    /// The byte ranges of the documents of the stream: each one from the end of the previous document, so that it keeps
    /// its directives and its <c>---</c>, to the end of its root value.
    /// </summary>
    internal static List<(int Start, int Length)> SplitDocuments(byte[] utf8, PlaygroundOptions options)
    {
        List<(int, int)> ranges = [];
        var reader = new Utf8YamlReader(utf8, options.ToReaderOptions());
        int start = 0;
        while (reader.Read())
        {
            if (reader.TokenType is YamlTokenType.Comment)
            {
                continue;
            }

            if (reader.TokenType is YamlTokenType.StartObject or YamlTokenType.StartArray)
            {
                reader.Skip();
            }

            int end = (int)reader.BytesConsumed;
            ranges.Add((start, end - start));
            start = end;
        }

        // An empty stream, or one with only comments, is one empty document.
        if (ranges.Count == 0)
        {
            ranges.Add((0, utf8.Length));
        }

        return ranges;
    }

    private static bool HasContent(ReadOnlySpan<byte> utf8)
    {
        // A document of comments, blank lines and markers only is empty: YamlDocument.Parse refuses it.
        foreach (var line in Encoding.UTF8.GetString(utf8).Split('\n'))
        {
            var text = line.Trim();
            if (text.Length > 0 && text[0] != '#' && text != "---" && text != "...")
            {
                return true;
            }
        }

        return false;
    }

    private static string Write(List<YamlDocument?> documents, PlaygroundOptions options, bool json)
    {
        var writerOptions = options.ToWriterOptions(json);
        var output = new StringBuilder();
        for (int i = 0; i < documents.Count; i++)
        {
            // Each document ends with a line break: JSON documents are one per line (or block), YAML ones are separated by ---.
            if (i > 0 && !json)
            {
                output.Append("---\n");
            }

            string text = documents[i] is { } document ? Write(document.RootElement, writerOptions) : json ? "null" : "";
            output.Append(text);
            if (text.Length > 0 && text[^1] != '\n')
            {
                output.Append('\n');
            }
        }

        return output.ToString();
    }

    private static string Write(YamlElement element, YamlWriterOptions options)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8YamlWriter(buffer, options))
        {
            element.WriteTo(writer);
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    private static PlaygroundNode ToNode(YamlElement element, string? key, bool complexKey)
    {
        string? anchor = element.GetAnchor();
        string? tag = element.GetExpandedTag();
        switch (element.ValueKind)
        {
            case YamlValueKind.Object:
                return new PlaygroundNode(key, complexKey, "mapping", null, null, Style(element.CollectionStyle), anchor, element.IsAlias, tag,
                    element.IsAlias ? [] : [.. element.EnumerateObject().Select(p =>
                        p.KeyKind == YamlKeyKind.Complex
                            ? ToNode(p.Value, KeyText(p.GetKeyElement()), true)
                            : ToNode(p.Value, p.Name, false))]);
            case YamlValueKind.Array:
                return new PlaygroundNode(key, complexKey, "sequence", null, null, Style(element.CollectionStyle), anchor, element.IsAlias, tag,
                    element.IsAlias ? [] : [.. element.EnumerateArray().Select(item => ToNode(item, null, false))]);
            default:
                string type = element.ValueKind switch
                {
                    YamlValueKind.Number => "number",
                    YamlValueKind.True or YamlValueKind.False => "boolean",
                    YamlValueKind.Null => "null",
                    _ => "string",
                };
                string? value = element.ValueKind == YamlValueKind.String ? element.GetString() : element.GetRawText();
                return new PlaygroundNode(key, complexKey, "scalar", type, value, element.ScalarStyle.ToString(), anchor, element.IsAlias, tag, []);
        }
    }

    private static string KeyText(YamlElement key) => Write(key, new YamlWriterOptions { NewLine = "\n" }).TrimEnd('\n');

    private static string? Style(YamlCollectionStyle? style) => style?.ToString();

    private static PlaygroundResult ReadTokens(byte[] utf8, PlaygroundOptions options)
    {
        var parse = Stopwatch.StartNew();
        List<PlaygroundToken> tokens = [];
        var lineStarts = LineStarts(utf8);
        try
        {
            var reader = new Utf8YamlReader(utf8, options.ToReaderOptions() with { CommentHandling = options.SkipJsonComments ? YamlCommentHandling.Allow : YamlCommentHandling.Disallow });
            while (reader.Read() && tokens.Count < MaxTokens)
            {
                string? text = reader.TokenType switch
                {
                    // A collection key is not text: ValueSpan holds its YAML.
                    YamlTokenType.PropertyName when reader.KeyKind == YamlKeyKind.Complex => Raw(ref reader),
                    YamlTokenType.String or YamlTokenType.PropertyName => reader.GetString(),
                    YamlTokenType.Comment => reader.GetComment(),
                    YamlTokenType.Number or YamlTokenType.True or YamlTokenType.False or YamlTokenType.Null or YamlTokenType.Alias => Raw(ref reader),
                    _ => null,
                };
                string? style = reader.TokenType is YamlTokenType.PropertyName && reader.KeyKind == YamlKeyKind.Complex ? "collection key"
                    : reader.TokenType is YamlTokenType.String or YamlTokenType.PropertyName or YamlTokenType.Number
                    or YamlTokenType.True or YamlTokenType.False or YamlTokenType.Null ? reader.ScalarStyle.ToString() : null;
                string? anchor = reader.Anchor.IsEmpty ? null : Encoding.UTF8.GetString(reader.Anchor.ToArray());
                string? tag = reader.Tag.IsEmpty ? null : reader.GetExpandedTag();
                var (line, column) = Position(lineStarts, reader.TokenStartIndex);
                tokens.Add(new PlaygroundToken(reader.TokenType, reader.CurrentDepth, text, style, anchor, tag, line, column));
            }

            parse.Stop();
            return new PlaygroundResult { Tokens = tokens, ParseTime = parse.Elapsed };
        }
        catch (Exception e) when (e is YamlException or InvalidOperationException or ArgumentException)
        {
            return new PlaygroundResult { Tokens = tokens, Error = ToError(e), ParseTime = parse.Elapsed };
        }
    }

    private static string Raw(ref Utf8YamlReader reader) =>
        Encoding.UTF8.GetString(reader.HasValueSequence ? reader.ValueSequence.ToArray() : reader.ValueSpan.ToArray());

    private static List<long> LineStarts(byte[] utf8)
    {
        List<long> starts = [0];
        for (int i = 0; i < utf8.Length; i++)
        {
            if (utf8[i] == '\n')
            {
                starts.Add(i + 1);
            }
        }

        return starts;
    }

    private static (int Line, int Column) Position(List<long> lineStarts, long index)
    {
        int line = lineStarts.BinarySearch(index);
        if (line < 0)
        {
            line = ~line - 1;
        }

        return (line + 1, (int)(index - lineStarts[line]) + 1);
    }

    private static PlaygroundError ToError(Exception e)
    {
        if (e is YamlException { LineNumber: { } line } yamlException)
        {
            // The message of a YamlException ends with its position, which the playground shows on its own.
            string message = yamlException.Message;
            int at = message.IndexOf(" LineNumber:", StringComparison.Ordinal);
            if (at < 0)
            {
                at = message.IndexOf(" Path:", StringComparison.Ordinal);
            }

            return new PlaygroundError(at > 0 ? message[..at] : message, (int)line + 1, (int)(yamlException.BytePositionInLine ?? 0) + 1);
        }

        return new PlaygroundError(e.Message, null, null);
    }
}
