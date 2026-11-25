// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;

namespace System.Text.Yaml
{
    /// <summary>
    /// Provides the ability for the user to define custom behavior when reading YAML.
    /// </summary>
    public struct YamlReaderOptions
    {
        internal const int DefaultMaxDepth = 64;

        private int _maxDepth;
        private YamlCommentHandling _commentHandling;
        private YamlSchemaKind _schema;

        /// <summary>
        /// Defines how the <see cref="Utf8YamlReader"/> handles comments: YAML's <c>#</c> comments, and with
        /// <see cref="YamlCommentHandling.Skip"/> or <see cref="YamlCommentHandling.Allow"/> the JSON comments
        /// (<c>//</c> and <c>/* */</c>) of a JSON payload.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the comment handling enum is set to a value that is not supported (i.e. not within the <see cref="YamlCommentHandling"/> enum range).
        /// </exception>
        /// <remarks>
        /// By default (<see cref="YamlCommentHandling.Disallow"/>) <c>#</c> comments are skipped and <c>//</c> and
        /// <c>/* */</c> are not comments, as in standard YAML. See <see cref="YamlCommentHandling"/> for where JSON
        /// comments are read.
        /// </remarks>
        public YamlCommentHandling CommentHandling
        {
            readonly get => _commentHandling;
            set
            {
                Debug.Assert(value >= 0);
                if (value > YamlCommentHandling.Allow)
                {
                    ThrowHelper.ThrowArgumentOutOfRangeException_CommentEnumMustBeInRange(nameof(value));
                }

                _commentHandling = value;
            }
        }

        /// <summary>
        /// Gets or sets the maximum depth allowed when reading YAML, with the default (i.e. 0) indicating a max depth of 64.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the max depth is set to a negative value.
        /// </exception>
        /// <remarks>
        /// Reading past this depth will throw a <exception cref="YamlException"/>.
        /// </remarks>
        public int MaxDepth
        {
            readonly get => _maxDepth;
            set
            {
                if (value < 0)
                {
                    ThrowHelper.ThrowArgumentOutOfRangeException_MaxDepthMustBePositive(nameof(value));
                }

                _maxDepth = value;
            }
        }

        /// <summary>
        /// Kept for source compatibility with System.Text.Json; it has no effect. YAML flow collections always allow
        /// (and ignore) one trailing comma before their closing bracket.
        /// </summary>
        /// <remarks>
        /// The JSON grammar that read this option was deleted in M7a (decision D8); setting it changes nothing.
        /// </remarks>
        public bool AllowTrailingCommas { get; set; }

        /// <summary>
        /// Defines whether the <see cref="Utf8YamlReader"/> should tolerate
        /// zero or more top-level YAML values that are whitespace separated.
        /// </summary>
        /// <remarks>
        /// By default, it's set to false, and <exception cref="YamlException"/> is thrown if trailing content is encountered after the first top-level YAML value.
        /// </remarks>
        public bool AllowMultipleValues { get; set; }

        /// <summary>
        /// Gets or sets whether a flow collection may <em>not</em> be an implicit mapping key (<c>[a, b]: v</c>,
        /// <c>{a: b}: v</c>, <c>[[a]: b]</c>, <c>{[a]: b}</c>). <see langword="false"/> by default: YAML allows such keys, and
        /// deciding whether a <c>[</c> or <c>{</c> at the root or in a flow sequence starts one needs the rest of its line
        /// (at most 1024 characters), so with partial data the reader may wait for more input before it returns
        /// <see cref="YamlTokenType.StartArray"/> or <see cref="YamlTokenType.StartObject"/>. Set it to
        /// <see langword="true"/> to read such collections at once, as the YAML reader does, for example for a
        /// stream of YAML; an implicit collection key is then an error. Explicit keys (<c>? [a, b]</c>) are not
        /// affected.
        /// </summary>
        public bool DisallowImplicitCollectionKeys { get; set; }

        /// <summary>
        /// Gets or sets whether the reader refuses the YAML 1.2 forms that the drafts of YAML 1.3 take out of the
        /// language. <see langword="false"/> by default. When it is set, each of these is an error:
        /// <list type="bullet">
        /// <item><description>A scalar at the top level of a document with no <c>---</c> line before it: a file that
        /// holds <c>'text'</c> alone.</description></item>
        /// <item><description>A plain or quoted scalar that starts on the <c>---</c> line (<c>--- text</c>). A block
        /// scalar may start there (<c>--- |</c>), and so may an anchor or a tag.</description></item>
        /// <item><description>The content of a top-level block scalar with no indentation, at the start of its
        /// lines.</description></item>
        /// <item><description>Outside a flow collection, an anchor or a tag alone on its line, when the line before
        /// does not end with the indicator the node follows: <c>&amp;a</c> on a line, then <c>key: v</c> on the next.
        /// <c>top: &amp;a</c>, with the mapping on the next line, is not affected.</description></item>
        /// <item><description>An anchor or alias name with an ASCII character that is not a letter, a digit,
        /// <c>_</c> or <c>-</c> (<c>&amp;a:b</c>). Characters outside ASCII are accepted.</description></item>
        /// <item><description>A flow collection as an implicit key of a block mapping (<c>[a, b]: v</c>). An explicit
        /// key (<c>? [a, b]</c>) and a key of a flow mapping (<c>{[a, b]: v}</c>) are not affected.</description></item>
        /// </list>
        /// </summary>
        /// <remarks>
        /// YAML 1.3 is not released. These rules come from its draft proposals and from the yaml-test-suite's cases
        /// marked as invalid in YAML 1.3 or rewritten for it, limited to what those cases need, and they may change
        /// with the drafts. The option
        /// does not depend on a <c>%YAML</c> directive: a document that declares another version is read by the same
        /// rules. Two forms that <see cref="Utf8YamlWriter"/> writes do not read back with this option: a top-level
        /// scalar, unless <see cref="Utf8YamlWriter.WriteDocumentStart"/> was called before it, and a top-level block
        /// collection with an anchor or a tag, which the writer puts on a line of its own.
        /// </remarks>
        public bool ApplyYaml13DraftRestrictions { get; set; }

