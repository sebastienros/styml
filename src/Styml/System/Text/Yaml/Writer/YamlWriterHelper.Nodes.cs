// Copyright (c) Sébastien Ros. Licensed under the MIT license.

namespace System.Text.Yaml
{
    // The checks of M4b's block-style members: anchor names, tags,
    // directives, comments, and the extra rules of single-quoted scalars and of plain scalars in flow collections.
    internal static partial class YamlWriterHelper
    {
        internal enum DirectiveKind : byte
        {
            None,
            Yaml,
            Tag,
        }

        // The length of the UTF-8 sequence that starts at s[i] when it is a character that may appear in an anchor name,
        // a comment or a single-quoted scalar (YAML's printable set, less the characters YAML 1.1 reads as line breaks
        // and the byte order mark), or 0. The text is valid UTF-8 (the callers checked it or produced it).
        private static int PrintableLength(ReadOnlySpan<byte> s, int i)
        {
            byte b = s[i];
            if (b < 0x80)
            {
                return b >= 0x20 && b != 0x7F ? 1 : 0;
            }

            if (b == 0xC2 && s[i + 1] <= 0x9F)
            {
                return 0; // C1 controls and NEL
            }

            if (b == 0xE2 && s[i + 1] == 0x80 && s[i + 2] is 0xA8 or 0xA9)
            {
                return 0; // LS and PS
            }

            if (b == 0xEF && ((s[i + 1] == 0xBB && s[i + 2] == 0xBF) || (s[i + 1] == 0xBF && s[i + 2] is 0xBE or 0xBF)))
            {
                return 0; // U+FEFF, U+FFFE, U+FFFF
            }

            return b >= 0xF0 ? 4 : b >= 0xE0 ? 3 : 2;
        }

        /// <summary>
        /// One or more <c>ns-anchor-char</c>: printable, no white space, no flow indicator (YAML 1.2, section 6.9.2).
        /// </summary>
        internal static bool IsValidAnchorName(ReadOnlySpan<byte> name)
        {
            if (name.IsEmpty || !IsValidUtf8String(name))
            {
                return false;
            }

            int i = 0;
            while (i < name.Length)
            {
                byte b = name[i];
                if (b is (byte)' ' or (byte)',' or (byte)'[' or (byte)']' or (byte)'{' or (byte)'}')
                {
                    return false;
                }

                int length = PrintableLength(name, i);
                if (length == 0)
                {
                    return false;
                }

                i += length;
            }

            return true;
        }

        // ns-uri-char, ASCII only (a percent escape is checked by the caller): word characters and #;/?:@&=+$,_.!~*'()[]
        private static bool IsUriChar(byte b) =>
            (b >= (byte)'0' && b <= (byte)'9') || (b >= (byte)'a' && b <= (byte)'z') || (b >= (byte)'A' && b <= (byte)'Z')
            || b is (byte)'-' or (byte)'#' or (byte)';' or (byte)'/' or (byte)'?' or (byte)':' or (byte)'@' or (byte)'&'
                or (byte)'=' or (byte)'+' or (byte)'$' or (byte)',' or (byte)'_' or (byte)'.' or (byte)'!' or (byte)'~'
                or (byte)'*' or (byte)'\'' or (byte)'(' or (byte)')' or (byte)'[' or (byte)']';

        private static bool IsWordChar(byte b) =>
            (b >= (byte)'0' && b <= (byte)'9') || (b >= (byte)'a' && b <= (byte)'z') || (b >= (byte)'A' && b <= (byte)'Z') || b == (byte)'-';

        private static bool IsHexDigit(byte b) =>
            (b >= (byte)'0' && b <= (byte)'9') || (b >= (byte)'a' && b <= (byte)'f') || (b >= (byte)'A' && b <= (byte)'F');

        // One or more URI characters (tagChars: ns-tag-char, which also excludes '!' and the flow indicators), or
        // percent escapes. minimum is 0 or 1.
        private static bool IsUriText(ReadOnlySpan<byte> s, bool tagChars, int minimum)
        {
            if (s.Length < minimum)
            {
                return false;
            }

            for (int i = 0; i < s.Length; i++)
            {
                byte b = s[i];
                if (b == (byte)'%')
                {
                    if (i + 2 >= s.Length || !IsHexDigit(s[i + 1]) || !IsHexDigit(s[i + 2]))
                    {
                        return false;
                    }

                    i += 2;
                    continue;
                }

                if (!IsUriChar(b) || (tagChars && b is (byte)'!' or (byte)',' or (byte)'[' or (byte)']'))
                {
                    return false;
                }
            }

            return true;
        }

