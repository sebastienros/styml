// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Encodings.Web;

namespace System.Text.Yaml
{
    /// <summary>
    /// Provides the ability for the user to define custom behavior when writing YAML
    /// using the <see cref="Utf8YamlWriter"/>. By default, the YAML is written without
    /// any indentation or extra white space. Also, the <see cref="Utf8YamlWriter"/> will
    /// throw an exception if the user attempts to write structurally invalid YAML.
    /// </summary>
    public struct YamlWriterOptions
    {
        private static readonly string s_alternateNewLine = Environment.NewLine.Length == 2 ? YamlConstants.NewLineLineFeed : YamlConstants.NewLineCarriageReturnLineFeed;

        internal const int DefaultMaxDepth = 1000;

        private int _maxDepth;
        private int _optionsMask;
        private int _maxLineWidth;

        /// <summary>
        /// The encoder to use when escaping strings, or <see langword="null" /> to use the default encoder.
        /// </summary>
        public JavaScriptEncoder? Encoder { get; set; }

        /// <summary>
        /// Defines whether the <see cref="Utf8YamlWriter"/> should pretty print the YAML which includes:
        /// indenting nested YAML tokens, adding new lines, and adding white space between property names and values.
        /// By default, the YAML is written without any extra white space.
        /// </summary>
        public bool Indented
        {
            get
            {
                return (_optionsMask & IndentBit) != 0;
            }
            set
            {
                if (value)
                    _optionsMask |= IndentBit;
                else
                    _optionsMask &= ~IndentBit;
            }
        }

        /// <summary>
        /// Defines the indentation character used by <see cref="Utf8YamlWriter"/> when <see cref="Indented"/> is enabled. Defaults to the space character.
        /// </summary>
        /// <remarks>Allowed characters are space and horizontal tab.</remarks>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> contains an invalid character.</exception>
        public char IndentCharacter
        {
            readonly get => (_optionsMask & IndentCharacterBit) != 0 ? YamlConstants.TabIndentCharacter : YamlConstants.DefaultIndentCharacter;
            set
            {
                YamlWriterHelper.ValidateIndentCharacter(value);
                if (value is not YamlConstants.DefaultIndentCharacter)
                    _optionsMask |= IndentCharacterBit;
                else
                    _optionsMask &= ~IndentCharacterBit;
            }
        }

        /// <summary>
        /// Defines the indentation size used by <see cref="Utf8YamlWriter"/> when <see cref="Indented"/> is enabled. Defaults to two.
        /// </summary>
        /// <remarks>Allowed values are integers between 0 and 127, included.</remarks>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is out of the allowed range.</exception>
        public int IndentSize
        {
            readonly get => EncodeIndentSize((_optionsMask & IndentSizeMask) >> OptionsBitCount);
            set
            {
                YamlWriterHelper.ValidateIndentSize(value);
                _optionsMask = (_optionsMask & ~IndentSizeMask) | (EncodeIndentSize(value) << OptionsBitCount);
            }
        }

        // Encoding is applied by swapping 0 with the default value to ensure default(YamlWriterOptions) instances are well-defined.
        // As this operation is symmetrical, it can also be used to decode.
        private static int EncodeIndentSize(int value) => value switch
        {
            0 => YamlConstants.DefaultIndentSize,
            YamlConstants.DefaultIndentSize => 0,
            _ => value
        };

        /// <summary>
        /// Gets or sets the maximum depth allowed when writing YAML, with the default (i.e. 0) indicating a max depth of 1000.
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
        /// Defines whether the <see cref="Utf8YamlWriter"/> should skip structural validation and allow
        /// the user to write invalid YAML, when set to true. If set to false, any attempts to write invalid YAML will result in
        /// a <exception cref="InvalidOperationException"/> to be thrown.
        /// </summary>
        /// <remarks>
        /// If the YAML being written is known to be correct,
        /// then skipping validation (by setting it to true) could improve performance.
        /// An example of invalid YAML where the writer will throw (when SkipValidation
        /// is set to false) is when you write a value within a YAML object
        /// without a property name.
        /// </remarks>
        public bool SkipValidation
        {
            get
            {
                return (_optionsMask & SkipValidationBit) != 0;
            }
            set
            {
                if (value)
                    _optionsMask |= SkipValidationBit;
                else
                    _optionsMask &= ~SkipValidationBit;
            }
        }

