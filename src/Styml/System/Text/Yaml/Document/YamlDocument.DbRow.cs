// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

namespace System.Text.Yaml
{
    public sealed partial class YamlDocument
    {
        [StructLayout(LayoutKind.Sequential)]
        internal readonly struct DbRow
        {
            internal const int Size = 12;

            // Sign bit: the row has an entry in the document's node table (YAML: anchor, tag, raw range, M5a)
            private readonly int _location;

            // Sign bit is used for "HasComplexChildren" (StartArray)
            private readonly int _sizeOrLengthUnion;

            // Top nybble is YamlTokenType
            // remaining nybbles are the number of rows to skip to get to the next value
            // This isn't limiting on the number of rows, since Span.MaxLength / sizeof(DbRow) can't
            // exceed that range.
            private readonly int _numberOfRowsAndTypeUnion;

            /// <summary>
            /// Index into the payload
            /// </summary>
            internal int Location => _location & int.MaxValue;

            /// <summary>The row has an entry in the document's node table (anchor, tag or raw source range).</summary>
            internal bool HasNodeInfo => _location < 0;

            /// <summary>
            /// length of text in YAML payload (or number of elements if its a YAML array)
            /// </summary>
            internal int SizeOrLength => _sizeOrLengthUnion & int.MaxValue;

            internal bool IsUnknownSize => _sizeOrLengthUnion == UnknownSize;

            /// <summary>
            /// String/PropertyName: Unescaping is required.
            /// Array: At least one element is an object/array.
            /// Otherwise; false
            /// </summary>
            internal bool HasComplexChildren => _sizeOrLengthUnion < 0;

            internal int NumberOfRows =>
                _numberOfRowsAndTypeUnion & 0x0FFFFFFF; // Number of rows that the current JSON element occupies within the database

            // A simple value never uses its row count, so its low bits hold the YAML scalar flags (M5a).
            internal const int PlainFlag = 1;          // the raw text has no quotes
            internal const int DecodedFlag = 2;        // the value bytes follow the source (decoded YAML scalar)
            internal const int RecursiveAliasFlag = 4; // an alias inside the node it names
            internal const int ComplexKeyFlag = 4;     // on a PropertyName row (M5e): the key is a collection, whose rows follow
            internal const int StyleShift = 3;         // bits 3-5: the scalar's YamlScalarStyle (0: none recorded)

            internal static int StyleBits(YamlScalarStyle style) => (int)style << StyleShift;

            internal YamlScalarStyle ScalarStyle
            {
                get
                {
                    Debug.Assert(IsSimpleValue);
                    return (YamlScalarStyle)((_numberOfRowsAndTypeUnion >> StyleShift) & 0x7);
                }
            }

            internal int ScalarFlags
            {
                get
                {
                    Debug.Assert(IsSimpleValue);
                    return _numberOfRowsAndTypeUnion & 0x7;
                }
            }

            internal YamlTokenType TokenType => (YamlTokenType)(unchecked((uint)_numberOfRowsAndTypeUnion) >> 28);

            internal const int UnknownSize = -1;

            internal DbRow(YamlTokenType yamlTokenType, int location, int sizeOrLength)
            {
                Debug.Assert(yamlTokenType is > YamlTokenType.None and <= YamlTokenType.Alias and not YamlTokenType.Comment);
                Debug.Assert((byte)yamlTokenType < 1 << 4);
                Debug.Assert(location >= 0);
                Debug.Assert(sizeOrLength >= UnknownSize);
                Debug.Assert(sizeof(DbRow) == Size);

                _location = location;
                _sizeOrLengthUnion = sizeOrLength;
                _numberOfRowsAndTypeUnion = (int)yamlTokenType << 28;
            }

            internal DbRow(YamlTokenType yamlTokenType, int location, int sizeOrLength, int scalarFlags, bool hasNodeInfo)
                : this(yamlTokenType, location, sizeOrLength)
            {
                Debug.Assert(scalarFlags is >= 0 and <= 63);
                Debug.Assert(scalarFlags == 0 || yamlTokenType >= YamlTokenType.PropertyName);
                _numberOfRowsAndTypeUnion |= scalarFlags;
                if (hasNodeInfo)
                {
                    _location |= int.MinValue;
                }
            }

            internal bool IsSimpleValue => TokenType >= YamlTokenType.PropertyName;
        }
    }
}
