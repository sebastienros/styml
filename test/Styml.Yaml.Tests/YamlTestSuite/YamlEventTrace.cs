// Copyright (c) Sébastien Ros. Licensed under the MIT license.

using System.Collections.Generic;
using System.Text;

namespace System.Text.Yaml.Tests
{
    /// <summary>
    /// Prints the tokens of <see cref="Utf8YamlReader"/> in the event notation of yaml-test-suite (test.event).
    /// </summary>
    /// <remarks>
    /// The token model has no document events: a document is one root
    /// value, so '+DOC'/'-DOC' are derived from depth and the explicit-marker suffixes (' ---', ' ...') are not
    /// produced. <see cref="NormalizeExpected"/> removes them from the expected trace. Tags are printed expanded
    /// (<see cref="Utf8YamlReader.GetExpandedTag"/>, M3d).
    /// </remarks>
    public static class YamlEventTrace
    {
        public static List<string> Read(ReadOnlySpan<byte> yaml)
        {
            var options = new YamlReaderOptions { AllowMultipleValues = true, MaxDepth = 256 };
            var reader = new Utf8YamlReader(yaml, options);
            return Read(ref reader);
        }

        public static List<string> Read(ref Utf8YamlReader reader)
        {
            var events = new List<string> { "+STR" };
            while (reader.Read())
            {
                if (reader.CurrentDepth == 0 && reader.TokenType is not (YamlTokenType.EndObject or YamlTokenType.EndArray))
                {
                    events.Add("+DOC");
                }

                AddEvent(ref reader, events);

                if (reader.CurrentDepth == 0 && reader.TokenType is not (YamlTokenType.StartObject or YamlTokenType.StartArray))
                {
                    events.Add("-DOC");
                }
            }
            events.Add("-STR");
            return events;
        }

        public static void AddEvent(ref Utf8YamlReader reader, List<string> events)
        {
            var sb = new StringBuilder();
            switch (reader.TokenType)
            {
                case YamlTokenType.StartObject:
                    sb.Append("+MAP");
                    if (reader.IsFlow)
                    {
                        sb.Append(" {}");
                    }
                    AppendProperties(ref reader, sb);
                    break;
                case YamlTokenType.StartArray:
                    sb.Append("+SEQ");
                    if (reader.IsFlow)
                    {
                        sb.Append(" []");
                    }
                    AppendProperties(ref reader, sb);
                    break;
                case YamlTokenType.EndObject:
                    sb.Append("-MAP");
                    break;
                case YamlTokenType.EndArray:
                    sb.Append("-SEQ");
                    break;
                case YamlTokenType.Alias:
                case YamlTokenType.PropertyName when reader.KeyKind == YamlKeyKind.Alias:
                    sb.Append("=ALI *").Append(Encoding.UTF8.GetString(reader.HasValueSequence ? System.Buffers.BuffersExtensions.ToArray(reader.ValueSequence) : reader.ValueSpan.ToArray()));
                    break;
                case YamlTokenType.PropertyName when reader.KeyKind == YamlKeyKind.Complex:
                    // A collection key (D10): its own events come from a reader scoped to the key node.
                    Utf8YamlReader keyReader = reader.CreateKeyReader();
                    while (keyReader.Read())
                    {
                        AddEvent(ref keyReader, events);
                    }
                    return;
                case YamlTokenType.Comment:
                    return;
                default:
                    sb.Append("=VAL");
                    AppendProperties(ref reader, sb);
                    sb.Append(' ').Append(StyleIndicator(reader.ScalarStyle));
                    sb.Append(Escape(ScalarText(ref reader)));
                    break;
            }
            events.Add(sb.ToString());
        }

        /// <summary>The scalar's value as text, for any scalar token type (decision D2 makes GetString accept them).</summary>
        public static string ScalarText(ref Utf8YamlReader reader)
        {
            if (reader.TokenType == YamlTokenType.Null)
            {
                return Encoding.UTF8.GetString(reader.HasValueSequence ? System.Buffers.BuffersExtensions.ToArray(reader.ValueSequence) : reader.ValueSpan.ToArray());
            }
            return reader.GetString() ?? string.Empty;
        }

        private static void AppendProperties(ref Utf8YamlReader reader, StringBuilder sb)
        {
            if (!reader.Anchor.IsEmpty)
            {
                sb.Append(" &").Append(Encoding.UTF8.GetString(reader.Anchor.ToArray()));
            }
            if (!reader.Tag.IsEmpty)
            {
                sb.Append(" <").Append(reader.GetExpandedTag()).Append('>');
            }
        }

        private static char StyleIndicator(YamlScalarStyle style) => style switch
        {
            YamlScalarStyle.SingleQuoted => '\'',
            YamlScalarStyle.DoubleQuoted => '"',
            YamlScalarStyle.Literal => '|',
            YamlScalarStyle.Folded => '>',
            _ => ':',
        };

        private static string Escape(string value)
        {
            var sb = new StringBuilder(value.Length);
            foreach (char ch in value)
            {
                switch (ch)
                {
                    case '\\': sb.Append("\\\\"); break;
                    case '\0': sb.Append("\\0"); break;
                    case '\a': sb.Append("\\a"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\t': sb.Append("\\t"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\v': sb.Append("\\v"); break;
                    case '\f': sb.Append("\\f"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\x1B': sb.Append("\\e"); break;
                    default: sb.Append(ch); break;
                }
            }
            return sb.ToString();
        }

        /// <summary>Removes what the token model cannot express: the explicit document-marker suffixes.</summary>
        public static List<string> NormalizeExpected(IEnumerable<string> lines)
        {
            var result = new List<string>();
            foreach (string line in lines)
            {
                if (line.Length == 0)
                {
                    continue;
                }
                if (line == "+DOC ---" || line.StartsWith("+DOC ", StringComparison.Ordinal))
                {
                    result.Add("+DOC");
                }
                else if (line == "-DOC ...")
                {
                    result.Add("-DOC");
                }
                else
                {
                    result.Add(line);
                }
            }
            return result;
        }
    }
}
