// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;

namespace System.Text.Yaml
{
    /// <summary>
    /// Provides the ability for the user to define custom behavior when parsing YAML to create a <see cref="YamlDocument"/>.
    /// </summary>
    public struct YamlDocumentOptions
    {
        internal const int DefaultMaxDepth = 64;

        private int _maxDepth;
        private YamlCommentHandling _commentHandling;

        /// <summary>
        /// Defines how the <see cref="Utf8YamlReader"/> should handle comments when reading through the YAML.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the comment handling enum is set to a value that is not supported (or not within the <see cref="YamlCommentHandling"/> enum range).
        /// </exception>
        /// <remarks>
        /// <para>
        /// By default (<see cref="YamlCommentHandling.Disallow"/>) <c>#</c> comments are skipped and <c>//</c> and
        /// <c>/* */</c> are not comments, as in standard YAML. With <see cref="YamlCommentHandling.Skip"/> the JSON
        /// comments of a JSON payload are skipped too, as System.Text.Json skips them; see
        /// <see cref="YamlCommentHandling"/> for where they are read.
        /// </para>
        /// <para>
        /// With <see cref="YamlCommentHandling.Allow"/> the document keeps its comments, where System.Text.Json's
        /// <c>JsonDocument</c> refuses the value: each one is attached to the entry or item it is above or ends the line
        /// of, or to the start or the end of a collection (the comments at the top and the bottom of a file are the root
        /// collection's). A block-style <see cref="Utf8YamlWriter"/> writes them back with the element; a
        /// JSON-compatible one writes none. The text and the place relative to the nodes are kept, not the columns, the
        /// blank lines or the spacing after <c>#</c>: a comment is written <c># text</c>. JSON comments read this way
        /// are written as <c>#</c> comments. A copy of a node, through an alias or a merge key, writes no comments of
        /// its source. A few comments are not kept: one on a block scalar's header (<c>key: | # c</c>), after an
        /// explicit key or value indicator (<c>? k # c</c>, <c>- # c</c> above the item's content), after a node's
        /// anchor or tag alone on its line, and inside a collection key.
        /// </para>
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
        /// Defines whether duplicate property names are allowed when deserializing YAML objects: a view of
        /// <see cref="DuplicateKeyHandling"/>, true unless it is <see cref="YamlDuplicateKeyHandling.Error"/>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Setting it to true selects <see cref="YamlDuplicateKeyHandling.LastWins"/> and false selects
        /// <see cref="YamlDuplicateKeyHandling.Error"/>, which throws a <see cref="YamlException"/> when a duplicate
        /// property name is encountered. By default it is false (decision 1).
        /// </para>
        /// </remarks>
        public bool AllowDuplicateProperties
        {
            readonly get => DuplicateKeyHandling != YamlDuplicateKeyHandling.Error;
            set => DuplicateKeyHandling = value ? YamlDuplicateKeyHandling.LastWins : YamlDuplicateKeyHandling.Error;
        }

        // 0 is unset, so that the default struct value reads the default (Error); otherwise the value plus one.
        private byte _duplicateKeyHandling;

        /// <summary>
        /// Gets or sets what parsing does with a mapping key that appears twice in an object. By default,
        /// <see cref="YamlDuplicateKeyHandling.Error"/>. Under <see cref="YamlDuplicateKeyHandling.LastWins"/> and
        /// <see cref="YamlDuplicateKeyHandling.FirstWins"/> the document keeps every entry, and a property lookup returns
        /// the last or the first match.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">The value is not a defined <see cref="YamlDuplicateKeyHandling"/>.</exception>
        public YamlDuplicateKeyHandling DuplicateKeyHandling
        {
            readonly get => _duplicateKeyHandling == 0 ? DefaultDuplicateKeyHandling : (YamlDuplicateKeyHandling)(_duplicateKeyHandling - 1);
            set
            {
                if ((uint)value > (uint)YamlDuplicateKeyHandling.FirstWins)
                {
                    throw new ArgumentOutOfRangeException(nameof(value));
                }

                _duplicateKeyHandling = (byte)((int)value + 1);
            }
        }

        /// <summary>The default handling: <c>Error</c>.</summary>
        internal static YamlDuplicateKeyHandling DefaultDuplicateKeyHandling =>
            YamlDuplicateKeyHandling.Error;

        /// <summary>
        /// Gets or sets the schema that resolves the document's plain scalars: <see cref="YamlSchemaKind.Core"/> (YAML 1.2,
        /// the default), <see cref="YamlSchemaKind.Yaml11"/>, <see cref="YamlSchemaKind.Json"/> or
        /// <see cref="YamlSchemaKind.Failsafe"/>. It decides the token types of <c>yes</c>, <c>017</c>, <c>1_000</c> or
        /// <c>foo</c>, and the number forms the element's getters accept.
        /// </summary>
        public YamlSchemaKind Schema { get; set; }

        /// <summary>
        /// Gets or sets the most nodes that expanding aliases may add when a document's nodes are copied: a
        /// <see cref="Nodes.YamlNode"/> made from the document, an element written or cloned apart from the anchor it
        /// refers to, or an element deserialized. Reading, enumerating and comparing the document copy nothing, so the
        /// limit does not apply to them.
        /// </summary>
        /// <remarks>
        /// The default (0) allows ten times the document's own node count, or 10,000 nodes, whichever is larger. Past the
        /// limit a <see cref="YamlException"/> is thrown. An alias inside the node it refers to is never expanded.
        /// </remarks>
        /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
        public int MaxAliasExpansion
        {
            readonly get => _maxAliasExpansion;
            set
            {
                if (value < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(value));
                }

                _maxAliasExpansion = value;
            }
        }

        private int _maxAliasExpansion;

        /// <summary>
        /// Gets or sets whether a plain <c>&lt;&lt;</c> key merges mappings into the mapping that holds it (the YAML 1.1
        /// merge type, yaml.org/type/merge): its value is an alias, a mapping or a sequence of them; merged entries take
        /// the place of the <c>&lt;&lt;</c> entry; the mapping's own keys win over merged ones, and an earlier merged
        /// mapping wins over a later one. Off by default, so <c>&lt;&lt;</c> is an ordinary key.
        /// </summary>
        /// <remarks>
        /// A document with a merge key is read from its merged text, written in block style, so its raw text and
        /// positions are the merged text's, and a syntax error surfaces from the parse of the source. Copied entries count
        /// against <see cref="MaxAliasExpansion"/>.
        /// </remarks>
        public bool ResolveMergeKeys { get; set; }

        /// <summary>
        /// The serializer's <c>AcceptLenientScalars</c>, for the DOM parses it makes from these options (the merge rewrite
        /// and the re-parse of an <c>object</c> value with outside aliases expanded; 5.3, C11). Not public: a document
        /// parsed by <see cref="YamlDocument.Parse(string, YamlDocumentOptions)"/> never reads leniently.
        /// </summary>
        internal bool AcceptLenientScalars { get; set; }

        internal YamlReaderOptions GetReaderOptions()
        {
            return new YamlReaderOptions
            {
                AllowTrailingCommas = AllowTrailingCommas,
                CommentHandling = CommentHandling,
                MaxDepth = MaxDepth,
                Schema = Schema,
                AcceptLenientScalars = AcceptLenientScalars,
            };
        }
    }
}