        // A tag handle: "!", "!!" or "!" word-chars "!".
        private static bool IsTagHandle(ReadOnlySpan<byte> handle)
        {
            if (handle.Length < 1 || handle[0] != (byte)'!')
            {
                return false;
            }

            if (handle.Length == 1)
            {
                return true;
            }

            if (handle[handle.Length - 1] != (byte)'!')
            {
                return false;
            }

            for (int i = 1; i < handle.Length - 1; i++)
            {
                if (!IsWordChar(handle[i]))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// A tag as written (YAML 1.2, section 6.8.2): the non-specific <c>!</c>, a verbatim tag <c>!&lt;uri&gt;</c>, or
        /// a shorthand <c>!suffix</c>, <c>!!suffix</c> or <c>!handle!suffix</c>. Checked, not expanded.
        /// </summary>
        internal static bool IsValidTag(ReadOnlySpan<byte> tag)
        {
            if (tag.IsEmpty || tag[0] != (byte)'!')
            {
                return false;
            }

            if (tag.Length == 1)
            {
                return true;
            }

            if (tag[1] == (byte)'<')
            {
                return tag.Length > 3 && tag[tag.Length - 1] == (byte)'>' && IsUriText(tag.Slice(2, tag.Length - 3), tagChars: false, minimum: 1);
            }

            int second = tag.Slice(1).IndexOf((byte)'!');
            if (second < 0)
            {
                return IsUriText(tag.Slice(1), tagChars: true, minimum: 1);
            }

            ReadOnlySpan<byte> handle = tag.Slice(0, second + 2);
            return IsTagHandle(handle) && IsUriText(tag.Slice(handle.Length), tagChars: true, minimum: 1);
        }

        private static bool IsSeparator(byte b) => b is (byte)' ' or (byte)'\t';

        /// <summary>
        /// <c>%YAML major.minor</c> or <c>%TAG handle prefix</c> (YAML 1.2, section 6.8.1), one line, no comment.
        /// </summary>
        internal static DirectiveKind GetDirectiveKind(ReadOnlySpan<byte> directive)
        {
            if (directive.StartsWith("%YAML"u8))
            {
                ReadOnlySpan<byte> rest = directive.Slice(5);
                int spaces = 0;
                while (spaces < rest.Length && IsSeparator(rest[spaces]))
                {
                    spaces++;
                }

                if (spaces == 0)
                {
                    return DirectiveKind.None;
                }

                rest = rest.Slice(spaces);
                int dot = rest.IndexOf((byte)'.');
                return dot > 0 && dot < rest.Length - 1 && AllDigits(rest.Slice(0, dot)) && AllDigits(rest.Slice(dot + 1))
                    ? DirectiveKind.Yaml
                    : DirectiveKind.None;
            }

            if (directive.StartsWith("%TAG"u8))
            {
                ReadOnlySpan<byte> rest = directive.Slice(4);
                if (rest.IsEmpty || !IsSeparator(rest[0]))
                {
                    return DirectiveKind.None;
                }

                rest = TrimSeparators(rest);
                int end = rest.IndexOfAny((byte)' ', (byte)'\t');
                if (end < 0 || !IsTagHandle(rest.Slice(0, end)))
                {
                    return DirectiveKind.None;
                }

                ReadOnlySpan<byte> prefix = TrimSeparators(rest.Slice(end));
                if (prefix.IsEmpty)
                {
                    return DirectiveKind.None;
                }

                bool valid = prefix[0] == (byte)'!'
                    ? IsUriText(prefix.Slice(1), tagChars: false, minimum: 0)
                    : prefix[0] is not ((byte)'!' or (byte)',' or (byte)'[' or (byte)']') && IsUriText(prefix, tagChars: false, minimum: 1);
                return valid ? DirectiveKind.Tag : DirectiveKind.None;
            }

            return DirectiveKind.None;
        }

        private static ReadOnlySpan<byte> TrimSeparators(ReadOnlySpan<byte> s)
        {
            int i = 0;
            while (i < s.Length && IsSeparator(s[i]))
            {
                i++;
            }

            return s.Slice(i);
        }

        private static bool AllDigits(ReadOnlySpan<byte> s)
        {
            foreach (byte b in s)
            {
                if (b < (byte)'0' || b > (byte)'9')
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// A block-style comment: printable text and tabs, with CR, LF or CRLF between its lines (each is written with
        /// its own <c>#</c>). Valid UTF-8 is checked by the caller.
        /// </summary>
        internal static bool IsValidBlockComment(ReadOnlySpan<byte> comment)
        {
            int i = 0;
            while (i < comment.Length)
            {
                byte b = comment[i];
                if (b is (byte)'\t' or (byte)'\n' or (byte)'\r')
                {
                    i++;
                    continue;
                }

                int length = PrintableLength(comment, i);
                if (length == 0)
                {
                    return false;
                }

                i += length;
            }

            return true;
        }

        /// <summary>
        /// Whether a text can be written between single quotes, where nothing can be escaped: printable characters only,
        /// with no tab and no line break (a line break would be folded).
        /// </summary>
        internal static bool CanWriteSingleQuoted(ReadOnlySpan<byte> text)
        {
            int i = 0;
            while (i < text.Length)
            {
                int length = PrintableLength(text, i);
                if (length == 0)
                {
                    return false;
                }

                i += length;
            }

            return true;
        }

        /// <summary>
        /// M7b: whether a text with a line break can be
        /// written as a multi-line single-quoted scalar. Each line must be printable with no tab, as on one line, and no
        /// space may touch a line break: line folding strips white space around a break, so it would be lost.
        /// </summary>
        internal static bool CanWriteMultiLineSingleQuoted(ReadOnlySpan<byte> text)
        {
            if (text.IndexOf((byte)'\n') < 0)
            {
                return false;
            }

            int i = 0;
            while (i < text.Length)
            {
                if (text[i] == (byte)'\n')
                {
                    if ((i > 0 && text[i - 1] == YamlConstants.Space) || (i + 1 < text.Length && text[i + 1] == YamlConstants.Space))
                    {
                        return false;
                    }

                    i++;
                    continue;
                }

                int length = PrintableLength(text, i);
                if (length == 0)
                {
                    return false;
                }

                i += length;
            }

            return true;
        }

        /// <summary>
        /// The extra rule of a plain scalar inside a flow collection (M4b): no flow indicator (and so no ':' followed
        /// by one). With <paramref name="lenient"/> (<see cref="YamlWriterOptions.QuoteForLenientReaders"/>), no ':' and no
        /// '?' either: SharpYaml ends a plain scalar at '?' in a flow collection, as libyaml's older scanners do, where
        /// YAML 1.2 and 1.1 read it as text (M4b's cross-read).
        /// </summary>
        internal static bool IsFlowPlain(ReadOnlySpan<byte> text, bool lenient = false) =>
            text.IndexOfAny(lenient ? FlowPlainStopLenient : FlowPlainStop) < 0;

        private static ReadOnlySpan<byte> FlowPlainStop => ",[]{}"u8;

        private static ReadOnlySpan<byte> FlowPlainStopLenient => ",[]{}:?"u8;

        /// <summary>The number of characters (UTF-8 sequences) in the text.</summary>
        internal static int CountCharacters(ReadOnlySpan<byte> text)
        {
            int count = 0;
            foreach (byte b in text)
            {
                if ((b & 0xC0) != 0x80)
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>
        /// Whether a plain scalar may continue on a new line with this character first: not an indicator, '#' or a
        /// space, so the continuation reads as text in every reader.
        /// </summary>
        internal static bool CanStartPlainContinuation(byte b) =>
            b is not ((byte)' ' or (byte)'\t' or (byte)'#' or (byte)'-' or (byte)'?' or (byte)':' or (byte)',' or (byte)'['
                or (byte)']' or (byte)'{' or (byte)'}' or (byte)'&' or (byte)'*' or (byte)'!' or (byte)'|' or (byte)'>'
                or (byte)'\'' or (byte)'"' or (byte)'%' or (byte)'@' or (byte)'`');
    }
}
