using System.ComponentModel;
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

    /// <summary>
    /// <see cref="YamlReaderOptions.CommentHandling"/>: <see cref="YamlCommentHandling.Skip"/> and
    /// <see cref="YamlCommentHandling.Allow"/> read JSON's <c>//</c> and <c>/* */</c> comments, and
    /// <see cref="YamlCommentHandling.Allow"/> returns every comment as a token. A <see cref="YamlDocument"/> stores no
    /// comments and refuses <see cref="YamlCommentHandling.Allow"/>, so the views that parse documents read with
    /// <see cref="YamlCommentHandling.Skip"/> instead (<see cref="ToDocumentOptions"/>).
    /// </summary>
    [YamlPropertyName("cm")] public YamlCommentHandling CommentHandling { get; init; }

    /// <summary>
    /// Reads the links written before <see cref="CommentHandling"/>, which stored a JSON comments checkbox: true selects
    /// <see cref="YamlCommentHandling.Skip"/>. It is never written.
    /// </summary>
    [YamlPropertyName("jc")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public bool LegacySkipJsonComments
    {
        get => false;
        init
        {
            if (value)
            {
                CommentHandling = YamlCommentHandling.Skip;
            }
        }
    }

    /// <summary><see cref="YamlDocumentOptions.MaxDepth"/>, 0 for the default (64), at most <see cref="MaxDepthLimit"/>.</summary>
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
    /// <remarks>Always written in the URL state: its default is <see langword="true"/>, which the state's
    /// <see cref="YamlIgnoreCondition.WhenWritingDefault"/> would otherwise drop when it is <see langword="false"/>.</remarks>
    [YamlPropertyName("ji")]
    [YamlIgnore(Condition = YamlIgnoreCondition.Never)]
    public bool JsonIndented { get; init; } = true;

    /// <summary>
    /// The deepest <see cref="MaxDepth"/> the playground accepts: the tree view is built recursively, and a deeper
    /// document would overflow the WebAssembly stack, which can't be caught.
    /// </summary>
    public const int MaxDepthLimit = 1_000;

    /// <summary>
    /// The comment handling of a <see cref="YamlDocument"/>: <see cref="CommentHandling"/>, with
    /// <see cref="YamlCommentHandling.Allow"/> read as <see cref="YamlCommentHandling.Skip"/>, since a document has no
    /// place for comments.
    /// </summary>
    [YamlIgnore]
    public YamlCommentHandling DocumentCommentHandling =>
        CommentHandling == YamlCommentHandling.Allow ? YamlCommentHandling.Skip : CommentHandling;

    /// <summary>The options of <see cref="YamlDocument.Parse(ReadOnlyMemory{byte}, YamlDocumentOptions)"/>.</summary>
    public YamlDocumentOptions ToDocumentOptions() => new()
    {
        Schema = Schema,
        DuplicateKeyHandling = DuplicateKeyHandling,
        ResolveMergeKeys = ResolveMergeKeys,
        CommentHandling = DocumentCommentHandling,
        MaxDepth = Math.Min(MaxDepth, MaxDepthLimit),
        MaxAliasExpansion = MaxAliasExpansion,
    };

    /// <summary>The options of the reader that splits the stream into documents, and of the token view.</summary>
    public YamlReaderOptions ToReaderOptions() => new()
    {
        Schema = Schema,
        CommentHandling = CommentHandling,
        MaxDepth = Math.Min(MaxDepth, MaxDepthLimit),
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