        /// <summary>
        /// Gets or sets the new line string to use when <see cref="Indented"/> is <see langword="true"/>.
        /// The default is the value of <see cref="Environment.NewLine"/>.
        /// </summary>
        /// <exception cref="ArgumentNullException">
        /// Thrown when the new line string is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the new line string is not <c>\n</c> or <c>\r\n</c>.
        /// </exception>
        public string NewLine
        {
            get => (_optionsMask & NewLineBit) != 0 ? s_alternateNewLine : Environment.NewLine;
            set
            {
                YamlWriterHelper.ValidateNewLine(value);
                if (value != Environment.NewLine)
                    _optionsMask |= NewLineBit;
                else
                    _optionsMask &= ~NewLineBit;
            }
        }

        /// <summary>
        /// The output style: <see cref="YamlWriterStyle.Block"/> YAML, or <see cref="YamlWriterStyle.JsonCompatible"/>
        /// JSON. When it is not set, the writer writes block YAML.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is not a defined style.</exception>
        public YamlWriterStyle Style
        {
            readonly get => IsBlockStyle ? YamlWriterStyle.Block : YamlWriterStyle.JsonCompatible;
            set
            {
                int bits = value switch
                {
                    YamlWriterStyle.Block => BlockStyleBits,
                    YamlWriterStyle.JsonCompatible => JsonCompatibleStyleBits,
                    _ => throw new ArgumentOutOfRangeException(nameof(value)),
                };
                _optionsMask = (_optionsMask & ~StyleMask) | bits;
            }
        }

        /// <summary>
        /// In block style, whether a sequence under a key is indented (<c>key:\n  - a</c>) rather than written at the
        /// key's column (<c>key:\n- a</c>, the default). Ignored in JSON-compatible style.
        /// </summary>
        public bool IndentSequences
        {
            readonly get => (_optionsMask & IndentSequencesBit) != 0;
            set
            {
                if (value)
                    _optionsMask |= IndentSequencesBit;
                else
                    _optionsMask &= ~IndentSequencesBit;
            }
        }

        /// <summary>
        /// In block style, how a string value with a line break is written when no style hint applies to it:
        /// <see cref="YamlScalarStyle.Literal"/> (<c>|</c>, also what <see cref="YamlScalarStyle.None"/>, the default,
        /// means), <see cref="YamlScalarStyle.Folded"/> (<c>&gt;</c>) or <see cref="YamlScalarStyle.DoubleQuoted"/>
        /// (<c>"a\nb"</c>, the default before M5f). A string that a block scalar cannot hold is double-quoted whatever this
        /// says: a <c>\r</c>, a control character, NEL, LS or PS, a line made only of spaces, no character but line
        /// breaks, a first line that starts with a tab, or, at the root, a first line that starts with a space. Keys,
        /// strings in a flow collection and strings written in segments are never block scalars. Ignored in
        /// JSON-compatible style.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">
        /// The value is <see cref="YamlScalarStyle.Plain"/>, <see cref="YamlScalarStyle.SingleQuoted"/> or not a scalar style.
        /// </exception>
        public YamlScalarStyle MultiLineStringStyle
        {
            readonly get => (YamlScalarStyle)((_optionsMask & MultiLineStringStyleMask) >> MultiLineStringStyleShift);
            set
            {
                if (value is not (YamlScalarStyle.None or YamlScalarStyle.DoubleQuoted or YamlScalarStyle.Literal or YamlScalarStyle.Folded))
                {
                    ThrowHelper.ThrowArgumentOutOfRangeException_ScalarStyle(nameof(value));
                }

                _optionsMask = (_optionsMask & ~MultiLineStringStyleMask) | ((int)value << MultiLineStringStyleShift);
            }
        }

