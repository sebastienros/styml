// Copyright (c) Sébastien Ros. Licensed under the MIT license.

using System.Collections.Generic;

namespace System.Text.Yaml.Serialization
{
    /// <summary>
    /// M5d: the rules for a configured tag
    /// (<c>YamlDerivedTypeAttribute.Tag</c>, <c>YamlSerializerOptions.TagMappings</c>, <c>[YamlTagMapping]</c>), shared
    /// by the library and the source generator. A configured tag is a local tag (<c>!dog</c>: <c>!</c> and tag
    /// characters, no second <c>!</c>) or a global tag URI (<c>tag:example.com,2000:dog</c>). Core tags, <c>!</c> and
    /// <c>?</c> are reserved.
    /// </summary>
    internal static class YamlConfiguredTag
    {
        internal const string CorePrefix = "tag:yaml.org,2002:";

        /// <summary>A core tag (<c>!!str</c>, <c>tag:yaml.org,2002:str</c>), the non-specific <c>!</c>, or <c>?</c>.</summary>
        internal static bool IsReserved(string tag) =>
            tag == "!" || tag == "?" || tag.StartsWith("!!", StringComparison.Ordinal) || tag.StartsWith(CorePrefix, StringComparison.Ordinal);

        /// <summary>Valid tag syntax in one of the two configured forms (reserved tags aside).</summary>
        internal static bool IsValid(string tag)
        {
            if (tag.Length == 0)
            {
                return false;
            }

            if (tag[0] == '!')
            {
                // A local tag is written as is: the primary handle and a shorthand suffix (ns-tag-char).
                return IsUriText(tag, 1, tagChars: true);
            }

            // A global tag is written verbatim (!<...>): a URI with a scheme.
            int colon = tag.IndexOf(':');
            return colon > 0 && IsUriText(tag, 0, tagChars: false);
        }

        /// <summary>The text a valid configured tag is written as.</summary>
        internal static string GetWrittenForm(string tag) => tag[0] == '!' ? tag : "!<" + tag + ">";

        /// <summary>
        /// The reader's expanded form of a valid configured tag, as UTF-8: a local tag with its suffix percent-decoded
        /// (YAML 1.2, 6.8.2), a global tag as is (a verbatim tag is not decoded).
        /// </summary>
        internal static byte[] GetExpandedUtf8(string tag)
        {
            if (tag[0] != '!' || tag.IndexOf('%') < 0)
            {
                return Encoding.UTF8.GetBytes(tag);
            }

            var bytes = new List<byte>(tag.Length);
            bytes.Add((byte)'!');
            for (int i = 1; i < tag.Length; i++)
            {
                if (tag[i] == '%' && i + 2 < tag.Length && TryHex(tag[i + 1], out int high) && TryHex(tag[i + 2], out int low))
                {
                    bytes.Add((byte)((high << 4) | low));
                    i += 2;
                }
                else
                {
                    bytes.Add((byte)tag[i]);
                }
            }

            return bytes.ToArray();
        }

        private static bool IsUriText(string s, int start, bool tagChars)
        {
            if (s.Length - start < 1)
            {
                return false;
            }

            for (int i = start; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '%')
                {
                    if (i + 2 >= s.Length || !TryHex(s[i + 1], out _) || !TryHex(s[i + 2], out _))
                    {
                        return false;
                    }

                    i += 2;
                    continue;
                }

                if (!IsUriChar(c) || (tagChars && (c == '!' || c == ',' || c == '[' || c == ']')))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsUriChar(char c) =>
            (c >= '0' && c <= '9') || (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z')
            || c == '-' || c == '#' || c == ';' || c == '/' || c == '?' || c == ':' || c == '@' || c == '&'
            || c == '=' || c == '+' || c == '$' || c == ',' || c == '_' || c == '.' || c == '!' || c == '~'
            || c == '*' || c == '\'' || c == '(' || c == ')' || c == '[' || c == ']';

        private static bool TryHex(char c, out int value)
        {
            value = c >= '0' && c <= '9' ? c - '0' : c >= 'a' && c <= 'f' ? c - 'a' + 10 : c >= 'A' && c <= 'F' ? c - 'A' + 10 : -1;
            return value >= 0;
        }
    }
}
