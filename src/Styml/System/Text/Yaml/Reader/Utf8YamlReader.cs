// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace System.Text.Yaml
{
    /// <summary>
    /// Provides a high-performance API for forward-only, read-only access to the UTF-8 encoded YAML text.
    /// It processes the text sequentially with no caching and adheres strictly to the YAML 1.2 specification
    /// by default (https://tools.ietf.org/html/rfc8259). When it encounters invalid YAML, it throws
    /// a YamlException with basic error information like line number and byte position on the line.
    /// Since this type is a ref struct, it does not directly support async. However, it does provide
    /// support for reentrancy to read incomplete data, and continue reading once more data is presented.
    /// To be able to set max depth while reading OR allow skipping comments, create an instance of
    /// <see cref="YamlReaderState"/> and pass that in to the reader.
    /// </summary>
    [DebuggerDisplay("{DebuggerDisplay,nq}")]
    public ref partial struct Utf8YamlReader
    {
        private ReadOnlySpan<byte> _buffer;

        private readonly bool _isFinalBlock;
        private readonly bool _isInputSequence;

        private long _lineNumber;
        private long _bytePositionInLine;

        // bytes consumed in the current segment (not token)
        private int _consumed;
        private bool _inObject;
        private YamlTokenType _tokenType;
        private YamlTokenType _previousTokenType;
        private YamlReaderOptions _readerOptions;
        private BitStack _bitStack;

        private long _totalConsumed;
        private bool _isLastSegment;
        private readonly bool _isMultiSegment;

        private SequencePosition _currentPosition;
        private readonly ReadOnlySequence<byte> _sequence;

        private readonly bool IsLastSpan => _isFinalBlock && (!_isMultiSegment || _isLastSegment);

        internal readonly ReadOnlySequence<byte> OriginalSequence => _sequence;

        internal readonly ReadOnlySpan<byte> OriginalSpan => _sequence.IsEmpty ? _buffer : default;

        internal readonly int ValueLength => HasValueSequence ? checked((int)ValueSequence.Length) : ValueSpan.Length;

        internal readonly bool AllowMultipleValues => _readerOptions.AllowMultipleValues;

        /// <summary>
        /// Gets the value of the last processed token as a ReadOnlySpan&lt;byte&gt; slice
        /// of the input payload. If the YAML is provided within a ReadOnlySequence&lt;byte&gt;
        /// and the slice that represents the token value fits in a single segment, then
        /// <see cref="ValueSpan"/> will contain the sliced value since it can be represented as a span.
        /// Otherwise, the <see cref="ValueSequence"/> will contain the token value.
        /// </summary>
        /// <remarks>
        /// If <see cref="HasValueSequence"/> is true, <see cref="ValueSpan"/> contains useless data, likely for
        /// a previous single-segment token. Therefore, only access <see cref="ValueSpan"/> if <see cref="HasValueSequence"/> is false.
        /// Otherwise, the token value must be accessed from <see cref="ValueSequence"/>.
        /// </remarks>
        public ReadOnlySpan<byte> ValueSpan { get; private set; }

        /// <summary>
        /// Returns the total amount of bytes consumed by the <see cref="Utf8YamlReader"/> so far
        /// for the current instance of the <see cref="Utf8YamlReader"/> with the given UTF-8 encoded input text.
        /// </summary>
        public readonly long BytesConsumed
        {
            get
            {
#if DEBUG
                if (!_isInputSequence)
                {
                    Debug.Assert(_totalConsumed == 0);
                }
#endif
                return _totalConsumed + _consumed;
            }
        }

        /// <summary>
        /// Returns the index that the last processed YAML token starts at
        /// within the given UTF-8 encoded input text, skipping any white space.
        /// </summary>
        /// <remarks>
        /// For YAML strings (including property names), this points to before the start quote.
        /// For comments, this points to before the first comment delimiter (i.e. '/').
        /// </remarks>
        public long TokenStartIndex { get; private set; }

        /// <summary>
        /// Tracks the recursive depth of the nested objects / arrays within the YAML text
        /// processed so far. This provides the depth of the current token.
        /// </summary>
        public readonly int CurrentDepth
        {
            get
            {
                int readerDepth = _bitStack.CurrentDepth;
                if (TokenType is YamlTokenType.StartArray or YamlTokenType.StartObject)
                {
                    Debug.Assert(readerDepth >= 1);
                    readerDepth--;
                }
                return readerDepth;
            }
        }

        internal readonly bool IsInArray => !_inObject;

        /// <summary>
        /// Gets the type of the last processed YAML token in the UTF-8 encoded YAML text.
        /// </summary>
        public readonly YamlTokenType TokenType => _tokenType;

        /// <summary>
        /// Lets the caller know which of the two 'Value' properties to read to get the
        /// token value. For input data within a ReadOnlySpan&lt;byte&gt; this will
        /// always return false. For input data within a ReadOnlySequence&lt;byte&gt;, this
        /// will only return true if the token value straddles more than a single segment and
        /// hence couldn't be represented as a span.
        /// </summary>
        public bool HasValueSequence { get; private set; }

        /// <summary>
        /// Lets the caller know whether the current <see cref="ValueSpan" /> or <see cref="ValueSequence"/> properties
        /// contain escape sequences per RFC 8259 section 7, and therefore require unescaping before being consumed.
        /// </summary>
        public bool ValueIsEscaped { get; private set; }

        /// <summary>
        /// Returns the mode of this instance of the <see cref="Utf8YamlReader"/>.
        /// True when the reader was constructed with the input span containing the entire data to process.
        /// False when the reader was constructed knowing that the input span may contain partial data with more data to follow.
        /// </summary>
        public readonly bool IsFinalBlock => _isFinalBlock;

        /// <summary>
        /// Gets the value of the last processed token as a ReadOnlySpan&lt;byte&gt; slice
        /// of the input payload. If the YAML is provided within a ReadOnlySequence&lt;byte&gt;
        /// and the slice that represents the token value fits in a single segment, then
        /// <see cref="ValueSpan"/> will contain the sliced value since it can be represented as a span.
        /// Otherwise, the <see cref="ValueSequence"/> will contain the token value.
        /// </summary>
        /// <remarks>
        /// If <see cref="HasValueSequence"/> is false, <see cref="ValueSequence"/> contains useless data, likely for
        /// a previous multi-segment token. Therefore, only access <see cref="ValueSequence"/> if <see cref="HasValueSequence"/> is true.
        /// Otherwise, the token value must be accessed from <see cref="ValueSpan"/>.
        /// </remarks>
        public ReadOnlySequence<byte> ValueSequence { get; private set; }

        /// <summary>
        /// Returns the current <see cref="SequencePosition"/> within the provided UTF-8 encoded
        /// input ReadOnlySequence&lt;byte&gt;. If the <see cref="Utf8YamlReader"/> was constructed
        /// with a ReadOnlySpan&lt;byte&gt; instead, this will always return a default <see cref="SequencePosition"/>.
        /// </summary>
        public readonly SequencePosition Position
        {
            get
            {
                if (_isInputSequence)
                {
                    Debug.Assert(_currentPosition.GetObject() is not null);
                    return _sequence.GetPosition(_consumed, _currentPosition);
                }
                return default;
            }
        }

        /// <summary>
        /// Returns the current snapshot of the <see cref="Utf8YamlReader"/> state which must
        /// be captured by the caller and passed back in to the <see cref="Utf8YamlReader"/> ctor with more data.
        /// Unlike the <see cref="Utf8YamlReader"/>, which is a ref struct, the state can survive
        /// across async/await boundaries and hence this type is required to provide support for reading
        /// in more data asynchronously before continuing with a new instance of the <see cref="Utf8YamlReader"/>.
        /// </summary>
        public readonly YamlReaderState CurrentState => new YamlReaderState
        (
            lineNumber: _lineNumber,
            bytePositionInLine: _bytePositionInLine,
            inObject: _inObject,
            valueIsEscaped: ValueIsEscaped,
            tokenType: _tokenType,
            previousTokenType: _previousTokenType,
            readerOptions: _readerOptions,
            bitStack: _bitStack,
            yaml: _yaml
        );

        /// <summary>
        /// Constructs a new <see cref="Utf8YamlReader"/> instance.
        /// </summary>
        /// <param name="yamlData">The ReadOnlySpan&lt;byte&gt; containing the UTF-8 encoded YAML text to process.</param>
        /// <param name="isFinalBlock">True when the input span contains the entire data to process.
        /// Set to false only if it is known that the input span contains partial data with more data to follow.</param>
        /// <param name="state">If this is the first call to the ctor, pass in a default state. Otherwise,
        /// capture the state from the previous instance of the <see cref="Utf8YamlReader"/> and pass that back.</param>
        /// <remarks>
        /// Since this type is a ref struct, it is a stack-only type and all the limitations of ref structs apply to it.
        /// This is the reason why the ctor accepts a <see cref="YamlReaderState"/>.
        /// </remarks>
        public Utf8YamlReader(ReadOnlySpan<byte> yamlData, bool isFinalBlock, YamlReaderState state)
        {
            _buffer = yamlData;

            _isFinalBlock = isFinalBlock;
            _isInputSequence = false;

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
            _isLastSegment = _isFinalBlock;
            _isMultiSegment = false;

            ValueSpan = ReadOnlySpan<byte>.Empty;

            _currentPosition = default;
            _sequence = default;
            HasValueSequence = false;
            ValueSequence = ReadOnlySequence<byte>.Empty;
        }

        /// <summary>
        /// Constructs a new <see cref="Utf8YamlReader"/> instance.
        /// </summary>
        /// <param name="yamlData">The ReadOnlySpan&lt;byte&gt; containing the UTF-8 encoded YAML text to process.</param>
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
        public Utf8YamlReader(ReadOnlySpan<byte> yamlData, YamlReaderOptions options = default)
            : this(yamlData, isFinalBlock: true, new YamlReaderState(options))
        {
        }

        /// <summary>
        /// Read the next YAML token from input source.
        /// </summary>
        /// <returns>True if the token was read successfully, else false.</returns>
        /// <exception cref="YamlException">
        /// Thrown when an invalid YAML token is encountered according to the YAML 1.2 specification
        /// or if the current depth exceeds the recursive limit set by the max depth.
        /// </exception>
        public bool Read() => ReadYaml();

        /// <summary>
        /// Skips the children of the current YAML token.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the reader was given partial data with more data to follow (i.e. <see cref="IsFinalBlock"/> is false).
        /// </exception>
        /// <exception cref="YamlException">
        /// Thrown when an invalid YAML token is encountered while skipping, according to the YAML 1.2 specification,
        /// or if the current depth exceeds the recursive limit set by the max depth.
        /// </exception>
        /// <remarks>
        /// When <see cref="TokenType"/> is <see cref="YamlTokenType.PropertyName" />, the reader first moves to the property value.
        /// When <see cref="TokenType"/> (originally, or after advancing) is <see cref="YamlTokenType.StartObject" /> or
        /// <see cref="YamlTokenType.StartArray" />, the reader advances to the matching
        /// <see cref="YamlTokenType.EndObject" /> or <see cref="YamlTokenType.EndArray" />.
        ///
        /// For all other token types, the reader does not move. After the next call to <see cref="Read"/>, the reader will be at
        /// the next value (when in an array), the next property name (when in an object), or the end array/object token.
        /// </remarks>
        public void Skip()
        {
            if (!_isFinalBlock)
            {
                ThrowHelper.ThrowInvalidOperationException_CannotSkipOnPartial();
            }

            SkipHelper();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SkipHelper()
        {
            Debug.Assert(_isFinalBlock);

            if (TokenType is YamlTokenType.PropertyName)
            {
                bool result = Read();
                // Since _isFinalBlock == true here, and the JSON token is not a primitive value or comment.
                // Read() is guaranteed to return true OR throw for invalid/incomplete data.
                Debug.Assert(result);
            }

            if (TokenType is YamlTokenType.StartObject or YamlTokenType.StartArray)
            {
                int depth = CurrentDepth;
                do
                {
                    bool result = Read();
                    // Since _isFinalBlock == true here, and the JSON token is not a primitive value or comment.
                    // Read() is guaranteed to return true OR throw for invalid/incomplete data.
                    Debug.Assert(result);
                }
                while (depth < CurrentDepth);
            }
        }

        /// <summary>
        /// Tries to skip the children of the current YAML token.
        /// </summary>
        /// <returns>True if there was enough data for the children to be skipped successfully, else false.</returns>
        /// <exception cref="YamlException">
        /// Thrown when an invalid YAML token is encountered while skipping, according to the YAML 1.2 specification,
        /// or if the current depth exceeds the recursive limit set by the max depth.
        /// </exception>
        /// <remarks>
        ///   <para>
        ///     If the reader did not have enough data to completely skip the children of the current token,
        ///     it will be reset to the state it was in before the method was called.
        ///   </para>
        ///   <para>
        ///     When <see cref="TokenType"/> is <see cref="YamlTokenType.PropertyName" />, the reader first moves to the property value.
        ///     When <see cref="TokenType"/> (originally, or after advancing) is <see cref="YamlTokenType.StartObject" /> or
        ///     <see cref="YamlTokenType.StartArray" />, the reader advances to the matching
        ///     <see cref="YamlTokenType.EndObject" /> or <see cref="YamlTokenType.EndArray" />.
        ///
        ///     For all other token types, the reader does not move. After the next call to <see cref="Read"/>, the reader will be at
        ///     the next value (when in an array), the next property name (when in an object), or the end array/object token.
        ///   </para>
        /// </remarks>
        public bool TrySkip()
        {
            if (_isFinalBlock)
            {
                SkipHelper();
                return true;
            }

            Utf8YamlReader restore = this;
            bool success = TrySkipPartial(targetDepth: CurrentDepth);
            if (!success)
            {
                // Roll back the reader if it contains partial data.
                this = restore;
            }

            return success;
        }

        /// <summary>
        /// Tries to skip the children of the current YAML token, advancing the reader even if there is not enough data.
        /// The skip operation can be resumed later, provided that the same <paramref name="targetDepth" /> is passed.
        /// </summary>
        /// <param name="targetDepth">The target depth we want to eventually skip to.</param>
        /// <returns>True if the entire YAML value has been skipped.</returns>
        internal bool TrySkipPartial(int targetDepth)
        {
            Debug.Assert(0 <= targetDepth && targetDepth <= CurrentDepth);

            if (targetDepth == CurrentDepth)
            {
                // This is the first call to TrySkipHelper.
                if (TokenType is YamlTokenType.PropertyName)
                {
                    // Skip any property name tokens preceding the value.
                    if (!Read())
                    {
                        return false;
                    }
                }

                if (TokenType is not (YamlTokenType.StartObject or YamlTokenType.StartArray))
                {
                    // The next value is not an object or array, so there is nothing to skip.
                    return true;
                }
            }

            // Start or resume iterating through the JSON object or array.
            do
            {
                if (!Read())
                {
                    return false;
                }
            }
            while (targetDepth < CurrentDepth);

            Debug.Assert(targetDepth == CurrentDepth);
            return true;
        }

        /// <summary>
        /// Compares the UTF-8 encoded text to the unescaped YAML token value in the source and returns true if they match.
        /// </summary>
        /// <param name="utf8Text">The UTF-8 encoded text to compare against.</param>
        /// <returns>True if the YAML token value in the source matches the UTF-8 encoded look up text.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown if trying to find a text match on a YAML token that is not a string
        /// (i.e. other than <see cref="YamlTokenType.String"/> or <see cref="YamlTokenType.PropertyName"/>).
        /// <seealso cref="TokenType" />
        /// </exception>
        /// <remarks>
        ///   <para>
        ///     If the look up text is invalid UTF-8 text, the method will return false since you cannot have
        ///     invalid UTF-8 within the YAML payload.
        ///   </para>
        ///   <para>
        ///     The comparison of the YAML token value in the source and the look up text is done by first unescaping the YAML value in source,
        ///     if required. The look up text is matched as is, without any modifications to it.
        ///   </para>
        /// </remarks>
        public readonly bool ValueTextEquals(ReadOnlySpan<byte> utf8Text)
        {
            if (!IsTokenTypeStringOrYamlScalar)
            {
                ThrowHelper.ThrowInvalidOperationException_ExpectedStringComparison(TokenType);
            }

            return TextEqualsHelper(utf8Text);
        }

        /// <summary>
        /// Compares the string text to the unescaped YAML token value in the source and returns true if they match.
        /// </summary>
        /// <param name="text">The text to compare against.</param>
        /// <returns>True if the YAML token value in the source matches the look up text.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown if trying to find a text match on a YAML token that is not a string
        /// (i.e. other than <see cref="YamlTokenType.String"/> or <see cref="YamlTokenType.PropertyName"/>).
        /// <seealso cref="TokenType" />
        /// </exception>
        /// <remarks>
        ///   <para>
        ///     If the look up text is invalid UTF-8 text, the method will return false since you cannot have
        ///     invalid UTF-8 within the YAML payload.
        ///   </para>
        ///   <para>
        ///     The comparison of the YAML token value in the source and the look up text is done by first unescaping the YAML value in source,
        ///     if required. The look up text is matched as is, without any modifications to it.
        ///   </para>
        /// </remarks>
        public readonly bool ValueTextEquals(string? text)
        {
            return ValueTextEquals(text.AsSpan());
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private readonly bool TextEqualsHelper(ReadOnlySpan<byte> otherUtf8Text)
        {
            if (_yamlNeedsProcessing)
            {
                return otherUtf8Text.SequenceEqual(GetYamlProcessedValue());
            }

            if (HasValueSequence)
            {
                return CompareToSequence(otherUtf8Text);
            }

            if (ValueIsEscaped)
            {
                return UnescapeAndCompare(otherUtf8Text);
            }

            return otherUtf8Text.SequenceEqual(ValueSpan);
        }

        /// <summary>
        /// Compares the text to the unescaped YAML token value in the source and returns true if they match.
        /// </summary>
        /// <param name="text">The text to compare against.</param>
        /// <returns>True if the YAML token value in the source matches the look up text.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown if trying to find a text match on a YAML token that is not a string
        /// (i.e. other than <see cref="YamlTokenType.String"/> or <see cref="YamlTokenType.PropertyName"/>).
        /// <seealso cref="TokenType" />
        /// </exception>
        /// <remarks>
        ///   <para>
        ///     If the look up text is invalid or incomplete UTF-16 text (i.e. unpaired surrogates), the method will return false
        ///     since you cannot have invalid UTF-16 within the YAML payload.
        ///   </para>
        ///   <para>
        ///     The comparison of the YAML token value in the source and the look up text is done by first unescaping the YAML value in source,
        ///     if required. The look up text is matched as is, without any modifications to it.
        ///   </para>
        /// </remarks>
        public readonly unsafe bool ValueTextEquals(ReadOnlySpan<char> text)
        {
            if (!IsTokenTypeStringOrYamlScalar)
            {
                ThrowHelper.ThrowInvalidOperationException_ExpectedStringComparison(TokenType);
            }

            if (MatchNotPossible(text.Length))
            {
                return false;
            }

            byte[]? otherUtf8TextArray = null;

            scoped Span<byte> otherUtf8Text;

            int length = checked(text.Length * YamlConstants.MaxExpansionFactorWhileTranscoding);

            if (length > YamlConstants.StackallocByteThreshold)
            {
                otherUtf8TextArray = ArrayPool<byte>.Shared.Rent(length);
                otherUtf8Text = otherUtf8TextArray;
            }
            else
            {
                otherUtf8Text = stackalloc byte[YamlConstants.StackallocByteThreshold];
            }

            OperationStatus status = YamlWriterHelper.ToUtf8(text, otherUtf8Text, out int written);
            Debug.Assert(status != OperationStatus.DestinationTooSmall);
            bool result;
            if (status == OperationStatus.InvalidData)
            {
                result = false;
            }
            else
            {
                Debug.Assert(status == OperationStatus.Done);
                result = TextEqualsHelper(otherUtf8Text.Slice(0, written));
            }

            if (otherUtf8TextArray is not null)
            {
                otherUtf8Text.Slice(0, written).Clear();
                ArrayPool<byte>.Shared.Return(otherUtf8TextArray);
            }

            return result;
        }

        private readonly bool CompareToSequence(ReadOnlySpan<byte> other)
        {
            Debug.Assert(HasValueSequence);

            if (ValueIsEscaped)
            {
                return UnescapeSequenceAndCompare(other);
            }

            ReadOnlySequence<byte> localSequence = ValueSequence;

            // M7c: the YAML reader gives an empty scalar of a multi-segment input an empty sequence, a single segment.
            Debug.Assert(!localSequence.IsSingleSegment || localSequence.IsEmpty);

            if (localSequence.Length != other.Length)
            {
                return false;
            }

            int matchedSoFar = 0;

            foreach (ReadOnlyMemory<byte> memory in localSequence)
            {
                ReadOnlySpan<byte> span = memory.Span;

                if (other.Slice(matchedSoFar).StartsWith(span))
                {
                    matchedSoFar += span.Length;
                }
                else
                {
                    return false;
                }
            }
            return true;
        }

        private readonly bool UnescapeAndCompare(ReadOnlySpan<byte> other)
        {
            Debug.Assert(!HasValueSequence);
            ReadOnlySpan<byte> localSpan = ValueSpan;

            if (localSpan.Length < other.Length || localSpan.Length / YamlConstants.MaxExpansionFactorWhileEscaping > other.Length)
            {
                return false;
            }

            int idx = localSpan.IndexOf(YamlConstants.BackSlash);
            Debug.Assert(idx != -1);

            if (!other.StartsWith(localSpan.Slice(0, idx)))
            {
                return false;
            }

            return YamlReaderHelper.UnescapeAndCompare(localSpan.Slice(idx), other.Slice(idx));
        }

        private readonly bool UnescapeSequenceAndCompare(ReadOnlySpan<byte> other)
        {
            Debug.Assert(HasValueSequence);
            Debug.Assert(!ValueSequence.IsSingleSegment);

            ReadOnlySequence<byte> localSequence = ValueSequence;
            long sequenceLength = localSequence.Length;

            // The JSON token value will at most shrink by 6 when unescaping.
            // If it is still larger than the lookup string, there is no value in unescaping and doing the comparison.
            if (sequenceLength < other.Length || sequenceLength / YamlConstants.MaxExpansionFactorWhileEscaping > other.Length)
            {
                return false;
            }

            int matchedSoFar = 0;

            bool result = false;

            foreach (ReadOnlyMemory<byte> memory in localSequence)
            {
                ReadOnlySpan<byte> span = memory.Span;

                int idx = span.IndexOf(YamlConstants.BackSlash);

                if (idx != -1)
                {
                    if (!other.Slice(matchedSoFar).StartsWith(span.Slice(0, idx)))
                    {
                        break;
                    }
                    matchedSoFar += idx;

                    other = other.Slice(matchedSoFar);
                    localSequence = localSequence.Slice(matchedSoFar);

                    if (localSequence.IsSingleSegment)
                    {
                        result = YamlReaderHelper.UnescapeAndCompare(localSequence.First.Span, other);
                    }
                    else
                    {
                        result = YamlReaderHelper.UnescapeAndCompare(localSequence, other);
                    }
                    break;
                }

                if (!other.Slice(matchedSoFar).StartsWith(span))
                {
                    break;
                }
                matchedSoFar += span.Length;
            }

            return result;
        }

        // Returns true if the TokenType is a primitive string "value", i.e. PropertyName or String
        // Otherwise, return false.
        private static bool IsTokenTypeString(YamlTokenType tokenType)
        {
            return tokenType is YamlTokenType.PropertyName or YamlTokenType.String;
        }

        private readonly bool IsTokenTypeStringOrYamlScalar => IsTokenTypeString(TokenType) || IsYamlScalarText;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private readonly bool MatchNotPossible(int charTextLength)
        {
            if (HasValueSequence)
            {
                return MatchNotPossibleSequence(charTextLength);
            }

            int sourceLength = ValueSpan.Length;

            // Transcoding from UTF-16 to UTF-8 will change the length by somwhere between 1x and 3x.
            // Unescaping the token value will at most shrink its length by 6x.
            // There is no point incurring the transcoding/unescaping/comparing cost if:
            // - The token value is smaller than charTextLength
            // - The token value needs to be transcoded AND unescaped and it is more than 6x larger than charTextLength
            //      - For an ASCII UTF-16 characters, transcoding = 1x, escaping = 6x => 6x factor
            //      - For non-ASCII UTF-16 characters within the BMP, transcoding = 2-3x, but they are represented as a single escaped hex value, \uXXXX => 6x factor
            //      - For non-ASCII UTF-16 characters outside of the BMP, transcoding = 4x, but the surrogate pair (2 characters) are represented by 16 bytes \uXXXX\uXXXX => 6x factor
            // - The token value needs to be transcoded, but NOT escaped and it is more than 3x larger than charTextLength
            //      - For an ASCII UTF-16 characters, transcoding = 1x,
            //      - For non-ASCII UTF-16 characters within the BMP, transcoding = 2-3x,
            //      - For non-ASCII UTF-16 characters outside of the BMP, transcoding = 2x, (surrogate pairs - 2 characters transcode to 4 UTF-8 bytes)

            if (sourceLength < charTextLength
                || sourceLength / (ValueIsEscaped ? YamlConstants.MaxExpansionFactorWhileEscaping : YamlConstants.MaxExpansionFactorWhileTranscoding) > charTextLength)
            {
                return true;
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private readonly bool MatchNotPossibleSequence(int charTextLength)
        {
            long sourceLength = ValueSequence.Length;

            if (sourceLength < charTextLength
                || sourceLength / (ValueIsEscaped ? YamlConstants.MaxExpansionFactorWhileEscaping : YamlConstants.MaxExpansionFactorWhileTranscoding) > charTextLength)
            {
                return true;
            }
            return false;
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string DebuggerDisplay => $"TokenType = {DebugTokenType}, TokenStartIndex = {TokenStartIndex}, Consumed = {BytesConsumed}";

        // Using TokenType.ToString() (or {TokenType}) fails to render in the debug window. The
        // message "The runtime refused to evaluate the expression at this time." is shown. This
        // is a workaround until we root cause and fix the issue.
        private string DebugTokenType
            => TokenType switch
            {
                YamlTokenType.Comment => nameof(YamlTokenType.Comment),
                YamlTokenType.EndArray => nameof(YamlTokenType.EndArray),
                YamlTokenType.EndObject => nameof(YamlTokenType.EndObject),
                YamlTokenType.False => nameof(YamlTokenType.False),
                YamlTokenType.None => nameof(YamlTokenType.None),
                YamlTokenType.Null => nameof(YamlTokenType.Null),
                YamlTokenType.Number => nameof(YamlTokenType.Number),
                YamlTokenType.PropertyName => nameof(YamlTokenType.PropertyName),
                YamlTokenType.StartArray => nameof(YamlTokenType.StartArray),
                YamlTokenType.StartObject => nameof(YamlTokenType.StartObject),
                YamlTokenType.String => nameof(YamlTokenType.String),
                YamlTokenType.True => nameof(YamlTokenType.True),
                _ => ((byte)TokenType).ToString()
            };

        private ReadOnlySpan<byte> GetUnescapedSpan()
        {
            if (_yamlNeedsProcessing)
            {
                return GetYamlProcessedValue();
            }

            ReadOnlySpan<byte> span = ValueBytesOrSequenceCopy;
            if (ValueIsEscaped)
            {
                span = YamlReaderHelper.GetUnescaped(span);
            }

            return span;
        }
    }
}