        /// <summary>
        /// In block style, the style for every string value that has no style of its own: no
        /// <see cref="Utf8YamlWriter.WriteScalarStyle"/> hint, no member <see cref="Serialization.YamlScalarStyleAttribute"/>
        /// and no source style kept by a <see cref="YamlDocument"/> or a <see cref="Nodes.YamlNode"/>.
        /// <see cref="YamlScalarStyle.None"/>, the default, leaves the writer's own choice. For a string with a line
        /// break, a <see cref="MultiLineStringStyle"/> that is set goes first. The style is applied where the string can
        /// be written in it, as a hint is; otherwise the string is written as it would be without it: plain cannot hold a
        /// line break, a block scalar needs one, single quotes over several lines cannot hold a tab or a space next to a
        /// line break, and inside a flow collection a string is never a block scalar. A value the writer writes as a YAML
        /// string from its own text (a date, a time, a GUID, Base64) takes a single- or double-quoted default, and keeps its
        /// form under the others. A key is styled only when the serializer or the DOM writes a dictionary's or a
        /// <see cref="Nodes.YamlObject"/>'s string key; member names and names written with
        /// <see cref="Utf8YamlWriter.WritePropertyName(string)"/> are not, and a key cannot be a block scalar. Numbers,
        /// booleans and nulls are not styled, and a string written in segments stays double-quoted. Ignored in
        /// JSON-compatible style.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">The value is not a scalar style.</exception>
        public YamlScalarStyle DefaultScalarStyle
        {
            readonly get => (YamlScalarStyle)((_optionsMask & DefaultScalarStyleMask) >> DefaultScalarStyleShift);
            set
            {
                if (value is < YamlScalarStyle.None or > YamlScalarStyle.Folded)
                {
                    ThrowHelper.ThrowArgumentOutOfRangeException_ScalarStyle(nameof(value));
                }

                _optionsMask = (_optionsMask & ~DefaultScalarStyleMask) | ((int)value << DefaultScalarStyleShift);
            }
        }

        /// <summary>
        /// In block style, whether a <see cref="YamlDocument"/>, a <see cref="YamlElement"/> or a
        /// <see cref="Nodes.YamlNode"/> is written in this writer's own styles rather than its source's: collections in
        /// the writer's style (block), scalars and keys as the writer would choose them for the same text (so a YAML
        /// source's quoted keys and strings are written plain where plain is safe), with
        /// <see cref="DefaultScalarStyle"/> and <see cref="MultiLineStringStyle"/> applying as they do to any value. A
        /// style set explicitly still applies: <see cref="Utf8YamlWriter.WriteScalarStyle"/>,
        /// <see cref="Utf8YamlWriter.WriteCollectionStyle"/>, a member's style, and a node's
        /// <see cref="Nodes.YamlObject.CollectionStyle"/> or <see cref="Nodes.YamlArray.CollectionStyle"/> set by the
        /// caller. Anchors and tags are content and are written as before. Off by default: written back, a document keeps
        /// its source's styles, as YamlDotNet's and SharpYaml's models do. Ignored in JSON-compatible style.
        /// </summary>
        public bool IgnoreSourceStyles
        {
            readonly get => (_optionsMask & IgnoreSourceStylesBit) != 0;
            set
            {
                if (value)
                    _optionsMask |= IgnoreSourceStylesBit;
                else
                    _optionsMask &= ~IgnoreSourceStylesBit;
            }
        }

        /// <summary>
        /// In block style, the width past which plain, double-quoted and folded scalars are folded onto the next line, at
        /// a space; 0 (the default) means no folding. A line can still be longer, when it has no space to fold at. Keys
        /// and scalars inside flow collections are not folded; instead a flow collection starts a new line before its
        /// next item or key once a line is past the width (after the comma, as libyaml's emitter does), indented one
        /// level per open flow collection past the enclosing block level. The column is kept across flushes. Ignored in
        /// JSON-compatible style.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
        public int MaxLineWidth
        {
            readonly get => _maxLineWidth;
            set
            {
                if (value < 0)
                {
                    ThrowHelper.ThrowArgumentOutOfRangeException_MaxLineWidthMustNotBeNegative(nameof(value));
                }

                _maxLineWidth = value;
            }
        }

        /// <summary>
        /// In block style, whether a collection that is a sequence item starts on the line after its dash
        /// (<c>-\n  a: 1</c>, <c>-\n  - 1</c>) rather than on the dash's line (<c>- a: 1</c>, <c>- - 1</c>, the default).
        /// Ignored in JSON-compatible style.
        /// </summary>
        public bool ExpandSequenceItems
        {
            readonly get => (_optionsMask & ExpandSequenceItemsBit) != 0;
            set
            {
                if (value)
                    _optionsMask |= ExpandSequenceItemsBit;
                else
                    _optionsMask &= ~ExpandSequenceItemsBit;
            }
        }

