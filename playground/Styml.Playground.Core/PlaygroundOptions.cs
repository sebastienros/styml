using System.Text.Yaml;
using System.Text.Yaml.Serialization;

namespace Styml.Playground;

/// <summary>
/// The options the playground lets users change: the ones that change how YAML is read into a document, and how the
/// document is written back. Every property defaults to Styml's default, so that a link only stores what was changed.
/// </summary>
public sealed record PlaygroundOptions
{
    // Reading (YamlDocumentOptions)

    /// <summary><see cref="YamlDocumentOptions.Schema"/>: how plain scalars resolve.</summary>
    [YamlPropertyName("sc")] public YamlSchemaKind Schema { get; init; }

    /// <summary><see cref="YamlDocumentOptions.DuplicateKeyHandling"/>.</summary>
    [YamlPropertyName("dk")] public YamlDuplicateKeyHandling DuplicateKeyHandling { get; init; }

    /// <summary><see cref="YamlDocumentOptions.ResolveMergeKeys"/>: <c>&lt;&lt;</c> merges mappings.</summary>
    [YamlPropertyName("mk")] public bool ResolveMergeKeys { get; init; }

    /// <summary><see cref="YamlDocumentOptions.CommentHandling"/> set to <see cref="YamlCommentHandling.Skip"/>: JSON's <c>//</c> and <c>/* */</c> comments.</summary>
    [YamlPropertyName("jc")] public bool SkipJsonComments { get; init; }

    /// <summary><see cref="YamlDocumentOptions.MaxDepth"/>, 0 for the default (64).</summary>
    [YamlPropertyName("md")] public int MaxDepth { get; init; }

    /// <summary><see cref="YamlDocumentOptions.MaxAliasExpansion"/>, 0 for the default.</summary>
    [YamlPropertyName("ma")] public int MaxAliasExpansion { get; init; }

    // Writing (YamlWriterOptions)

    /// <summary><see cref="YamlWriterOptions.IndentSize"/>, at least 2 in block style.</summary>
    [YamlPropertyName("is")] public int IndentSize { get; init; } = 2;

    /// <summary><see cref="YamlWriterOptions.IndentSequences"/>.</summary>
    [YamlPropertyName("iq")] public bool IndentSequences { get; init; }

    /// <summary><see cref="YamlWriterOptions.ExpandSequenceItems"/>.</summary>
    [YamlPropertyName("ex")] public bool ExpandSequenceItems { get; init; }

    /// <summary><see cref="YamlWriterOptions.MaxLineWidth"/>, 0 to fold nothing.</summary>
    [YamlPropertyName("lw")] public int MaxLineWidth { get; init; }

    /// <summary><see cref="YamlWriterOptions.MultiLineStringStyle"/>.</summary>
    [YamlPropertyName("ml")] public YamlScalarStyle MultiLineStringStyle { get; init; }

    /// <summary><see cref="YamlWriterOptions.DefaultScalarStyle"/>.</summary>
    [YamlPropertyName("ds")] public YamlScalarStyle DefaultScalarStyle { get; init; }

    /// <summary><see cref="YamlWriterOptions.QuoteForLenientReaders"/>.</summary>
    [YamlPropertyName("ql")] public bool QuoteForLenientReaders { get; init; }

    /// <summary><see cref="YamlWriterOptions.IgnoreSourceStyles"/>: write in the options' styles, not the source's.</summary>
    [YamlPropertyName("ig")] public bool IgnoreSourceStyles { get; init; }

    /// <summary><see cref="YamlWriterOptions.Indented"/> for the JSON output.</summary>
    [YamlPropertyName("ji")] public bool JsonIndented { get; init; } = true;

    /// <summary>The options of <see cref="YamlDocument.Parse(ReadOnlyMemory{byte}, YamlDocumentOptions)"/>.</summary>
    public YamlDocumentOptions ToDocumentOptions() => new()
    {
        Schema = Schema,
        DuplicateKeyHandling = DuplicateKeyHandling,
        ResolveMergeKeys = ResolveMergeKeys,
        CommentHandling = SkipJsonComments ? YamlCommentHandling.Skip : YamlCommentHandling.Disallow,
        MaxDepth = MaxDepth,
        MaxAliasExpansion = MaxAliasExpansion,
    };

    /// <summary>The options of the reader that splits the stream into documents, and of the token view.</summary>
    public YamlReaderOptions ToReaderOptions() => new()
    {
        Schema = Schema,
        CommentHandling = SkipJsonComments ? YamlCommentHandling.Skip : YamlCommentHandling.Disallow,
        MaxDepth = MaxDepth,
        AllowMultipleValues = true,
    };

    /// <summary>The options that write a document back, in block style or as JSON.</summary>
    public YamlWriterOptions ToWriterOptions(bool json) => new()
    {
        Style = json ? YamlWriterStyle.JsonCompatible : YamlWriterStyle.Block,
        Indented = json && JsonIndented,
        IndentSize = Math.Max(IndentSize, 2),
        IndentSequences = IndentSequences,
        ExpandSequenceItems = ExpandSequenceItems,
        MaxLineWidth = MaxLineWidth,
        MultiLineStringStyle = MultiLineStringStyle,
        DefaultScalarStyle = DefaultScalarStyle,
        QuoteForLenientReaders = QuoteForLenientReaders,
        IgnoreSourceStyles = IgnoreSourceStyles,
        NewLine = "\n",
    };
}
