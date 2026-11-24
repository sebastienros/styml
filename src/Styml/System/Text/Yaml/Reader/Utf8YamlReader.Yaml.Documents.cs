// Copyright (c) Sébastien Ros. Licensed under the MIT license.

using System.Diagnostics;
using System.Globalization;

namespace System.Text.Yaml
{
    public ref partial struct Utf8YamlReader
    {
        /// <summary>
        /// Reads at depth 0: between documents, document markers and directives, and the root node of a document.
        /// </summary>
        private YamlStep YamlReadDocument(ref YamlCursor c)
        {
            while (true)
            {
                if (!YamlScanToContent(ref c, out YamlScan s, stopAtComment: YamlAllowComments))
                {
                    return YamlStep.NeedMoreData;
                }

                if (s.IsComment)
                {
                    return YamlEmitComment(ref c);
                }

                if (s.AtEnd)
                {
                    if (_yaml.Has(YamlParseFlags.DocumentOpen))
                    {
                        // '---' followed by nothing: one null document.
                        return YamlEmitNull(c, s.LineStart, default, c.Pos, c.Line, c.Column);
                    }

                    if (_yaml.Has(YamlParseFlags.DirectivesSeen))
                    {
                        YamlThrow(YamlGrammarError.DirectiveWithoutDocument, c);
                    }

                    YamlCommit(c);
                    return YamlStep.EndOfInput;
                }

                byte b = _buffer[c.Pos];
                if (s.LineStart && c.Column == 0)
                {
                    if (b == (byte)'%')
                    {
                        if (_yaml.Has(YamlParseFlags.DocumentOpen))
                        {
                            YamlThrow(YamlGrammarError.ReservedIndicator, c, "%");
                        }
                        if (_yaml.Has(YamlParseFlags.RootDone))
                        {
                            YamlThrow(YamlGrammarError.DirectiveAfterContent, c);
                        }
                        if (!YamlReadDirective(ref c))
                        {
                            return YamlStep.NeedMoreData;
                        }
                        continue;
                    }

                    int marker = YamlDocumentMarker(c, s.LineStart);
                    if (marker < 0)
                    {
                        return YamlStep.NeedMoreData;
                    }

                    if (marker == 1)
                    {
                        if (_yaml.Has(YamlParseFlags.DocumentOpen))
                        {
                            return YamlEmitNull(c, s.LineStart, default, c.Pos, c.Line, c.Column);
                        }

                        if (_yaml.Has(YamlParseFlags.AnyDocument) && !_readerOptions.AllowMultipleValues)
                        {
                            YamlThrow(YamlGrammarError.MultipleDocuments, c);
                        }

                        if (!_yaml.Has(YamlParseFlags.DirectivesSeen) && !InheritsTagDirectives)
                        {
                            // YAML 1.2: a document without directives has only the default handles. Under
                            // InheritTagDirectives (YAML 1.1) it keeps the previous document's.
                            _yaml.TagDirectives = null;
                        }

                        c.Pos += 3;
                        c.Column += 3;
                        _yaml.Set(YamlParseFlags.RootDone | YamlParseFlags.DirectivesSeen | YamlParseFlags.YamlDirectiveSeen, false);
                        _yaml.Set(YamlParseFlags.DocumentOpen | YamlParseFlags.AnyDocument, true);
                        YamlStartAnchorDocument(c.Pos);
                        continue;
                    }

                    if (marker == 2)
                    {
                        if (_yaml.Has(YamlParseFlags.DocumentOpen))
                        {
                            return YamlEmitNull(c, s.LineStart, default, c.Pos, c.Line, c.Column);
                        }
                        if (_yaml.Has(YamlParseFlags.DirectivesSeen))
                        {
                            YamlThrow(YamlGrammarError.DirectiveWithoutDocument, c);
                        }

                        c.Pos += 3;
                        c.Column += 3;
                        _yaml.Set(YamlParseFlags.RootDone, false);
                        if (!YamlExpectLineEnd(ref c))
                        {
                            return YamlStep.NeedMoreData;
                        }
                        continue;
                    }
                }

                if (_yaml.Has(YamlParseFlags.RootDone))
                {
                    YamlThrow(b == (byte)':' && !s.LineStart ? YamlKeyAfterNodeError(c) : YamlGrammarError.ExpectedDocumentEnd, c);
                }

                if (_yaml.Has(YamlParseFlags.DirectivesSeen) && !_yaml.Has(YamlParseFlags.DocumentOpen))
                {
                    YamlThrow(YamlGrammarError.DirectiveWithoutDocument, c);
                }

                bool onMarkerLine = false;
                if (_yaml.Has(YamlParseFlags.DocumentOpen))
                {
                    onMarkerLine = !s.LineStart;
                }
                else
                {
                    // A bare document.
                    if (_yaml.Has(YamlParseFlags.AnyDocument) && !_readerOptions.AllowMultipleValues)
                    {
                        YamlThrow(YamlGrammarError.MultipleDocuments, c);
                    }
                    if (_yaml.Has(YamlParseFlags.AnyDocument))
                    {
                        // A later bare document has no directives. The first one keeps the table it starts with:
                        // empty, or the parent's for a reader scoped to one value. Under InheritTagDirectives a later
                        // one keeps the previous document's.
                        if (!InheritsTagDirectives)
                        {
                            _yaml.TagDirectives = null;
                        }

                        YamlStartAnchorDocument(c.Pos);
                    }
                    _yaml.Set(YamlParseFlags.AnyDocument, true);
                }

                bool documentLine = onMarkerLine || _yaml.Has(YamlParseFlags.ScopedDocumentLine);
                YamlStep step = YamlParseBlockNode(
                    ref c,
                    s,
                    parentIndent: _yaml.BaseIndent,
                    allowBlockCollection: !onMarkerLine,
                    allowIndentlessSequence: false,
                    documentLine: documentLine);
                if (step == YamlStep.Token)
                {
                    _yaml.Set(YamlParseFlags.ScopedDocumentLine, false);
                }

                return step;
            }
        }

        // A later document without directives keeps the previous one's %TAG handles: under the option, or in a stream that
        // declared %YAML 1.1.
        private readonly bool InheritsTagDirectives => _readerOptions.InheritTagDirectives || _yaml.Has(YamlParseFlags.Yaml11Scope);

        /// <summary>Reads one directive line (<c>%YAML</c>, <c>%TAG</c> or a reserved directive).</summary>
        private bool YamlReadDirective(ref YamlCursor c)
        {
            Debug.Assert(_buffer[c.Pos] == (byte)'%');
            if (!_yaml.Has(YamlParseFlags.DirectivesSeen))
            {
                // A document's first directive starts its own table, under InheritTagDirectives too (YAML 1.1 section 7.4:
                // "all directives of previous documents, if any, are ignored").
                _yaml.TagDirectives = null;
            }
            ReadOnlySpan<byte> rest = _buffer.Slice(c.Pos);
            int lineLength = YamlReaderHelper.IndexOfLineBreak(rest);
            if (lineLength < 0)
            {
                if (!IsLastSpan)
                {
                    return false;
                }
                lineLength = rest.Length;
            }

            ReadOnlySpan<byte> line = rest.Slice(1, lineLength - 1);
            int nameLength = YamlReaderHelper.IndexOfBlank(line);
            ReadOnlySpan<byte> name = nameLength < 0 ? line : line.Slice(0, nameLength);
            ReadOnlySpan<byte> args = nameLength < 0 ? default : line.Slice(nameLength);

            // Strip a trailing comment from the arguments.
            int comment = YamlReaderHelper.IndexOfComment(args);
            if (comment >= 0)
            {
                args = args.Slice(0, comment);
            }
            args = YamlReaderHelper.TrimBlanks(args);

            if (name.Length == 0)
            {
                YamlThrow(YamlGrammarError.InvalidDirective, c);
            }

            if (name.SequenceEqual("YAML"u8))
            {
                if (_yaml.Has(YamlParseFlags.YamlDirectiveSeen))
                {
                    YamlThrow(YamlGrammarError.InvalidDirective, c);
                }

                // version: digits '.' digits, major version 1.
                int dot = args.IndexOf((byte)'.');
                if (dot <= 0 || dot == args.Length - 1 || !YamlReaderHelper.AllDigits(args.Slice(0, dot)) || !YamlReaderHelper.AllDigits(args.Slice(dot + 1)))
                {
                    YamlThrow(YamlGrammarError.InvalidDirective, c);
                }
                if (!args.Slice(0, dot).SequenceEqual("1"u8))
                {
                    YamlThrow(YamlGrammarError.UnsupportedYamlVersion, c, YamlReaderHelper.TranscodeHelper(args));
                }
                _yaml.Set(YamlParseFlags.YamlDirectiveSeen, true);

                // %YAML 1.0 or 1.1: YAML 1.1's directive scope from this document on; 1.2 or later ends it.
                _yaml.Set(YamlParseFlags.Yaml11Scope, YamlReaderHelper.IsYaml11Version(args.Slice(dot + 1)));
            }
            else if (name.SequenceEqual("TAG"u8))
            {
                // %TAG <handle> <prefix>; handles are '!', '!!' or '!word!'.
                int split = YamlReaderHelper.IndexOfBlank(args);
                if (split <= 0)
                {
                    YamlThrow(YamlGrammarError.InvalidDirective, c);
                }
                ReadOnlySpan<byte> handle = args.Slice(0, split);
                ReadOnlySpan<byte> prefix = YamlReaderHelper.TrimBlanks(args.Slice(split));
                if (prefix.Length == 0 || YamlReaderHelper.IndexOfBlank(prefix) >= 0 || handle[0] != (byte)'!' || handle[handle.Length - 1] != (byte)'!')
                {
                    YamlThrow(YamlGrammarError.InvalidDirective, c);
                }
                if (!YamlTagDirectives.TryAdd(_yaml.TagDirectives, handle, prefix, out YamlTagDirectives? table))
                {
                    YamlThrow(YamlGrammarError.TooManyTagDirectives, c, YamlTagDirectives.MaxEntries.ToString(CultureInfo.InvariantCulture));
                }

                _yaml.TagDirectives = table;
            }

            // Other (reserved) directives are ignored, as the specification recommends.
            c.Pos += lineLength;
            c.Column += lineLength;
            _yaml.Set(YamlParseFlags.DirectivesSeen, true);
            return true;
        }
    }
}