        /// <summary>
        /// In block style, also quotes the strings that some readers read as something other than a string although
        /// neither YAML 1.2 nor YAML 1.1 resolves them: keywords in a case no standard lists (<c>tRuE</c>, <c>yEs</c>),
        /// the IEEE names (<c>nan</c>, <c>Infinity</c>), number forms with underscores or white space where no standard
        /// accepts them (<c>_1</c>, <c>-0o17</c>), Unicode white space at an edge, keys with <c>*</c> after a space,
        /// <c>?</c> and <c>:</c> in plain scalars inside flow collections, literal scalars with a line that starts
        /// with a tab (written double-quoted), and the four kinds of form Ruby's Psych 3.1 reads as a number, a date or
        /// a time, or fails on, where neither standard does: sexagesimal numbers with a leading zero such as
        /// <c>09:30</c>, digits with commas such as <c>1,000</c>, dates and timestamps by Psych's own patterns (a
        /// one-digit month or day such as <c>2024-3-1</c>, a leading <c>-</c> such as <c>-2001-12-14 21:59:43</c>, a
        /// zone such as <c>+0500</c>), and a float with no digit before the exponent such as <c>.e+0</c>. The readers it
        /// was checked against
        /// are YamlDotNet, SharpYaml, VYaml, Psych 3.1 and PyYAML 6. Off by default: the default output is standard YAML
        /// that every conforming reader reads back as written; this option accommodates the readers that do not.
        /// Ignored in JSON-compatible style.
        /// </summary>
        public bool QuoteForLenientReaders
        {
            readonly get => (_optionsMask & QuoteForLenientReadersBit) != 0;
            set
            {
                if (value)
                    _optionsMask |= QuoteForLenientReadersBit;
                else
                    _optionsMask &= ~QuoteForLenientReadersBit;
            }
        }

        /// <summary>
        /// M7c: the serializer's <see cref="YamlSchemaKind.Json"/>.
        /// Under the JSON schema a plain scalar must be null, a boolean or a number, so in block style a string value the
        /// core schema would write plain is double-quoted, and so is a date, a time or a GUID written from its text.
        /// Keys, which the JSON schema reads plain, are unchanged.
        /// </summary>
        internal bool QuoteStringsForJsonSchema
        {
            readonly get => (_optionsMask & JsonSchemaBit) != 0;
            set
            {
                if (value)
                    _optionsMask |= JsonSchemaBit;
                else
                    _optionsMask &= ~JsonSchemaBit;
            }
        }

        // From M4c, a style that is not set means block, unless the
        // JSON-compatible switch is on (YamlWriterStyleDefault).
        internal readonly bool IsBlockStyle => (_optionsMask & StyleMask) switch
        {
            BlockStyleBits => true,
            JsonCompatibleStyleBits => false,
            _ => YamlWriterStyleDefault.UnsetStyleIsBlock,
        };

        /// <summary>Whether the style was set explicitly, for the serializer's pinned paths.</summary>
        internal readonly bool IsStyleSet => (_optionsMask & StyleMask) != 0;

        private const int OptionsBitCount = 4;
        private const int IndentBit = 1;
        private const int SkipValidationBit = 2;
        private const int NewLineBit = 4;
        private const int IndentCharacterBit = 8;
        private const int IndentSizeMask = YamlConstants.MaximumIndentSize << OptionsBitCount;
        private const int StyleShift = 16;
        private const int StyleMask = 3 << StyleShift;
        private const int BlockStyleBits = 1 << StyleShift;
        private const int JsonCompatibleStyleBits = 2 << StyleShift;
        private const int IndentSequencesBit = 1 << 18;
        private const int MultiLineStringStyleShift = 19;
        private const int MultiLineStringStyleMask = 7 << MultiLineStringStyleShift;
        private const int ExpandSequenceItemsBit = 1 << 22;
        private const int QuoteForLenientReadersBit = 1 << 23;
        private const int DefaultScalarStyleShift = 24;
        private const int DefaultScalarStyleMask = 7 << DefaultScalarStyleShift;
        private const int IgnoreSourceStylesBit = 1 << 27;
        private const int JsonSchemaBit = 1 << 28;
    }
}
