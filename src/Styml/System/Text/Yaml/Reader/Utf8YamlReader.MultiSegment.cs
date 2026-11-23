// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace System.Text.Yaml
{
    public ref partial struct Utf8YamlReader
    {
        /// <summary>
        /// Constructs a new <see cref="Utf8YamlReader"/> instance.
        /// </summary>
        /// <param name="yamlData">The ReadOnlySequence&lt;byte&gt; containing the UTF-8 encoded YAML text to process.</param>
        /// <param name="isFinalBlock">True when the input span contains the entire data to process.
        /// Set to false only if it is known that the input span contains partial data with more data to follow.</param>
        /// <param name="state">If this is the first call to the ctor, pass in a default state. Otherwise,
        /// capture the state from the previous instance of the <see cref="Utf8YamlReader"/> and pass that back.</param>
        /// <remarks>
        /// Since this type is a ref struct, it is a stack-only type and all the limitations of ref structs apply to it.
        /// This is the reason why the ctor accepts a <see cref="YamlReaderState"/>.
        /// </remarks>
        public Utf8YamlReader(ReadOnlySequence<byte> yamlData, bool isFinalBlock, YamlReaderState state)
        {
            _buffer = yamlData.First.Span;

            _isFinalBlock = isFinalBlock;
            _isInputSequence = true;

            _lineNumber = state._lineNumber;
            _bytePositionInLine = state._bytePositionInLine;
            _inObject = state._inObject;
            ValueIsEscaped = state._valueIsEscaped;
            _tokenType = state._tokenType;
            _previousTokenType = state._previousTokenType;
            _readerOptions = state._readerOptions;
            if (_readerOptions.MaxDepth == 0)
            {
                _readerOptions.MaxDepth = YamlReaderOptions.DefaultMaxDepth;  // If max depth is not set, revert to the default depth.
            }
            _bitStack = state._bitStack;
            _yaml = state._yaml;

            _consumed = 0;
            TokenStartIndex = 0;
            _totalConsumed = 0;

            ValueSpan = ReadOnlySpan<byte>.Empty;

            _sequence = yamlData;
            HasValueSequence = false;
            ValueSequence = ReadOnlySequence<byte>.Empty;

            if (yamlData.IsSingleSegment)
            {
                _currentPosition = yamlData.Start;
                _isLastSegment = isFinalBlock;
                _isMultiSegment = false;
            }
            else
            {
                _currentPosition = yamlData.Start;
                SequencePosition nextPosition = _currentPosition;

                bool firstSegmentIsEmpty = _buffer.Length == 0;
                if (firstSegmentIsEmpty)
                {
                    // Once we find a non-empty segment, we need to set current position to it.
                    // Therefore, track the next position in a copy before it gets advanced to the next segment.
                    SequencePosition previousNextPosition = nextPosition;
                    while (yamlData.TryGet(ref nextPosition, out ReadOnlyMemory<byte> memory, advance: true))
                    {
                        // _currentPosition should point to the segment right befor the segment that nextPosition points to.
                        _currentPosition = previousNextPosition;
                        if (memory.Length != 0)
                        {
                            _buffer = memory.Span;
                            break;
                        }
                        previousNextPosition = nextPosition;
                    }
                }

                // If firstSegmentIsEmpty is true,
                //    only check if we have reached the last segment but do not advance nextPosition. The while loop above already advanced it.
                //    Otherwise, we would end up skipping a segment (i.e. advance = false).
                // If firstSegmentIsEmpty is false,
                //    make sure to advance nextPosition so that it is no longer the same as _currentPosition (i.e. advance = true).
                _isLastSegment = !yamlData.TryGet(ref nextPosition, out _, advance: !firstSegmentIsEmpty) && isFinalBlock; // Don't re-order to avoid short-circuiting

                Debug.Assert(!nextPosition.Equals(_currentPosition));

                _isMultiSegment = true;
            }
        }

        /// <summary>
        /// Constructs a new <see cref="Utf8YamlReader"/> instance.
        /// </summary>
        /// <param name="yamlData">The ReadOnlySequence&lt;byte&gt; containing the UTF-8 encoded YAML text to process.</param>
        /// <param name="options">Defines the customized behavior of the <see cref="Utf8YamlReader"/>
        /// that is different from the defaults (for example how to handle comments or maximum depth allowed when reading).
        /// By default, the <see cref="Utf8YamlReader"/> follows the YAML 1.2 specification and reads up to a maximum depth of 64.</param>
        /// <remarks>
        ///   <para>
        ///     Since this type is a ref struct, it is a stack-only type and all the limitations of ref structs apply to it.
        ///   </para>
        ///   <para>
        ///     This assumes that the entire YAML payload is passed in (equivalent to <see cref="IsFinalBlock"/> = true)
        ///   </para>
        /// </remarks>
        public Utf8YamlReader(ReadOnlySequence<byte> yamlData, YamlReaderOptions options = default)
            : this(yamlData, isFinalBlock: true, new YamlReaderState(options))
        {
        }
    }
}