        /// <summary>
        /// Gets or sets whether typed reads accept scalars that YamlDotNet or SharpYaml resolve although no YAML standard
        /// does. <see langword="false"/> by default. When it is set:
        /// <list type="bullet">
        /// <item><description><see cref="Utf8YamlReader.GetBoolean"/> accepts <c>true</c> and <c>false</c> in any case
        /// (<c>tRue</c>), and with <see cref="YamlSchemaKind.Yaml11"/> also <c>y</c>, <c>yes</c>, <c>n</c>, <c>no</c>,
        /// <c>on</c> and <c>off</c> in any case (<c>yEs</c>).</description></item>
        /// <item><description><see cref="Utf8YamlReader.GetDouble"/> and <see cref="Utf8YamlReader.GetSingle"/> accept
        /// <c>.inf</c> and <c>.nan</c> in any case, with or without a sign, and .NET's <c>nan</c> and <c>infinity</c> in
        /// any case (<c>-Infinity</c>).</description></item>
        /// <item><description>The boolean and number readers also accept a quoted, block or tagged scalar (<c>'true'</c>,
        /// <c>"12"</c>, <c>!!str 1.5</c>, <c>|</c> blocks) whose text, trimmed of white space and line breaks, is the
        /// plain scalar of a boolean, a number or one of those names. A quoted null (<c>''</c>, <c>'null'</c>) is still a
        /// string.</description></item>
        /// <item><description>An enum read takes a quoted, block or tagged number as the plain number (<c>'1'</c>), and
        /// the date, time, <see cref="Guid"/> and <see cref="Version"/> readers take a quoted or block text padded with
        /// white space or line breaks as its trimmed text (<c>' 2001-01-01 '</c>); a <see cref="char"/> takes one
        /// character followed by white space or a line break.</description></item>
        /// <item><description>A core tag also takes those forms: <c>!!bool tRuE</c>, <c>!!float .iNf</c> and
        /// <c>!!null NuLL</c> resolve to their type, so for these three the token type changes.</description></item>
        /// <item><description>The serializer reads <c>null</c> in any case (<c>NuLL</c>) as it reads <c>null</c> into a
        /// target that is not <see cref="object"/> or a YAML document or node type, and reads a null into a
        /// non-nullable value type (<see cref="int"/>, <see cref="bool"/>, an enum, a struct) as its default. A
        /// constructor parameter is the exception: it refuses a null as without the option, where a property of the same
        /// type takes the default.</description></item>
        /// <item><description>A plain <see cref="bool"/> dictionary key in another case than the core schema's
        /// (<c>tRue:</c>) reads only with the option, and with <see cref="YamlSchemaKind.Yaml11"/> so does
        /// <c>yEs:</c>.</description></item>
        /// <item><description>Under <see cref="YamlSchemaKind.Json"/>, an untagged plain scalar that matches no JSON form
        /// (<c>yes</c>, <c>True</c>, <c>~</c>, <c>0x1F</c>, <c>foo</c>, the empty scalar) is a
        /// <see cref="YamlTokenType.String"/>, as SharpYaml reads it, rather than an error. It is a superset of SharpYaml's
        /// reading, since the other rules still apply: <c>True</c> then reads into a <see cref="bool"/>, which SharpYaml's
        /// JSON schema refuses; <c>1_000</c> and <c>.inf</c> stay strings.</description></item>
        /// </list>
        /// A converter of your own that handles null receives a <c>NuLL</c> scalar as the <see cref="YamlTokenType.String"/>
        /// token it is, not as <see cref="YamlTokenType.Null"/>. Styml's writer writes a string such as <c>NuLl</c> plain by
        /// default, which a lenient reader takes as null; write with <see cref="YamlWriterOptions.QuoteForLenientReaders"/>
        /// to read every string back.
        /// </summary>
        public bool AcceptLenientScalars { get; set; }

        /// <summary>
        /// The serializer's readers record every anchor they meet in the operation's <see cref="YamlAnchorTable"/>.
        /// Readers that users and the DOM create do not.
        /// </summary>
        internal bool RecordAnchors { get; set; }

        /// <summary>
        /// The reader looks for <c>#</c> comments only, whatever <see cref="CommentHandling"/> says about JSON
        /// comments: for the library's own readers that ask for comment tokens over YAML text.
        /// </summary>
        internal bool YamlCommentsOnly { get; set; }

        /// <summary>
        /// Gets or sets the schema that gives plain scalars in value position their token type and that the typed
        /// readers (<see cref="Utf8YamlReader.GetInt32"/> and the others) accept. <see cref="YamlSchemaKind.Core"/>, the
        /// YAML 1.2 core schema, by default.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">The value is not a defined <see cref="YamlSchemaKind"/>.</exception>
        public YamlSchemaKind Schema
        {
            readonly get => _schema;
            set
            {
                if ((uint)value > (uint)YamlSchemaKind.Failsafe)
                {
                    throw new ArgumentOutOfRangeException(nameof(value));
                }
                _schema = value;
            }
        }
    }
}
