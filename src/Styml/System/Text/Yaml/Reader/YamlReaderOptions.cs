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
        /// Defines how the <see cref="Utf8YamlReader"/> handles <c>#</c> comments.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the comment handling enum is set to a value that is not supported (i.e. not within the <see cref="YamlCommentHandling"/> enum range).
        /// </exception>
        /// <remarks>
        /// By default comments are skipped (<see cref="YamlCommentHandling.Disallow"/> behaves as <see cref="YamlCommentHandling.Skip"/>, decision D3).
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
        /// Gets or sets whether a document with no directives keeps the previous document's <c>%TAG</c> handles, as YAML 1.1
        /// reads a stream (section 7.4): a document that declares any directive starts from its own. A stream that declares
        /// <c>%YAML 1.1</c> (or 1.0) reads that way without it, as YamlDotNet's parser does, until a document declares
        /// <c>%YAML 1.2</c>; this option applies the rule whatever the version, for a stream without the directive.
        /// <see langword="false"/> by default: YAML 1.2 makes each document independent (section 9.1). The schema does not
        /// change on <c>%YAML 1.1</c>.
        /// </summary>
        public bool InheritTagDirectives { get; set; }

        /// <summary>
        /// Gets or sets whether Unity's <c>stripped</c> marker is accepted after a document root's properties on its
        /// <c>---</c> line (<c>--- !u!4 &amp;400000 stripped</c>), where YAML would read <c>stripped</c> as the root's
        /// content. <see langword="false"/> by default. Only the word <c>stripped</c>, after properties that include a tag,
        /// before the end of the line; the root then starts on the next line, and
        /// <see cref="Utf8YamlReader.IsUnityStripped"/> is <see langword="true"/> on its token.
        /// </summary>
        public bool AcceptUnityStrippedMarker { get; set; }

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
