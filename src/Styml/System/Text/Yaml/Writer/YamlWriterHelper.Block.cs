// Copyright (c) Sébastien Ros. Licensed under the MIT license.

using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Encodings.Web;

namespace System.Text.Yaml
{
    // Block style's scalar rules: whether a string can be written plain
    // (the syntactic check, and 5.3, the resolution check), and the escaper of double-quoted scalars. Every
    // function takes valid UTF-8; the callers validate or transcode first.
    internal static partial class YamlWriterHelper
    {
        // Bytes at which the plain check must look closer: C0 controls (tab and the line breaks included), DEL, ':'
        // and '#' (checked against their neighbours), and the lead bytes of the non-ASCII characters that disqualify
        // plain style: C1 controls and NEL (C2 80..9F), LS and PS (E2 80 A8/A9), U+FEFF (EF BB BF) and the two
        // non-characters U+FFFE and U+FFFF (EF BF BE/BF), which YAML's printable set excludes.
        private static readonly byte[] s_blockPlainStopBytes =
        [
            0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x0A, 0x0B, 0x0C, 0x0D, 0x0E, 0x0F,
            0x10, 0x11, 0x12, 0x13, 0x14, 0x15, 0x16, 0x17, 0x18, 0x19, 0x1A, 0x1B, 0x1C, 0x1D, 0x1E, 0x1F,
            0x7F, (byte)':', (byte)'#', 0xC2, 0xE2, 0xEF,
        ];

        // Bytes at which the double-quoted escaper must look closer: the same set, without ':' and '#', with '"' and
        // '\'.
        private static readonly byte[] s_blockEscapeStopBytes =
        [
            0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x0A, 0x0B, 0x0C, 0x0D, 0x0E, 0x0F,
            0x10, 0x11, 0x12, 0x13, 0x14, 0x15, 0x16, 0x17, 0x18, 0x19, 0x1A, 0x1B, 0x1C, 0x1D, 0x1E, 0x1F,
            0x7F, (byte)'"', (byte)'\\', 0xC2, 0xE2, 0xEF,
        ];

        // The plain check of text that the JSON-compatible escaping has already run over (the block branch of a dispatch
        // point): the same set, with '\\', since a backslash there is always an escape.
        private static readonly byte[] s_escapedTextStopBytes = [.. s_blockPlainStopBytes, (byte)'\\'];

        // ScanUnescapedAscii's sets (M4p, candidate b). The plain bytes: printable ASCII but ':' and '#' (checked against
        // their neighbours), '"' and '\\'. The rest's bytes, once the text is not plain: ASCII but '"' and '\\', the bytes
        // YamlBlockEncoder.Instance leaves as they are.
        private static byte[] OneScanBytes(bool rest)
        {
            var bytes = new System.Collections.Generic.List<byte>();
            for (int b = rest ? 0 : 0x20; b < (rest ? 0x80 : 0x7F); b++)
            {
                if (b is not ((int)'"' or (int)'\\') && (rest || b is not ((int)':' or (int)'#')))
                {
                    bytes.Add((byte)b);
                }
            }

            return bytes.ToArray();
        }

#if NET
        private static readonly SearchValues<byte> s_oneScanPlain = SearchValues.Create(OneScanBytes(rest: false));
        private static readonly SearchValues<byte> s_oneScanRest = SearchValues.Create(OneScanBytes(rest: true));
        private static int IndexOfOneScanStop(ReadOnlySpan<byte> s) => s.IndexOfAnyExcept(s_oneScanPlain);
        private static int IndexOfOneScanRestStop(ReadOnlySpan<byte> s) => s.IndexOfAnyExcept(s_oneScanRest);
        private static readonly SearchValues<char> s_oneScanPlainChars = SearchValues.Create(Encoding.ASCII.GetString(OneScanBytes(rest: false)));
        private static readonly SearchValues<char> s_oneScanRestChars = SearchValues.Create(Encoding.ASCII.GetString(OneScanBytes(rest: true)));
        private static int IndexOfOneScanStop(ReadOnlySpan<char> s) => s.IndexOfAnyExcept(s_oneScanPlainChars);
        private static int IndexOfOneScanRestStop(ReadOnlySpan<char> s) => s.IndexOfAnyExcept(s_oneScanRestChars);
#else
        private static readonly bool[] s_oneScanPlain = ToTable(OneScanBytes(rest: false));
        private static readonly bool[] s_oneScanRest = ToTable(OneScanBytes(rest: true));
        private static int IndexOfOneScanStop(ReadOnlySpan<byte> s) => IndexOfAnyExcept(s, s_oneScanPlain);
        private static int IndexOfOneScanRestStop(ReadOnlySpan<byte> s) => IndexOfAnyExcept(s, s_oneScanRest);

        private static int IndexOfOneScanStop(ReadOnlySpan<char> s) => IndexOfAnyExcept(s, s_oneScanPlain);
        private static int IndexOfOneScanRestStop(ReadOnlySpan<char> s) => IndexOfAnyExcept(s, s_oneScanRest);

        private static int IndexOfAnyExcept(ReadOnlySpan<byte> s, bool[] table)
        {
            for (int i = 0; i < s.Length; i++)
            {
                if (!table[s[i]])
                {
                    return i;
                }
            }

            return -1;
        }

        // Above 0xFF is never in a table (the tables hold ASCII only).
        private static int IndexOfAnyExcept(ReadOnlySpan<char> s, bool[] table)
        {
            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] > 0xFF || !table[s[i]])
                {
                    return i;
                }
            }

            return -1;
        }
#endif

#if NET
        private static readonly SearchValues<byte> s_escapedTextStop = SearchValues.Create(s_escapedTextStopBytes);
        private static int IndexOfEscapedTextStop(ReadOnlySpan<byte> s) => s.IndexOfAny(s_escapedTextStop);
        private static readonly SearchValues<byte> s_blockPlainStop = SearchValues.Create(s_blockPlainStopBytes);
        private static readonly SearchValues<byte> s_blockEscapeStop = SearchValues.Create(s_blockEscapeStopBytes);

        private static int IndexOfBlockPlainStop(ReadOnlySpan<byte> s) => s.IndexOfAny(s_blockPlainStop);
        private static int IndexOfBlockEscapeStop(ReadOnlySpan<byte> s) => s.IndexOfAny(s_blockEscapeStop);
#else
        private static readonly bool[] s_escapedTextStop = ToTable(s_escapedTextStopBytes);
        private static int IndexOfEscapedTextStop(ReadOnlySpan<byte> s) => IndexOfAny(s, s_escapedTextStop);
        private static readonly bool[] s_blockPlainStop = ToTable(s_blockPlainStopBytes);
        private static readonly bool[] s_blockEscapeStop = ToTable(s_blockEscapeStopBytes);

        private static bool[] ToTable(byte[] bytes)
        {
            var table = new bool[256];
            foreach (byte b in bytes)
            {
                table[b] = true;
            }

            return table;
        }

        private static int IndexOfAny(ReadOnlySpan<byte> s, bool[] table)
        {
            for (int i = 0; i < s.Length; i++)
            {
                if (table[s[i]])
                {
                    return i;
                }
            }

            return -1;
        }

        private static int IndexOfBlockPlainStop(ReadOnlySpan<byte> s) => IndexOfAny(s, s_blockPlainStop);
        private static int IndexOfBlockEscapeStop(ReadOnlySpan<byte> s) => IndexOfAny(s, s_blockEscapeStop);
#endif

        // The indicators that may not start a plain scalar.
        private static ReadOnlySpan<byte> BlockIndicator =>
        [
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            //   !  "  #  $  %  &  '  (  )  *  +  ,  -  .  /  0  1  2  3  4  5  6  7  8  9  :  ;  <  =  >  ?
            0, 1, 1, 1, 0, 1, 1, 1, 0, 0, 1, 0, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 1, 1,
            // @  A ... Z  [  \  ]  ^  _
            1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 1, 0, 0,
            // `  a ... z  {  |  }  ~  DEL
            1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 0, 0,
        ];

        // The resolution check's first-byte prefilter. Every string the resolver or the keyword and legacy-form matcher can
        // turn into a non-string starts with one of these bytes (a test derives this by enumeration). The quoting for
        // lenient readers (QuoteForLenientReaders) does not go through it.
        private static ReadOnlySpan<byte> ResolutionPrefilter =>
        [
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            //   !  "  #  $  %  &  '  (  )  *  +  ,  -  .  /  0  1  2  3  4  5  6  7  8  9  :  ;  <  =  >  ?
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 1, 1, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 1, 1, 0, 0,
            // @  A  B  C  D  E  F  G  H  I  J  K  L  M  N  O  P  Q  R  S  T  U  V  W  X  Y  Z  [  \  ]  ^  _
            0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 1, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0,
            // `  a  b  c  d  e  f  g  h  i  j  k  l  m  n  o  p  q  r  s  t  u  v  w  x  y  z  {  |  }  ~  DEL
            0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 1, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0,
        ];

        /// <summary>The bytes the prefilter lets through, for the test that derives it.</summary>
        internal static bool PassesResolutionPrefilter(byte first) => first < 0x80 && ResolutionPrefilter[first] != 0;

        /// <summary>
        /// The string can be written as a plain scalar in a block context; with <paramref name="lenient"/>
        /// (<see cref="YamlWriterOptions.QuoteForLenientReaders"/>), not if a lenient reader misreads it either.
        /// </summary>
        internal static bool IsBlockPlain(ReadOnlySpan<byte> utf8Value, bool lenient = false) =>
            IsBlockPlainSyntax(utf8Value) && !ResolvesToNonString(utf8Value, lenient);

        /// <summary>The syntactic check. The input is valid UTF-8.</summary>
        internal static bool IsBlockPlainSyntax(ReadOnlySpan<byte> s) => ScanSyntax(s, escapedText: false) == BlockScan.Plain;

        /// <summary>What the syntactic check finds in text as a dispatch point receives it (YAML-escaped).</summary>
        internal enum BlockScan : byte
        {
            /// <summary>No escape, and the syntactic check allows plain style.</summary>
            Plain,

            /// <summary>No escape, and the syntactic check does not allow plain style.</summary>
            NotPlain,

            /// <summary>The text holds an escape: it must be unescaped before it is judged.</summary>
            Escaped,

            /// <summary>
            /// Not scanned yet; from <see cref="ScanUnescapedAscii(ReadOnlySpan{byte}, out int)"/>, a text the one scan does
            /// not judge, which takes the shared escaping scan (resumed) and the block body's scan as before.
            /// </summary>
            Unknown,

            /// <summary>
            /// M4p candidate a: a pre-encoded name (<see cref="YamlEncodedText"/>) judged when it was encoded, plain as a key
            /// in block style under the lenient rules and therefore under the standard ones too. Only a block key takes it.
            /// </summary>
            PlainKey,
        }

        /// <summary>
        /// M4p candidate a: whether escaped text is a plain block key whatever the writer's options. Lenient quoting
        /// (<c>QuoteForLenientReaders</c>) only adds to the standard quoting, so text plain under it is plain under both.
        /// The write-time conditions (a pending hint, an anchor or tag, a user encoder) stay with the writer.
        /// </summary>
        internal static bool IsPlainBlockKeyInAnyWriter(ReadOnlySpan<byte> escaped) =>
            escaped.Length != 0 && escaped.Length <= YamlConstants.MaxImplicitKeyLength - 2
            && ScanEscapedText(escaped) == BlockScan.Plain
            && !ResolvesToNonString(escaped, lenient: true)
            && !HasSpaceBeforeAsterisk(escaped);

        /// <summary>
        /// M4p, candidate b: the shared escaping scan of <see cref="YamlBlockEncoder.Instance"/>
        /// and the syntactic check in one pass, for UTF-8 text as the caller has it. It judges ASCII text with no <c>"</c> and
        /// no <c>\</c>, which that encoder leaves as it is, and returns what <see cref="ScanEscapedText"/> returns for it
        /// (<see cref="BlockScan.Plain"/> or <see cref="BlockScan.NotPlain"/>). For any other text (a <c>"</c>, a
        /// <c>\</c> or a byte above ASCII, anywhere) it returns <see cref="BlockScan.Unknown"/>, and the caller takes the
        /// path the text takes without it. <paramref name="stop"/> is then the first <c>"</c>, <c>\</c> or byte above ASCII:
        /// every byte before it is ASCII that the block encoder leaves as it is, so its escaping scan can resume there
        /// (<see cref="BlockEncoderEscapeIndex(ReadOnlySpan{byte}, int)"/>).
        /// </summary>
        internal static BlockScan ScanUnescapedAscii(ReadOnlySpan<byte> s, out int stop)
        {
            stop = -1;
            if (s.IsEmpty)
            {
                return BlockScan.NotPlain;
            }

            // The edges, as ScanSyntax checks them.
            bool notPlain;
            byte first = s[0];
            if (first < 0x80 && BlockIndicator[first] != 0
                && (first is not ((byte)'-' or (byte)'?') || s.Length == 1 || s[1] is (byte)' ' or (byte)'\t'))
            {
                notPlain = true;
            }
            else
            {
                notPlain = first == (byte)' ' || s[s.Length - 1] == (byte)' '
                    || (s.Length >= 3 && ((first == (byte)'-' && s[1] == (byte)'-' && s[2] == (byte)'-')
                        || (first == (byte)'.' && s[1] == (byte)'.' && s[2] == (byte)'.')));
            }

            int i = 0;
            while (!notPlain)
            {
                int k = IndexOfOneScanStop(s.Slice(i));
                if (k < 0)
                {
                    return BlockScan.Plain;
                }

                i += k;
                byte b = s[i];
                if (b == (byte)':')
                {
                    notPlain = i == s.Length - 1 || s[i + 1] == (byte)' ';
                }
                else if (b == (byte)'#')
                {
                    // A '#' at position 0 is an indicator, checked above.
                    notPlain = s[i - 1] == (byte)' ';
                }
                else if (b is (byte)'"' or (byte)'\\' || b >= 0x80)
                {
                    stop = i;
                    return BlockScan.Unknown;
                }
                else
                {
                    notPlain = true; // C0 controls (tab and line breaks included) and DEL
                }

                i++;
            }

            // Not plain: the rest decides only whether the text is one the scan judges.
            int rest = IndexOfOneScanRestStop(s.Slice(i));
            if (rest < 0)
            {
                return BlockScan.NotPlain;
            }

            stop = i + rest;
            return BlockScan.Unknown;
        }

        /// <summary>
        /// <see cref="ScanUnescapedAscii(ReadOnlySpan{byte}, out int)"/> for UTF-16 text: the block encoder's escaping scan
        /// of the text and the syntactic check's scan of its UTF-8 form, in one pass over the characters. ASCII text with no <c>"</c> and
        /// no <c>\</c> transcodes byte for byte, so the answer is the one <see cref="ScanEscapedText"/> gives for the
        /// transcoded bytes; any other text is <see cref="BlockScan.Unknown"/>.
        /// </summary>
        internal static BlockScan ScanUnescapedAscii(ReadOnlySpan<char> s, out int stop)
        {
            stop = -1;
            if (s.IsEmpty)
            {
                return BlockScan.NotPlain;
            }

            bool notPlain;
            char first = s[0];
            if (first < 0x80 && BlockIndicator[first] != 0
                && (first is not ('-' or '?') || s.Length == 1 || s[1] is ' ' or '\t'))
            {
                notPlain = true;
            }
            else
            {
                notPlain = first == ' ' || s[s.Length - 1] == ' '
                    || (s.Length >= 3 && ((first == '-' && s[1] == '-' && s[2] == '-') || (first == '.' && s[1] == '.' && s[2] == '.')));
            }

            int i = 0;
            while (!notPlain)
            {
                int k = IndexOfOneScanStop(s.Slice(i));
                if (k < 0)
                {
                    return BlockScan.Plain;
                }

                i += k;
                char c = s[i];
                if (c == ':')
                {
                    notPlain = i == s.Length - 1 || s[i + 1] == ' ';
                }
                else if (c == '#')
                {
                    notPlain = s[i - 1] == ' ';
                }
                else if (c is '"' or '\\' || c >= 0x80)
                {
                    stop = i;
                    return BlockScan.Unknown;
                }
                else
                {
                    notPlain = true;
                }

                i++;
            }

            int rest = IndexOfOneScanRestStop(s.Slice(i));
            if (rest < 0)
            {
                return BlockScan.NotPlain;
            }

            stop = i + rest;
            return BlockScan.Unknown;
        }

        /// <summary>
        /// What <see cref="NeedsEscaping(ReadOnlySpan{byte}, JavaScriptEncoder)"/> returns with
        /// <see cref="YamlBlockEncoder.Instance"/>, from <see cref="ScanUnescapedAscii(ReadOnlySpan{byte}, out int)"/>'s stop:
        /// the encoder's scan, resumed there (the bytes before it hold nothing it escapes).
        /// </summary>
        internal static int BlockEncoderEscapeIndex(ReadOnlySpan<byte> s, int stop)
        {
            Debug.Assert(stop >= 0 && stop < s.Length);
            if (s[stop] < 0x80)
            {
                return stop; // '"' or '\\'
            }

            int k = YamlBlockEncoder.FindFirstToEscapeUtf8(s.Slice(stop));
            return k < 0 ? -1 : stop + k;
        }

        /// <summary>The same for UTF-16 text: the first <c>"</c> or <c>\</c> from the stop on.</summary>
        internal static int BlockEncoderEscapeIndex(ReadOnlySpan<char> s, int stop)
        {
            Debug.Assert(stop >= 0 && stop < s.Length);
            if (s[stop] < 0x80)
            {
                return stop;
            }

            int k = s.Slice(stop).IndexOfAny('"', '\\');
            return k < 0 ? -1 : stop + k;
        }

        /// <summary>The syntactic check over YAML-escaped text, in the same pass as the search for an escape.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static BlockScan ScanEscapedText(ReadOnlySpan<byte> s) => ScanSyntax(s, escapedText: true);

        private static BlockScan ScanSyntax(ReadOnlySpan<byte> s, bool escapedText)
        {
            if (s.IsEmpty)
            {
                return BlockScan.NotPlain;
            }

            byte first = s[0];
            if (first < 0x80 && BlockIndicator[first] != 0)
            {
                // '-' and '?' may start a plain scalar when a character other than a space or tab follows.
                if (first is not ((byte)'-' or (byte)'?') || s.Length == 1 || s[1] is (byte)' ' or (byte)'\t')
                {
                    return NotPlain(s, 0, escapedText);
                }
            }

            if (first == (byte)' ' || s[s.Length - 1] == (byte)' ')
            {
                return NotPlain(s, 0, escapedText);
            }

            if (s.Length >= 3 && ((first == (byte)'-' && s[1] == (byte)'-' && s[2] == (byte)'-')
                || (first == (byte)'.' && s[1] == (byte)'.' && s[2] == (byte)'.')))
            {
                return NotPlain(s, 0, escapedText);
            }

            int i = 0;
            while (true)
            {
                int k = escapedText ? IndexOfEscapedTextStop(s.Slice(i)) : IndexOfBlockPlainStop(s.Slice(i));
                if (k < 0)
                {
                    return BlockScan.Plain;
                }

                i += k;
                switch (s[i])
                {
                    case (byte)'\\':
                        Debug.Assert(escapedText);
                        return BlockScan.Escaped;
                    case (byte)':':
                        if (i == s.Length - 1 || s[i + 1] == (byte)' ')
                        {
                            return NotPlain(s, i, escapedText);
                        }
                        break;
                    case (byte)'#':
                        // A '#' at position 0 is an indicator, checked above.
                        if (s[i - 1] == (byte)' ')
                        {
                            return NotPlain(s, i, escapedText);
                        }
                        break;
                    case 0xC2:
                        if (s[i + 1] <= 0x9F)
                        {
                            return NotPlain(s, i, escapedText); // C1 controls and NEL (U+0080..U+009F)
                        }
                        break;
                    case 0xE2:
                        if (s[i + 1] == 0x80 && s[i + 2] is 0xA8 or 0xA9)
                        {
                            return NotPlain(s, i, escapedText); // LS and PS
                        }
                        break;
                    case 0xEF:
                        if ((s[i + 1] == 0xBB && s[i + 2] == 0xBF) || (s[i + 1] == 0xBF && s[i + 2] is 0xBE or 0xBF))
                        {
                            return NotPlain(s, i, escapedText); // U+FEFF, U+FFFE, U+FFFF
                        }
                        break;
                    default:
                        return NotPlain(s, i, escapedText); // C0 controls (tab and line breaks included) and DEL
                }

                i++;
            }
        }

        private static bool HasUnicodeWhiteSpaceAtAnEdge(ReadOnlySpan<byte> s)
        {
            if (s.IsEmpty)
            {
                return false;
            }

            if (s[0] >= 0xC2 && IsUnicodeWhiteSpace(DecodeAt(s, 0)))
            {
                return true;
            }

            int last = s.Length - 1;
            if (s[last] < 0x80)
            {
                return false;
            }

            while (last > 0 && (s[last] & 0xC0) == 0x80)
            {
                last--;
            }

            return IsUnicodeWhiteSpace(DecodeAt(s, last));
        }

        private static int DecodeAt(ReadOnlySpan<byte> s, int i)
        {
            byte b = s[i];
            int length = b >= 0xF0 ? 4 : b >= 0xE0 ? 3 : 2;
            return i + length <= s.Length ? DecodeUtf8Scalar(s.Slice(i, length)) : -1;
        }

        // The Unicode white space (.NET's char.IsWhiteSpace) above ASCII: NEL, NBSP, the Zs space separators, LS and PS.
        private static bool IsUnicodeWhiteSpace(int c) =>
            c is 0x85 or 0xA0 or 0x1680 or (>= 0x2000 and <= 0x200A) or 0x2028 or 0x2029 or 0x202F or 0x205F or 0x3000;

        // Not plain; in JSON-escaped text, an escape after this point still means the text must be unescaped first.
        private static BlockScan NotPlain(ReadOnlySpan<byte> s, int from, bool escapedText) =>
            escapedText && s.Slice(from).IndexOf((byte)'\\') >= 0 ? BlockScan.Escaped : BlockScan.NotPlain;

        /// <summary>
        /// The resolution check: whether a YAML 1.1 or 1.2 reader can read the string, written plain, as
        /// something other than the same string, by the YAML 1.2 core schema or the YAML 1.1 types (decision 2, bounded to
        /// what the standards resolve). With <paramref name="lenient"/>
        /// (<see cref="YamlWriterOptions.QuoteForLenientReaders"/>), also whether a reader that resolves beyond the
        /// standards does (<see cref="QuotedForLenientReaders"/>). Called only for strings that pass the syntactic check.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool ResolvesToNonString(ReadOnlySpan<byte> s, bool lenient = false)
        {
            Debug.Assert(!s.IsEmpty);

            byte first = s[0];
            if (first >= 0x80 || ResolutionPrefilter[first] == 0)
            {
                return lenient && QuotedForLenientReaders(s);
            }

            // A string that starts with a letter resolves to a non-string only as one of the keywords, in the cases the
            // standards list, all of them five letters at most, so it skips the resolver ("variant D" of M4.0's
            // spike).
            if ((uint)((first | 0x20) - 'a') <= 'z' - 'a')
            {
                return (s.Length <= 5 && IsStandardLetterKeyword(s)) || (lenient && QuotedForLenientReaders(s));
            }

            return NonLetterResolvesToNonString(s) || (lenient && QuotedForLenientReaders(s));
        }

        // M4p candidate d: the bytes a string whose first byte passes the prefilter and is not a letter can hold and still
        // resolve, by the patterns of the matchers NonLetterResolvesToNonString runs: digits, signs, '.', '_' and ':', the
        // hex digits, 'o' and 'x' (the prefixes), 'i', 'n', 'I' and 'N' (.inf and .nan, with the hex letters), 'T', 't',
        // 'Z', space and tab (the timestamps). A test derives it against the matchers.
        private static ReadOnlySpan<byte> ResolvableBytes =>
        [
            0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            //   !  "  #  $  %  &  '  (  )  *  +  ,  -  .  /  0  1  2  3  4  5  6  7  8  9  :  ;  <  =  >  ?
            1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 1, 1, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0,
            // @  A  B  C  D  E  F  G  H  I  J  K  L  M  N  O  P  Q  R  S  T  U  V  W  X  Y  Z  [  \  ]  ^  _
            0, 1, 1, 1, 1, 1, 1, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1,
            // `  a  b  c  d  e  f  g  h  i  j  k  l  m  n  o  p  q  r  s  t  u  v  w  x  y  z  {  |  }  ~  DEL
            0, 1, 1, 1, 1, 1, 1, 0, 0, 1, 0, 0, 0, 0, 1, 1, 0, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0,
        ];

        /// <summary>The bytes <see cref="NonLetterResolvesToNonString"/>'s table lets through, for the test that derives it.</summary>
        internal static bool IsResolvableByte(byte b) => b < 0x80 && ResolvableBytes[b] != 0;

        /// <summary>
        /// The resolution check for a string whose first byte passes the prefilter and is not a letter (a digit, a sign,
        /// '.', '~', '&lt;' or '='): <c>ResolveYamlPlainScalar(s, yaml11: true) != String || IsYamlKeywordOrLegacyForm(s)</c>,
        /// computed with less work. '~', '&lt;' and '=' resolve only as <c>~</c>, <c>&lt;&lt;</c> and <c>=</c>. A string
        /// with a byte outside <see cref="ResolvableBytes"/> resolves as nothing. The others run the number matchers and
        /// the legacy forms, without the null and boolean comparisons, none of whose forms starts with such a byte.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static bool NonLetterResolvesToNonString(ReadOnlySpan<byte> s)
        {
            byte first = s[0];
            if (first is (byte)'~' or (byte)'<' or (byte)'=')
            {
                return s.Length == 1 ? first != (byte)'<' : s.Length == 2 && first == (byte)'<' && s[1] == (byte)'<';
            }

            ReadOnlySpan<byte> table = ResolvableBytes;
            foreach (byte b in s)
            {
                if (b >= 0x80 || table[b] == 0)
                {
                    return false;
                }
            }

            return YamlReaderHelper.IsYamlInteger(s, yaml11: true) || YamlReaderHelper.IsYamlFloat(s, yaml11: true)
                || IsYamlKeywordOrLegacyForm(s);
        }

        /// <summary>
        /// The strings a lenient reader reads as non-strings although neither YAML 1.2 nor YAML 1.1 resolves them, quoted
        /// only with <see cref="YamlWriterOptions.QuoteForLenientReaders"/> (such
        /// accommodations are opt-in): the keywords in a case no standard lists (<c>tRuE</c>, <c>yEs</c>, <c>NuLL</c>),
        /// the IEEE names (<c>nan</c>, <c>inf</c>, <c>infinity</c>, <c>NaN</c>, <c>Infinity</c>, signed or not), the
        /// number forms SharpYaml reads after it drops underscores and trims white space
        /// (<see cref="IsOtherReadersNumber"/>), and Unicode white space at an edge. M4b's cross-library property test
        /// found them.
        /// </summary>
        /// <remarks>
        /// M4p candidate e: three of the four matchers are false unless the
        /// first byte is one they can start with, so a table of first bytes picks what runs. Class 0 (no such byte): the
        /// edge test alone, which the first byte does not decide. Class 1 (a keyword letter): with no '_' and no space or
        /// tab at the end, the edge test and the letter keywords. Otherwise, the four matchers.
        /// </remarks>
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static bool QuotedForLenientReaders(ReadOnlySpan<byte> s)
        {
            Debug.Assert(!s.IsEmpty);

            byte first = s[0];
            int lenientClass = first >= 0x80 ? 2 : LenientFirstByteClass[first];
            if (lenientClass == 0)
            {
                return HasUnicodeWhiteSpaceAtAnEdge(s);
            }

            if (lenientClass == 1 && s[s.Length - 1] is not ((byte)' ' or (byte)'\t') && s.IndexOf((byte)'_') < 0)
            {
                return HasUnicodeWhiteSpaceAtAnEdge(s) || (s.Length <= 8 && IsLetterKeyword(s));
            }

            return QuotedForLenientReadersAllMatchers(s);
        }

        /// <summary>The option's four matchers, as <see cref="QuotedForLenientReaders"/> ran them before its first-byte table.</summary>
        internal static bool QuotedForLenientReadersAllMatchers(ReadOnlySpan<byte> s) =>
            HasUnicodeWhiteSpaceAtAnEdge(s) || IsKeywordInAnyCase(s) || IsOtherReadersNumber(s) || IsPsychNonString(s);

        // M4p candidate e: the class of a first byte below 0x80 for QuotedForLenientReaders (bytes at or above 0x80 are
        // class 2). 1: the letters a keyword starts with in any case (y n o t f i). 2: the first bytes of the other forms
        // of IsKeywordInAnyCase, IsOtherReadersNumber and IsPsychNonString (+ - . , _ < =, the digits, space and tab).
        // 0: every other byte. A test derives it against the matchers.
        private static ReadOnlySpan<byte> LenientFirstByteClass =>
        [
            0, 0, 0, 0, 0, 0, 0, 0, 0, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            //   !  "  #  $  %  &  '  (  )  *  +  ,  -  .  /  0  1  2  3  4  5  6  7  8  9  :  ;  <  =  >  ?
            2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 2, 2, 2, 2, 0, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 0, 0, 2, 2, 0, 0,
            // @  A  B  C  D  E  F  G  H  I  J  K  L  M  N  O  P  Q  R  S  T  U  V  W  X  Y  Z  [  \  ]  ^  _
            0, 0, 0, 0, 0, 0, 1, 0, 0, 1, 0, 0, 0, 0, 1, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 2,
            // `  a  b  c  d  e  f  g  h  i  j  k  l  m  n  o  p  q  r  s  t  u  v  w  x  y  z  {  |  }  ~  DEL
            0, 0, 0, 0, 0, 0, 1, 0, 0, 1, 0, 0, 0, 0, 1, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0,
        ];

        /// <summary>The class <see cref="QuotedForLenientReaders"/>'s table gives a first byte, for the test that derives it.</summary>
        internal static int LenientClassOf(byte b) => b >= 0x80 ? 2 : LenientFirstByteClass[b];

        /// <summary>
        /// The forms Ruby's Psych (the scalar scanner of Ruby 2.6) reads as non-strings although neither YAML 1.2 nor
        /// YAML 1.1 resolves them:
        /// <list type="bullet">
        /// <item>sexagesimal integers with a leading zero (<c>09:30</c>, <c>0:30</c>, <c>0_:0</c>): Psych takes
        /// <c>[-+]?[0-9][0-9_]*(:[0-5]?[0-9])+</c> where YAML 1.1 has <c>[1-9]</c> first;</item>
        /// <item>digits with commas (<c>1,000</c>, <c>12,5</c>, <c>0,.</c>): Psych drops commas and underscores before
        /// it reads an integer, and its float pattern takes <c>[0-9][0-9_,]*</c> before the point;</item>
        /// <item>a date with a one-digit month or day and no time (<c>2024-3-1</c>);</item>
        /// <item>a float with no digit before the exponent (<c>.e+0</c>, <c>-.E-1</c>), on which Psych fails;</item>
        /// <item>a timestamp by Psych's <c>TIME</c> pattern (<see cref="IsPsychTimestamp"/>), which also takes a leading
        /// <c>-</c> (<c>-2001-12-14 21:59:43</c>) and zones YAML 1.1 does not (<c>+0500</c>, <c>+530</c>, <c>+05:</c>):
        /// a reader that resolves beyond the standards.</item>
        /// </list>
        /// </summary>
        internal static bool IsPsychNonString(ReadOnlySpan<byte> s)
        {
            int i = s[0] is (byte)'+' or (byte)'-' ? 1 : 0;
            if (i >= s.Length)
            {
                return false;
            }

            if (s[i] == (byte)'.')
            {
                // [-+]?\.[eE][-+][0-9]+
                return i + 3 < s.Length && (s[i + 1] | 0x20) == (byte)'e' && s[i + 2] is (byte)'+' or (byte)'-'
                    && NonEmptyDigits(s.Slice(i + 3));
            }

            // Every other form starts, after the sign, with a digit, or (an integer with commas) with ',' or '_'.
            bool digit = IsAsciiDigit(s[i]);
            if (!digit && s[i] is not ((byte)',' or (byte)'_'))
            {
                return false;
            }

            if (s.IndexOf((byte)',') >= 0)
            {
                return IsPsychCommaNumber(s, i);
            }

            return digit && ((i == 0 && IsPsychDate(s)) || IsPsychSexagesimal(s, i) || (s[0] != (byte)'+' && IsPsychTimestamp(s)));
        }

        /// <summary>
        /// Psych 3.1's <c>ScalarScanner::TIME</c>, exactly:
        /// <c>^-?\d{4}-\d{1,2}-\d{1,2}(?:[Tt]|\s+)\d{1,2}:\d\d:\d\d(?:\.\d*)?(?:\s*(?:Z|[-+]\d{1,2}:?(?:\d\d)?))?$</c>,
        /// where <c>\s</c> is ASCII white space other than a line feed (a string with a line feed is a string to Psych
        /// before this pattern is tried). Beyond the YAML 1.1 timestamp it takes a leading <c>-</c>, white space before a
        /// numeric zone, and the zones <c>[-+]\d{3,4}</c> and <c>[-+]\d{1,2}:</c>. A string Psych's time parser then
        /// rejects (a month of 13) comes back as a string; quoting it is harmless.
        /// </summary>
        internal static bool IsPsychTimestamp(ReadOnlySpan<byte> s)
        {
            int i = s.Length > 0 && s[0] == (byte)'-' ? 1 : 0;
            if (s.Length - i < 16 || s[i + 4] != (byte)'-')
            {
                return false;
            }

            if (!Digits(s, ref i, 4, 4) || !Expect(s, ref i, (byte)'-') || !Digits(s, ref i, 1, 2) || !Expect(s, ref i, (byte)'-')
                || !Digits(s, ref i, 1, 2) || i == s.Length)
            {
                return false;
            }

            if (s[i] is (byte)'T' or (byte)'t')
            {
                i++;
            }
            else if (!PsychWhiteSpace(s, ref i))
            {
                return false;
            }

            if (!Digits(s, ref i, 1, 2) || !Expect(s, ref i, (byte)':') || !Digits(s, ref i, 2, 2)
                || !Expect(s, ref i, (byte)':') || !Digits(s, ref i, 2, 2))
            {
                return false;
            }

            if (i < s.Length && s[i] == (byte)'.')
            {
                i++;
                while (i < s.Length && IsAsciiDigit(s[i]))
                {
                    i++;
                }
            }

            if (i == s.Length)
            {
                return true;
            }

            // (?:\s*(?:Z|[-+]\d{1,2}:?(?:\d\d)?))?$ : white space alone does not end it.
            PsychWhiteSpace(s, ref i);
            if (i == s.Length)
            {
                return false;
            }

            if (s[i] == (byte)'Z')
            {
                return i + 1 == s.Length;
            }

            if (s[i] is not ((byte)'+' or (byte)'-'))
            {
                return false;
            }

            // [-+]\d{1,2}:?(?:\d\d)? with backtracking: 1 to 4 digits without a colon; 1 or 2 digits, a colon, and 0 or 2
            // digits with one.
            i++;
            int start = i;
            while (i < s.Length && IsAsciiDigit(s[i]))
            {
                i++;
            }

            int digits = i - start;
            if (i == s.Length)
            {
                return digits is >= 1 and <= 4;
            }

            if (s[i] != (byte)':' || digits is < 1 or > 2)
            {
                return false;
            }

            i++;
            return i == s.Length || (s.Length - i == 2 && IsAsciiDigit(s[i]) && IsAsciiDigit(s[i + 1]));
        }

        // \s+ of Ruby's regular expressions, without the line feed: space, tab, CR, form feed, vertical tab.
        private static bool PsychWhiteSpace(ReadOnlySpan<byte> s, ref int i)
        {
            int start = i;
            while (i < s.Length && s[i] is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\f' or (byte)'\v')
            {
                i++;
            }

            return i > start;
        }

        // [-+]?[0-9][0-9_]*(:[0-5]?[0-9])+, from the digit at i.
        private static bool IsPsychSexagesimal(ReadOnlySpan<byte> s, int i)
        {
            i++;
            while (i < s.Length && (IsAsciiDigit(s[i]) || s[i] == (byte)'_'))
            {
                i++;
            }

            int groups = 0;
            while (i < s.Length && s[i] == (byte)':')
            {
                i++;
                int d = i;
                while (i < s.Length && IsAsciiDigit(s[i]) && i - d < 2)
                {
                    i++;
                }

                int n = i - d;
                if (n == 0 || (n == 2 && s[d] > (byte)'5'))
                {
                    return false;
                }

                groups++;
            }

            return groups > 0 && i == s.Length;
        }

        // \d{4}-(1[012]|0\d|\d)-([12]\d|3[01]|0\d|\d)
        private static bool IsPsychDate(ReadOnlySpan<byte> s)
        {
            if (s.Length < 8 || s.Length > 10 || s[4] != (byte)'-' || !NonEmptyDigits(s.Slice(0, 4)))
            {
                return false;
            }

            int dash = s.Slice(5).IndexOf((byte)'-');
            if (dash < 0)
            {
                return false;
            }

            ReadOnlySpan<byte> month = s.Slice(5, dash);
            ReadOnlySpan<byte> day = s.Slice(6 + dash);
            return month.Length is 1 or 2 && NonEmptyDigits(month) && (month.Length == 1 || month[0] == (byte)'0' || (month[0] == (byte)'1' && month[1] <= (byte)'2'))
                && day.Length is 1 or 2 && NonEmptyDigits(day) && (day.Length == 1 || day[0] <= (byte)'2' || (day[0] == (byte)'3' && day[1] <= (byte)'1'));
        }

        // A string with a comma, from i, after the sign: Psych's float, [-+]?[0-9][0-9_,]*\.[0-9]*([eE][-+][0-9]+)?, or its
        // integer after it drops commas and underscores, [-+]?(0b[0-1]+|0[0-7]+|0|[1-9][0-9]*|0x[0-9a-fA-F]+) (-,1 is
        // -1). Psych reads a few of the integer forms as strings first (+_,1); quoting them is harmless.
        private static bool IsPsychCommaNumber(ReadOnlySpan<byte> s, int i)
        {
            int start = i;
            while (i < s.Length && (IsAsciiDigit(s[i]) || s[i] is (byte)'_' or (byte)','))
            {
                i++;
            }

            if (IsAsciiDigit(s[start]) && i < s.Length && s[i] == (byte)'.')
            {
                i++;
                while (i < s.Length && IsAsciiDigit(s[i]))
                {
                    i++;
                }

                if (i < s.Length && (s[i] | 0x20) == (byte)'e')
                {
                    return i + 2 < s.Length && s[i + 1] is (byte)'+' or (byte)'-' && NonEmptyDigits(s.Slice(i + 2));
                }

                return i == s.Length;
            }

            if (s.Length - start > 64)
            {
                return false;
            }

            Span<byte> buffer = stackalloc byte[64];
            int n = 0;
            foreach (byte b in s.Slice(start))
            {
                if (b is not ((byte)',' or (byte)'_'))
                {
                    buffer[n++] = b;
                }
            }

            ReadOnlySpan<byte> t = buffer.Slice(0, n);
            if (t.IsEmpty)
            {
                return false;
            }

            if (t[0] != (byte)'0')
            {
                return NonEmptyDigits(t);
            }

            if (t.Length == 1)
            {
                return true;
            }

            ReadOnlySpan<byte> rest = t.Slice(2);
            switch (t[1])
            {
                case (byte)'b':
                    foreach (byte b in rest)
                    {
                        if (b is not ((byte)'0' or (byte)'1'))
                        {
                            return false;
                        }
                    }

                    return !rest.IsEmpty;
                case (byte)'x':
                    foreach (byte b in rest)
                    {
                        if (!IsAsciiDigit(b) && (uint)((b | 0x20) - 'a') > 'f' - 'a')
                        {
                            return false;
                        }
                    }

                    return !rest.IsEmpty;
                default:
                    foreach (byte b in t.Slice(1))
                    {
                        if ((uint)(b - '0') > 7)
                        {
                            return false;
                        }
                    }

                    return true;
            }
        }

        private static bool NonEmptyDigits(ReadOnlySpan<byte> s)
        {
            if (s.IsEmpty)
            {
                return false;
            }

            foreach (byte b in s)
            {
                if (!IsAsciiDigit(b))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// The number forms that other readers resolve beyond Styml's resolver (M4b, found by the cross-library property
        /// test; SharpYaml's reader is the lenient one). SharpYaml drops every underscore and trims white space before it
        /// parses a number, so the text is judged that way: <c>_1</c>, <c>1_</c>, <c>_ 6</c>, <c>nan_</c>,
        /// <c>-0o1_7</c>. On that text, a sign before <c>nan</c>, <c>inf</c> or <c>infinity</c>, <c>.inf</c> and
        /// <c>.nan</c> in any case, and <c>0o</c>, <c>0x</c> and <c>0b</c> in either case and with a sign are numbers too.
        /// </summary>
        internal static bool IsOtherReadersNumber(ReadOnlySpan<byte> s)
        {
            if (s.Length > 64)
            {
                return false;
            }

            Span<byte> buffer = stackalloc byte[64];
            int n = 0;
            foreach (byte b in s)
            {
                if (b != (byte)'_')
                {
                    buffer[n++] = b;
                }
            }

            ReadOnlySpan<byte> u = TrimWhiteSpace(buffer.Slice(0, n));
            if (u.IsEmpty)
            {
                return false;
            }

            bool changed = u.Length != s.Length;
            bool signed = u[0] is (byte)'+' or (byte)'-';
            ReadOnlySpan<byte> t = signed ? u.Slice(1) : u;
            if (t.IsEmpty)
            {
                return false;
            }

            if (t[0] == (byte)'.' && t.Length == 4
                && (EqualsAsciiIgnoreCase(t.Slice(1), "inf"u8) || EqualsAsciiIgnoreCase(t.Slice(1), "nan"u8)))
            {
                return true;
            }

            if ((uint)((t[0] | 0x20) - 'a') <= 'z' - 'a')
            {
                return t.Length <= 8 && IsLetterKeyword(t);
            }

            if (t.Length > 2 && t[0] == (byte)'0' && (t[1] | 0x20) is (byte)'o' or (byte)'x' or (byte)'b')
            {
                byte prefix = (byte)(t[1] | 0x20);
                foreach (byte b in t.Slice(2))
                {
                    bool digit = prefix switch
                    {
                        (byte)'o' => (uint)(b - '0') <= 7,
                        (byte)'b' => b is (byte)'0' or (byte)'1',
                        _ => IsAsciiDigit(b) || (uint)((b | 0x20) - 'a') <= 'f' - 'a',
                    };
                    if (!digit)
                    {
                        return false;
                    }
                }

                return true;
            }

            // The resolver has judged the unchanged, unsigned text already.
            return (changed || signed) && YamlReaderHelper.ResolveYamlPlainScalar(u, yaml11: true) == YamlTokenType.Number;
        }

        private static ReadOnlySpan<byte> TrimWhiteSpace(ReadOnlySpan<byte> s)
        {
            while (!s.IsEmpty)
            {
                if (s[0] is (byte)' ' or (byte)'\t')
                {
                    s = s.Slice(1);
                }
                else if (s[0] >= 0xC2 && DecodeAt(s, 0) is int c && IsUnicodeWhiteSpace(c))
                {
                    s = s.Slice(c < 0x800 ? 2 : 3);
                }
                else
                {
                    break;
                }
            }

            while (!s.IsEmpty)
            {
                int last = s.Length - 1;
                if (s[last] is (byte)' ' or (byte)'\t')
                {
                    s = s.Slice(0, last);
                    continue;
                }

                if (s[last] < 0x80)
                {
                    break;
                }

                int lead = last;
                while (lead > 0 && (s[lead] & 0xC0) == 0x80)
                {
                    lead--;
                }

                if (!IsUnicodeWhiteSpace(DecodeAt(s, lead)))
                {
                    break;
                }

                s = s.Slice(0, lead);
            }

            return s;
        }

        /// <summary>
        /// The key rule from M4b, with <see cref="YamlWriterOptions.QuoteForLenientReaders"/> only: a key in which a space is
        /// followed by <c>*</c> (<c>a *b</c>) is quoted, since YamlDotNet reads <c>a *b: v</c> as one scalar (valid
        /// YAML, which reads the key <c>a *b</c>).
        /// </summary>
        internal static bool HasSpaceBeforeAsterisk(ReadOnlySpan<byte> s) => s.IndexOf(" *"u8) >= 0;

        /// <summary>The scalar rules for a key: <see cref="IsBlockPlain"/>, and with <paramref name="lenient"/> <see cref="HasSpaceBeforeAsterisk"/>.</summary>
        internal static bool IsBlockPlainKey(ReadOnlySpan<byte> utf8Key, bool lenient = false) =>
            IsBlockPlain(utf8Key, lenient) && !(lenient && HasSpaceBeforeAsterisk(utf8Key));

        // The keywords of the YAML 1.2 core schema and the YAML 1.1 types that start with a letter (y n no on yes off true
        // null false), in the cases they list: all lower case, all upper case, or capitalized (y and n in either case).
        private static bool IsStandardLetterKeyword(ReadOnlySpan<byte> s)
        {
            ulong v = 0;
            int upper = 0;
            for (int i = 0; i < s.Length; i++)
            {
                byte b = s[i];
                v |= (ulong)(byte)(b | 0x20) << (8 * i);
                if ((b & 0x20) == 0)
                {
                    upper |= 1 << i;
                }
            }

            bool keyword = s.Length switch
            {
                1 => v is 0x79 or 0x6E, // y, n
                2 => v is 0x6F6E or 0x6E6F, // no, on
                3 => v is 0x736579 or 0x66666F, // yes, off
                4 => v is 0x65757274 or 0x6C6C756E, // true, null
                5 => v == 0x65736C6166, // false
                _ => false,
            };

            // The letters are known here, so the case bit tells upper from lower.
            return keyword && (upper == 0 || upper == (1 << s.Length) - 1 || upper == 1);
        }

        // The keywords of decision 2's full set in any case, and the signed IEEE names, for QuotedForLenientReaders.
        private static bool IsKeywordInAnyCase(ReadOnlySpan<byte> s)
        {
            if (s.Length > 9)
            {
                return false;
            }

            if ((uint)((s[0] | 0x20) - 'a') <= 'z' - 'a')
            {
                return s.Length <= 8 && IsLetterKeyword(s);
            }

            foreach (byte[] keyword in s_yamlKeywords)
            {
                if (keyword.Length == s.Length && EqualsAsciiIgnoreCase(s, keyword))
                {
                    return true;
                }
            }

            return false;
        }

        // The keywords of 5.3 that start with a letter, in any case: y n no on yes off nan inf true null false infinity.
        // Each byte is lower-cased with | 0x20, which maps only the letters onto letters.
        private static bool IsLetterKeyword(ReadOnlySpan<byte> s)
        {
            ulong v = 0;
            for (int i = 0; i < s.Length; i++)
            {
                v |= (ulong)(byte)(s[i] | 0x20) << (8 * i);
            }

            // The bytes packed little-endian: "no" is 'n' | 'o' << 8.
            return s.Length switch
            {
                1 => v is 0x79 or 0x6E, // y, n
                2 => v is 0x6F6E or 0x6E6F,
                3 => v is 0x736579 or 0x66666F or 0x6E616E or 0x666E69,
                4 => v is 0x65757274 or 0x6C6C756E,
                5 => v == 0x65736C6166,
                8 => v == 0x7974696E69666E69,
                _ => false,
            };
        }

        private static readonly byte[][] s_yamlKeywords =
        [
            "true"u8.ToArray(), "false"u8.ToArray(), "null"u8.ToArray(),
            "y"u8.ToArray(), "n"u8.ToArray(), "yes"u8.ToArray(), "no"u8.ToArray(), "on"u8.ToArray(), "off"u8.ToArray(),
            "nan"u8.ToArray(), "inf"u8.ToArray(), "infinity"u8.ToArray(),
            "+inf"u8.ToArray(), "-inf"u8.ToArray(), "+infinity"u8.ToArray(), "-infinity"u8.ToArray(),
            "<<"u8.ToArray(), "="u8.ToArray(),
        ];

        /// <summary>
        /// The resolution check for what the resolver does not cover and a YAML 1.1 type resolves: <c>&lt;&lt;</c> (merge),
        /// <c>=</c> (value), and the YAML 1.1 timestamps and sexagesimal floats. The keywords in other
        /// cases and the IEEE names are <see cref="QuotedForLenientReaders"/>'s. Also the YAML 1.1 binary and hexadecimal
        /// integers with no digit after the prefix (<c>0x_</c>, <c>-0b__</c>), which the patterns as printed match
        /// (<c>[-+]?0x[0-9a-fA-F_]+</c>, <c>[-+]?0b[0-1_]+</c>) and the resolver does not: PyYAML fails on them.
        /// </summary>
        internal static bool IsYamlKeywordOrLegacyForm(ReadOnlySpan<byte> s)
        {
            if (s.SequenceEqual("<<"u8) || s.SequenceEqual("="u8))
            {
                return true;
            }

            int start = s[0] is (byte)'+' or (byte)'-' ? 1 : 0;
            if (start < s.Length && IsAsciiDigit(s[start]))
            {
                if (s.IndexOfAny((byte)'-', (byte)':') >= 0 && (IsYaml11Timestamp(s) || IsYaml11SexagesimalFloat(s)))
                {
                    return true;
                }

                return IsYaml11PrefixWithoutDigits(s.Slice(start));
            }

            return false;
        }

        // 0x or 0b followed by underscores only.
        private static bool IsYaml11PrefixWithoutDigits(ReadOnlySpan<byte> t)
        {
            if (t.Length < 3 || t[0] != (byte)'0' || t[1] is not ((byte)'x' or (byte)'b'))
            {
                return false;
            }

            for (int i = 2; i < t.Length; i++)
            {
                if (t[i] != (byte)'_')
                {
                    return false;
                }
            }

            return true;
        }

        private static bool EqualsAsciiIgnoreCase(ReadOnlySpan<byte> s, ReadOnlySpan<byte> lowerCase)
        {
            for (int i = 0; i < s.Length; i++)
            {
                byte c = s[i];
                if ((uint)(c - 'A') <= 'Z' - 'A')
                {
                    c |= 0x20;
                }

                if (c != lowerCase[i])
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsAsciiDigit(byte b) => (uint)(b - '0') <= 9;

        /// <summary>
        /// The YAML 1.1 timestamp (yaml.org/type/timestamp): <c>YYYY-MM-DD</c>, or <c>YYYY-M-D</c> followed by
        /// <c>T</c>, <c>t</c> or spaces and <c>H:MM:SS(.f)?</c>, then an optional zone.
        /// </summary>
        internal static bool IsYaml11Timestamp(ReadOnlySpan<byte> s)
        {
            int i = 0;
            if (!Digits(s, ref i, 4, 4) || !Expect(s, ref i, (byte)'-'))
            {
                return false;
            }

            int month = i;
            if (!Digits(s, ref i, 1, 2) || !Expect(s, ref i, (byte)'-'))
            {
                return false;
            }

            int monthLength = i - 1 - month;
            int day = i;
            if (!Digits(s, ref i, 1, 2))
            {
                return false;
            }

            if (i == s.Length)
            {
                return monthLength == 2 && i - day == 2;
            }

            if (s[i] is (byte)'T' or (byte)'t')
            {
                i++;
            }
            else
            {
                int spaces = i;
                while (i < s.Length && s[i] is (byte)' ' or (byte)'\t')
                {
                    i++;
                }

                if (i == spaces)
                {
                    return false;
                }
            }

            if (!Digits(s, ref i, 1, 2) || !Expect(s, ref i, (byte)':') || !Digits(s, ref i, 2, 2)
                || !Expect(s, ref i, (byte)':') || !Digits(s, ref i, 2, 2))
            {
                return false;
            }

            if (i < s.Length && s[i] == (byte)'.')
            {
                i++;
                while (i < s.Length && IsAsciiDigit(s[i]))
                {
                    i++;
                }
            }

            while (i < s.Length && s[i] is (byte)' ' or (byte)'\t')
            {
                i++;
            }

            if (i == s.Length)
            {
                return true;
            }

            if (s[i] == (byte)'Z')
            {
                return i + 1 == s.Length;
            }

            if (s[i] is (byte)'+' or (byte)'-')
            {
                i++;
                if (!Digits(s, ref i, 1, 2))
                {
                    return false;
                }

                if (i < s.Length && s[i] == (byte)':')
                {
                    i++;
                    if (!Digits(s, ref i, 2, 2))
                    {
                        return false;
                    }
                }

                return i == s.Length;
            }

            return false;
        }

        /// <summary>The YAML 1.1 sexagesimal float: <c>[-+]?[0-9][0-9_]*(:[0-5]?[0-9])+\.[0-9_]*</c>.</summary>
        internal static bool IsYaml11SexagesimalFloat(ReadOnlySpan<byte> s)
        {
            int i = s[0] is (byte)'+' or (byte)'-' ? 1 : 0;
            if (i >= s.Length || !IsAsciiDigit(s[i]))
            {
                return false;
            }

            i++;
            while (i < s.Length && (IsAsciiDigit(s[i]) || s[i] == (byte)'_'))
            {
                i++;
            }

            int groups = 0;
            while (i < s.Length && s[i] == (byte)':')
            {
                i++;
                int d = i;
                while (i < s.Length && IsAsciiDigit(s[i]) && i - d < 2)
                {
                    i++;
                }

                int n = i - d;
                if (n == 0 || (n == 2 && s[d] > (byte)'5'))
                {
                    return false;
                }

                groups++;
            }

            if (groups == 0 || i >= s.Length || s[i] != (byte)'.')
            {
                return false;
            }

            i++;
            while (i < s.Length && (IsAsciiDigit(s[i]) || s[i] == (byte)'_'))
            {
                i++;
            }

            return i == s.Length;
        }

        private static bool Digits(ReadOnlySpan<byte> s, ref int i, int min, int max)
        {
            int start = i;
            while (i < s.Length && i - start < max && IsAsciiDigit(s[i]))
            {
                i++;
            }

            return i - start >= min;
        }

        private static bool Expect(ReadOnlySpan<byte> s, ref int i, byte c)
        {
            if (i < s.Length && s[i] == c)
            {
                i++;
                return true;
            }

            return false;
        }

        /// <summary>
        /// The length of a written key in characters, counted only when its UTF-8 length is above the implicit-key
        /// limit: YAML 1.2 limits implicit keys to 1024 Unicode characters.
        /// </summary>
        internal static bool NeedsExplicitKey(int writtenUtf8Length, ReadOnlySpan<byte> utf8Content)
        {
            if (writtenUtf8Length <= YamlConstants.MaxImplicitKeyLength)
            {
                return false;
            }

            // Every byte that is not a continuation byte starts a character. The quotes and escapes, when the key is
            // quoted, are ASCII and are added by the caller (writtenUtf8Length - utf8Content.Length).
            int characters = writtenUtf8Length - utf8Content.Length;
            foreach (byte b in utf8Content)
            {
                if ((b & 0xC0) != 0x80)
                {
                    characters++;
                }
            }

            return characters > YamlConstants.MaxImplicitKeyLength;
        }

#if !NET
        private static readonly UTF8Encoding s_strictUtf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
#endif

        /// <summary>UTF-16 to UTF-8 with each unpaired surrogate as U+FFFD, as the JSON-compatible style writes it.</summary>
        internal static int ToUtf8Replacing(ReadOnlySpan<char> source, Span<byte> destination)
        {
#if NET
            OperationStatus status = System.Text.Unicode.Utf8.FromUtf16(source, destination, out _, out int written, replaceInvalidSequences: true);
            Debug.Assert(status == OperationStatus.Done);
            return written;
#else
            byte[] bytes = Encoding.UTF8.GetBytes(source.ToArray());
            bytes.CopyTo(destination);
            return bytes.Length;
#endif
        }

        /// <summary>Whether the bytes are valid UTF-8: block style writes non-ASCII characters as they are.</summary>
        internal static bool IsValidUtf8(ReadOnlySpan<byte> utf8Value)
        {
#if NET
            return System.Text.Unicode.Utf8.IsValid(utf8Value);
#else
            for (int i = 0; i < utf8Value.Length; i++)
            {
                if (utf8Value[i] >= 0x80)
                {
                    try
                    {
                        _ = s_strictUtf8.GetCharCount(utf8Value.Slice(i));
                        return true;
                    }
                    catch (DecoderFallbackException)
                    {
                        return false;
                    }
                }
            }

            return true;
#endif
        }

        /// <summary>The largest output of <see cref="EscapeBlockDoubleQuoted"/> for an input of this many bytes.</summary>
        internal static int GetMaxBlockEscapedLength(int utf8Length) => utf8Length * YamlConstants.MaxExpansionFactorWhileEscaping;

        /// <summary>
        /// Writes the content of a double-quoted scalar (without the quotes): the YAML set of 5.1 is escaped, and
        /// with an explicit encoder, so is every character the encoder escapes. Other characters, non-ASCII ones
        /// included, are copied. The input is valid UTF-8.
        /// </summary>
        internal static int EscapeBlockDoubleQuoted(ReadOnlySpan<byte> value, Span<byte> destination, JavaScriptEncoder? encoder)
        {
            if (encoder is not null)
            {
                return EscapeBlockDoubleQuotedWithEncoder(value, destination, encoder);
            }

            int i = 0;
            int written = 0;
            while (true)
            {
                int k = IndexOfBlockEscapeStop(value.Slice(i));
                if (k < 0)
                {
                    value.Slice(i).CopyTo(destination.Slice(written));
                    return written + value.Length - i;
                }

                value.Slice(i, k).CopyTo(destination.Slice(written));
                written += k;
                i += k;

                byte b = value[i];
                if (b < 0x80)
                {
                    written += WriteBlockAsciiEscape(b, destination.Slice(written));
                    i++;
                    continue;
                }

                int scalar = -1;
                int length = 1;
                if (b == 0xC2)
                {
                    length = 2;
                    if (value[i + 1] <= 0x9F)
                    {
                        scalar = value[i + 1];
                    }
                }
                else if (b == 0xE2)
                {
                    length = 3;
                    if (value[i + 1] == 0x80 && value[i + 2] is 0xA8 or 0xA9)
                    {
                        scalar = 0x2000 | (value[i + 2] & 0x3F);
                    }
                }
                else
                {
                    Debug.Assert(b == 0xEF);
                    length = 3;
                    if (value[i + 1] == 0xBB && value[i + 2] == 0xBF)
                    {
                        scalar = 0xFEFF;
                    }
                    else if (value[i + 1] == 0xBF && value[i + 2] is 0xBE or 0xBF)
                    {
                        scalar = 0xFFC0 | (value[i + 2] & 0x3F);
                    }
                }

                if (scalar >= 0)
                {
                    written += WriteUnicodeEscape(scalar, destination.Slice(written));
                }
                else
                {
                    value.Slice(i, length).CopyTo(destination.Slice(written));
                    written += length;
                }

                i += length;
            }
        }

        private static int EscapeBlockDoubleQuotedWithEncoder(ReadOnlySpan<byte> value, Span<byte> destination, JavaScriptEncoder encoder)
        {
            int written = 0;
            int i = 0;
            while (i < value.Length)
            {
                byte b = value[i];
                if (b < 0x80)
                {
                    if (b < 0x20 || b is 0x7F or (byte)'"' or (byte)'\\')
                    {
                        written += WriteBlockAsciiEscape(b, destination.Slice(written));
                    }
                    else if (encoder.WillEncode(b))
                    {
                        written += WriteUnicodeEscape(b, destination.Slice(written));
                    }
                    else
                    {
                        destination[written++] = b;
                    }

                    i++;
                    continue;
                }

                int length = b >= 0xF0 ? 4 : b >= 0xE0 ? 3 : 2;
                int scalar = DecodeUtf8Scalar(value.Slice(i, length));
                bool yamlEscape = scalar <= 0x9F || scalar is 0x2028 or 0x2029 or 0xFEFF or 0xFFFE or 0xFFFF;
                if (yamlEscape || encoder.WillEncode(scalar))
                {
                    if (scalar > 0xFFFF)
                    {
                        // An astral character is written as a surrogate pair, as the JSON-compatible style writes it.
                        int v = scalar - 0x10000;
                        written += WriteUnicodeEscape(0xD800 + (v >> 10), destination.Slice(written));
                        written += WriteUnicodeEscape(0xDC00 + (v & 0x3FF), destination.Slice(written));
                    }
                    else
                    {
                        written += WriteUnicodeEscape(scalar, destination.Slice(written));
                    }
                }
                else
                {
                    value.Slice(i, length).CopyTo(destination.Slice(written));
                    written += length;
                }

                i += length;
            }

            return written;
        }

        private static int DecodeUtf8Scalar(ReadOnlySpan<byte> s) => s.Length switch
        {
            2 => ((s[0] & 0x1F) << 6) | (s[1] & 0x3F),
            3 => ((s[0] & 0x0F) << 12) | ((s[1] & 0x3F) << 6) | (s[2] & 0x3F),
            _ => ((s[0] & 0x07) << 18) | ((s[1] & 0x3F) << 12) | ((s[2] & 0x3F) << 6) | (s[3] & 0x3F),
        };

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int WriteBlockAsciiEscape(byte value, Span<byte> destination)
        {
            destination[0] = (byte)'\\';
            switch (value)
            {
                case (byte)'"': destination[1] = (byte)'"'; return 2;
                case (byte)'\\': destination[1] = (byte)'\\'; return 2;
                case (byte)'\n': destination[1] = (byte)'n'; return 2;
                case (byte)'\r': destination[1] = (byte)'r'; return 2;
                case (byte)'\t': destination[1] = (byte)'t'; return 2;
                case (byte)'\b': destination[1] = (byte)'b'; return 2;
                case (byte)'\f': destination[1] = (byte)'f'; return 2;
                default: return WriteUnicodeEscape(value, destination);
            }
        }

        private static int WriteUnicodeEscape(int value, Span<byte> destination)
        {
            Debug.Assert(value <= 0xFFFF);
            destination[0] = (byte)'\\';
            destination[1] = (byte)'u';
            destination[2] = HexDigit(value >> 12);
            destination[3] = HexDigit(value >> 8);
            destination[4] = HexDigit(value >> 4);
            destination[5] = HexDigit(value);
            return 6;
        }

        private static byte HexDigit(int value)
        {
            value &= 0xF;
            return (byte)(value < 10 ? '0' + value : 'A' + value - 10);
        }

        /// <summary>
        /// The string (with at least one line break) can be written as a literal block scalar. The input
        /// is valid UTF-8.
        /// </summary>
        internal static bool CanWriteLiteral(ReadOnlySpan<byte> s, bool lenient = false)
        {
            bool hasLineBreak = false;
            bool hasContent = false;
            bool lineOnlySpaces = true;
            int lineLength = 0;
            for (int i = 0; i < s.Length; i++)
            {
                byte b = s[i];
                if (b == (byte)'\n')
                {
                    if (lineLength > 0 && lineOnlySpaces)
                    {
                        return false; // a line made only of spaces
                    }

                    hasLineBreak = true;
                    lineOnlySpaces = true;
                    lineLength = 0;
                    continue;
                }

                if ((b < 0x20 && b != (byte)'\t') || b == 0x7F)
                {
                    return false; // \r and the other C0 controls, DEL
                }

                if (b == (byte)'\t' && lineLength == 0 && !hasContent)
                {
                    // M5f: the first content line starts with a tab. That is valid YAML, but SharpYaml's
                    // reader refuses it ("a tab character where an indentation space is expected"), at any position.
                    return false;
                }

                if (b == (byte)'\t' && lineLength == 0 && lenient)
                {
                    // A line that starts with a tab after the indentation, valid YAML that SharpYaml and VYaml reject
                    // (M4b; QuoteForLenientReaders only).
                    return false;
                }

                if (b == 0xC2 && s[i + 1] <= 0x9F)
                {
                    return false; // C1 controls and NEL
                }

                if (b == 0xE2 && s[i + 1] == 0x80 && s[i + 2] is 0xA8 or 0xA9)
                {
                    return false; // LS and PS
                }

                if (b == 0xEF && ((s[i + 1] == 0xBB && s[i + 2] == 0xBF) || (s[i + 1] == 0xBF && s[i + 2] is 0xBE or 0xBF)))
                {
                    return false; // U+FEFF, U+FFFE, U+FFFF
                }

                hasContent = true;
                lineLength++;
                if (b != (byte)' ')
                {
                    lineOnlySpaces = false;
                }
            }

            if (lineLength > 0 && lineOnlySpaces)
            {
                return false;
            }

            return hasLineBreak && hasContent;
        }
    }
}
