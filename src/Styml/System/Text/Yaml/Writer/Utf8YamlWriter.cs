// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics.CodeAnalysis;

namespace System.Text.Yaml
{
    /// <summary>
    /// Provides a high-performance API for forward-only, non-cached writing of UTF-8 encoded YAML text.
    /// </summary>
    /// <remarks>
    ///   <para>
    ///     It writes the text sequentially with no caching and adheres to the YAML 1.2 specification
    ///     by default (https://tools.ietf.org/html/rfc8259), with the exception of writing comments.
    ///   </para>
    ///   <para>
    ///     When the user attempts to write invalid YAML and validation is enabled, it throws
    ///     an <see cref="InvalidOperationException"/> with a context specific error message.
    ///   </para>
    ///   <para>
    ///     To be able to format the output with indentation and whitespace OR to skip validation, create an instance of
    ///     <see cref="YamlWriterOptions"/> and pass that in to the writer.
    ///   </para>
    /// </remarks>
    [DebuggerDisplay("{DebuggerDisplay,nq}")]
    public sealed partial class Utf8YamlWriter : IDisposable, IAsyncDisposable
    {
        private const int DefaultGrowthSize = 4096;
        private const int InitialGrowthSize = 256;

        private IBufferWriter<byte>? _output;
        private Stream? _stream;
        private ArrayBufferWriter<byte>? _arrayBufferWriter;

        private Memory<byte> _memory;

        private EnclosingContainerType _enclosingContainer;
        private bool _commentAfterNoneOrPropertyName;
        private YamlTokenType _tokenType;
        private BitStack _bitStack;

        /// <summary>
        /// This 3-byte array stores the partial string data leftover when writing a string value
        /// segment that is split across multiple segment write calls.
        /// </summary>
#if !NET
        private byte[]? _partialStringData;
        private Span<byte> PartialStringDataRaw => _partialStringData ??= new byte[3];
#else
        private Inline3ByteArray _partialStringData;
        private Span<byte> PartialStringDataRaw => _partialStringData;

        [InlineArray(3)]
        private struct Inline3ByteArray
        {
            public byte byte0;
        }
#endif

        /// <summary>
        /// Length of the partial string data.
        /// </summary>
        private byte _partialStringDataLength;

        // The highest order bit of _currentDepth is used to discern whether we are writing the first item in a list or not.
        // if (_currentDepth >> 31) == 1, add a list separator before writing the item
        // else, no list separator is needed since we are writing the first item.
        private int _currentDepth;

        private YamlWriterOptions _options; // Since YamlWriterOptions is a struct, use a field to avoid a copy for internal code.

        // Cache indentation settings from YamlWriterOptions to avoid recomputing them in the hot path.
        private byte _indentByte;
        private int _indentLength;

        // A length of 1 will emit LF for indented writes, a length of 2 will emit CRLF. Other values are invalid.
        private int _newLineLength;

        // Block style: BlockPrefixLength's part that does not depend on the depth, _indentLength + _newLineLength + 4.
        private int _blockPrefixFixed;

        /// <summary>
        /// Returns the amount of bytes written by the <see cref="Utf8YamlWriter"/> so far
        /// that have not yet been flushed to the output and committed.
        /// </summary>
        public int BytesPending { get; private set; }

        /// <summary>
        /// Returns the amount of bytes committed to the output by the <see cref="Utf8YamlWriter"/> so far.
        /// </summary>
        /// <remarks>
        /// In the case of IBufferwriter, this is how much the IBufferWriter has advanced.
        /// In the case of Stream, this is how much data has been written to the stream.
        /// </remarks>
        public long BytesCommitted { get; private set; }

        /// <summary>
        /// Gets the custom behavior when writing YAML using
        /// the <see cref="Utf8YamlWriter"/> which indicates whether to format the output
        /// while writing and whether to skip structural YAML validation or not.
        /// </summary>
        public YamlWriterOptions Options
        {
            get
            {
                YamlWriterOptions options = _options;
                if (ReferenceEquals(options.Encoder, YamlBlockEncoder.Instance))
                {
                    options.Encoder = null;
                }

                return options;
            }
        }

        private int Indentation => CurrentDepth * _indentLength;

        internal YamlTokenType TokenType => _tokenType;

        /// <summary>
        /// Tracks the recursive depth of the nested objects / arrays within the YAML text
        /// written so far. This provides the depth of the current token.
        /// </summary>
        public int CurrentDepth => _currentDepth & YamlConstants.RemoveFlagsBitMask;

        /// <summary>
        /// The partial UTF-8 code point.
        /// </summary>
        private ReadOnlySpan<byte> PartialUtf8StringData
        {
            get
            {
                Debug.Assert(_enclosingContainer == EnclosingContainerType.Utf8StringSequence);

                ReadOnlySpan<byte> partialStringDataBytes = PartialStringDataRaw;
                Debug.Assert(partialStringDataBytes.Length == 3);

                byte length = _partialStringDataLength;
                Debug.Assert(length < 4);

                return partialStringDataBytes.Slice(0, length);
            }

            set
            {
                Debug.Assert(value.Length <= 3);

                Span<byte> partialStringDataBytes = PartialStringDataRaw;

                value.CopyTo(partialStringDataBytes);
                _partialStringDataLength = (byte)value.Length;
            }
        }

        /// <summary>
        /// The partial UTF-16 code point.
        /// </summary>
        private ReadOnlySpan<char> PartialUtf16StringData
        {
            get
            {
                Debug.Assert(_enclosingContainer == EnclosingContainerType.Utf16StringSequence);

                ReadOnlySpan<byte> partialStringDataBytes = PartialStringDataRaw;
                Debug.Assert(partialStringDataBytes.Length == 3);

                byte length = _partialStringDataLength;
                Debug.Assert(length is 2 or 0);

                return MemoryMarshal.Cast<byte, char>(partialStringDataBytes.Slice(0, length));
            }
            set
            {
                Debug.Assert(value.Length <= 1);

                Span<byte> partialStringDataBytes = PartialStringDataRaw;

                value.CopyTo(MemoryMarshal.Cast<byte, char>(partialStringDataBytes));
                _partialStringDataLength = (byte)(2 * value.Length);
            }
        }

        /// <summary>
        /// The partial base64 data.
        /// </summary>
        private ReadOnlySpan<byte> PartialBase64StringData
        {
            get
            {
                Debug.Assert(_enclosingContainer == EnclosingContainerType.Base64StringSequence);

                ReadOnlySpan<byte> partialStringDataBytes = PartialStringDataRaw;
                Debug.Assert(partialStringDataBytes.Length == 3);

                byte length = _partialStringDataLength;
                Debug.Assert(length < 3);

                return partialStringDataBytes.Slice(0, length);
            }
            set
            {
                Debug.Assert(value.Length < 3);

                Span<byte> partialStringDataBytes = PartialStringDataRaw;

                value.CopyTo(partialStringDataBytes);
                _partialStringDataLength = (byte)value.Length;
            }
        }

        private Utf8YamlWriter()
        {
        }

        /// <summary>
        /// Constructs a new <see cref="Utf8YamlWriter"/> instance with a specified <paramref name="bufferWriter"/>.
        /// </summary>
        /// <param name="bufferWriter">An instance of <see cref="IBufferWriter{Byte}" /> used as a destination for writing YAML text into.</param>
        /// <param name="options">Defines the customized behavior of the <see cref="Utf8YamlWriter"/>
        /// By default, the <see cref="Utf8YamlWriter"/> writes YAML minimized (that is, with no extra whitespace)
        /// and validates that the YAML being written is structurally valid.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when the instance of <see cref="IBufferWriter{Byte}" /> that is passed in is null.
        /// </exception>
        public Utf8YamlWriter(IBufferWriter<byte> bufferWriter, YamlWriterOptions options = default)
        {
            ArgumentNullException.ThrowIfNull(bufferWriter);

            _output = bufferWriter;
            SetOptions(options);
        }

        /// <summary>
        /// Constructs a new <see cref="Utf8YamlWriter"/> instance with a specified <paramref name="utf8Yaml"/>.
        /// </summary>
        /// <param name="utf8Yaml">An instance of <see cref="Stream" /> used as a destination for writing YAML text into.</param>
        /// <param name="options">Defines the customized behavior of the <see cref="Utf8YamlWriter"/>
        /// By default, the <see cref="Utf8YamlWriter"/> writes YAML minimized (that is, with no extra whitespace)
        /// and validates that the YAML being written is structurally valid.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when the instance of <see cref="Stream" /> that is passed in is null.
        /// </exception>
        public Utf8YamlWriter(Stream utf8Yaml, YamlWriterOptions options = default)
        {
            ArgumentNullException.ThrowIfNull(utf8Yaml);

            if (!utf8Yaml.CanWrite)
                throw new ArgumentException(SR.StreamNotWritable);

            _stream = utf8Yaml;
            SetOptions(options);

            _arrayBufferWriter = new ArrayBufferWriter<byte>();
        }

        private void SetOptions(YamlWriterOptions options)
        {
            if (options.IsBlockStyle)
            {
                // A dash needs a space after it, and YAML indentation is
                // spaces. The property setters cannot see the style, so the writer checks them.
                if (options.IndentSize < 2)
                {
                    ThrowHelper.ThrowArgumentException_BlockStyleIndentSizeTooSmall();
                }

                if (options.IndentCharacter != YamlConstants.DefaultIndentCharacter)
                {
                    ThrowHelper.ThrowArgumentException_BlockStyleTabIndentation();
                }
            }

            _options = options;
            _style = options.IsBlockStyle ? WriterStyleBlock : options.Indented ? WriterStyleJsonIndented : WriterStyleJsonMinimized;
            if (_style == WriterStyleBlock && options.Encoder is null)
            {
                // Block style escapes only what YAML requires: the shared escaping scan takes the YAML set's
                // encoder, so non-ASCII text reaches the block body as it is. Options still reports no encoder.
                _options.Encoder = YamlBlockEncoder.Instance;
            }
            _lenient = _style == WriterStyleBlock && options.QuoteForLenientReaders;
            _startEndSlow = _style != WriterStyleJsonMinimized || !options.SkipValidation;
            _indentByte = (byte)_options.IndentCharacter;
            _indentLength = options.IndentSize;

            Debug.Assert(options.NewLine is "\n" or "\r\n", "Invalid NewLine string.");
            _newLineLength = options.NewLine.Length;
            _blockPrefixFixed = _indentLength + _newLineLength + 4;

            if (_options.MaxDepth == 0)
            {
                _options.MaxDepth = YamlWriterOptions.DefaultMaxDepth; // If max depth is not set, revert to the default depth.
            }

            ResetBlockState();
        }

        /// <summary>
        /// Resets the <see cref="Utf8YamlWriter"/> internal state so that it can be re-used.
        /// </summary>
        /// <remarks>
        /// The <see cref="Utf8YamlWriter"/> will continue to use the original writer options
        /// and the original output as the destination (either <see cref="IBufferWriter{Byte}" /> or <see cref="Stream" />).
        /// </remarks>
        /// <exception cref="ObjectDisposedException">
        ///   The instance of <see cref="Utf8YamlWriter"/> has been disposed.
        /// </exception>
        public void Reset()
        {
            CheckNotDisposed();

            _arrayBufferWriter?.Clear();
            ResetHelper();
        }

        /// <summary>
        /// Resets the <see cref="Utf8YamlWriter"/> internal state so that it can be re-used with the new instance of <see cref="Stream" />.
        /// </summary>
        /// <param name="utf8Yaml">An instance of <see cref="Stream" /> used as a destination for writing YAML text into.</param>
        /// <remarks>
        /// The <see cref="Utf8YamlWriter"/> will continue to use the original writer options
        /// but now write to the passed in <see cref="Stream" /> as the new destination.
        /// </remarks>
        /// <exception cref="ArgumentNullException">
        /// Thrown when the instance of <see cref="Stream" /> that is passed in is null.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when the instance of <see cref="Stream" /> that is passed in does not support writing.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        ///   The instance of <see cref="Utf8YamlWriter"/> has been disposed.
        /// </exception>
        public void Reset(Stream utf8Yaml)
        {
            CheckNotDisposed();

            if (utf8Yaml is null)
            {
                throw new ArgumentNullException(nameof(utf8Yaml));
            }

            if (!utf8Yaml.CanWrite)
            {
                throw new ArgumentException(SR.StreamNotWritable);
            }

            _stream = utf8Yaml;
            if (_arrayBufferWriter is null)
            {
                _arrayBufferWriter = new ArrayBufferWriter<byte>();
            }
            else
            {
                _arrayBufferWriter.Clear();
            }

            _output = null;
            ResetHelper();
        }

        /// <summary>
        /// Resets the <see cref="Utf8YamlWriter"/> internal state so that it can be re-used with the new instance of <see cref="Stream" />
        /// and the specified <see cref="YamlWriterOptions"/>.
        /// </summary>
        /// <param name="utf8Yaml">An instance of <see cref="Stream" /> used as a destination for writing YAML text into.</param>
        /// <param name="options">Defines the customized behavior of the <see cref="Utf8YamlWriter"/>.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when the instance of <see cref="Stream" /> that is passed in is null.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when the instance of <see cref="Stream" /> that is passed in does not support writing.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        ///   The instance of <see cref="Utf8YamlWriter"/> has been disposed.
        /// </exception>
        public void Reset(Stream utf8Yaml, YamlWriterOptions options)
        {
            Reset(utf8Yaml);
            SetOptions(options);
        }

        /// <summary>
        /// Resets the <see cref="Utf8YamlWriter"/> internal state so that it can be re-used with the new instance of <see cref="IBufferWriter{Byte}" />.
        /// </summary>
        /// <param name="bufferWriter">An instance of <see cref="IBufferWriter{Byte}" /> used as a destination for writing YAML text into.</param>
        /// <remarks>
        /// The <see cref="Utf8YamlWriter"/> will continue to use the original writer options
        /// but now write to the passed in <see cref="IBufferWriter{Byte}" /> as the new destination.
        /// </remarks>
        /// <exception cref="ArgumentNullException">
        /// Thrown when the instance of <see cref="IBufferWriter{Byte}" /> that is passed in is null.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        ///   The instance of <see cref="Utf8YamlWriter"/> has been disposed.
        /// </exception>
        public void Reset(IBufferWriter<byte> bufferWriter)
        {
            CheckNotDisposed();

            _output = bufferWriter ?? throw new ArgumentNullException(nameof(bufferWriter));
            _stream = null;
            _arrayBufferWriter = null;

            ResetHelper();
        }

        /// <summary>
        /// Resets the <see cref="Utf8YamlWriter"/> internal state so that it can be re-used with the new instance of <see cref="IBufferWriter{Byte}" />
        /// and the specified <see cref="YamlWriterOptions"/>.
        /// </summary>
        /// <param name="bufferWriter">An instance of <see cref="IBufferWriter{Byte}" /> used as a destination for writing YAML text into.</param>
        /// <param name="options">Defines the customized behavior of the <see cref="Utf8YamlWriter"/>.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when the instance of <see cref="IBufferWriter{Byte}" /> that is passed in is null.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        ///   The instance of <see cref="Utf8YamlWriter"/> has been disposed.
        /// </exception>
        public void Reset(IBufferWriter<byte> bufferWriter, YamlWriterOptions options)
        {
            Reset(bufferWriter);
            SetOptions(options);
        }

        internal void ResetAllStateForCacheReuse()
        {
            ResetHelper();

            _stream = null;
            _arrayBufferWriter = null;
            _output = null;
        }

        internal void ConfigureForCacheReuse(IBufferWriter<byte> bufferWriter, YamlWriterOptions options)
        {
            Debug.Assert(_output is null && _stream is null && _arrayBufferWriter is null);

            _output = bufferWriter;
            SetOptions(options);
        }

        internal static Utf8YamlWriter CreateEmptyInstanceForCaching() => new Utf8YamlWriter();

        // M5b: the serializer's anchor names (&1, &2, ...), unique within one output, so that a nested write's anchors
        // never re-define an outer one.
        private int _anchorCounter;

        internal string NextAnchorName() => (++_anchorCounter).ToString(Globalization.CultureInfo.InvariantCulture);

        private void ResetHelper()
        {
            _anchorCounter = 0;
            BytesPending = default;
            BytesCommitted = default;
            _memory = default;

            _enclosingContainer = default;
            _tokenType = default;
            _commentAfterNoneOrPropertyName = default;
            _currentDepth = default;

            _bitStack = default;

            _partialStringData = default;
            _partialStringDataLength = default;

            _blockColumn = 0;
            _scalarStyleHint = YamlScalarStyle.None;
            _plainFromSource = false;
            _softScalarStyleHint = false;
            _defaultStyleKey = false;
            _flushedColumn = 0;
            _blockSpecial = 0;
            if (_block is not null)
            {
                // Block style only (M4b): the JSON-compatible style's Reset stays as M4a had it, apart from one store.
                ResetBlockState();
            }
        }

        private void CheckNotDisposed()
        {
            if (_stream is null)
            {
                // The conditions are ordered with stream first as that would be the most common mode
                if (_output is null)
                {
                    ThrowHelper.ThrowObjectDisposedException_Utf8YamlWriter();
                }
            }
        }

        /// <summary>
        /// Commits the YAML text written so far which makes it visible to the output destination.
        /// </summary>
        /// <remarks>
        /// In the case of IBufferWriter, this advances the underlying <see cref="IBufferWriter{Byte}" /> based on what has been written so far.
        /// In the case of Stream, this writes the data to the stream and flushes it.
        /// </remarks>
        /// <exception cref="ObjectDisposedException">
        ///   The instance of <see cref="Utf8YamlWriter"/> has been disposed.
        /// </exception>
        public void Flush()
        {
            CheckNotDisposed();

            KeepColumnAcrossFlush();
            _memory = default;

            if (_stream is not null)
            {
                Debug.Assert(_arrayBufferWriter is not null);
                if (BytesPending != 0)
                {
                    _arrayBufferWriter.Advance(BytesPending);
                    BytesPending = 0;

#if NET
                    _stream.Write(_arrayBufferWriter.WrittenSpan);
#else
                    _stream.Write(_arrayBufferWriter.WrittenMemory);
#endif

                    BytesCommitted += _arrayBufferWriter.WrittenCount;
                    _arrayBufferWriter.Clear();
                }
                _stream.Flush();
            }
            else
            {
                Debug.Assert(_output is not null);
                if (BytesPending != 0)
                {
                    _output.Advance(BytesPending);
                    BytesCommitted += BytesPending;
                    BytesPending = 0;
                }
            }
        }

        /// <summary>
        /// Commits any left over YAML text that has not yet been flushed and releases all resources used by the current instance.
        /// </summary>
        /// <remarks>
        ///   <para>
        ///     In the case of IBufferWriter, this advances the underlying <see cref="IBufferWriter{Byte}" /> based on what has been written so far.
        ///     In the case of Stream, this writes the data to the stream and flushes it.
        ///   </para>
        ///   <para>
        ///     The <see cref="Utf8YamlWriter"/> instance cannot be re-used after disposing.
        ///   </para>
        /// </remarks>
        public void Dispose()
        {
            if (_stream is null)
            {
                // The conditions are ordered with stream first as that would be the most common mode
                if (_output is null)
                {
                    return;
                }
            }

            Flush();
            ResetHelper();

            _stream = null;
            _arrayBufferWriter = null;
            _output = null;
        }

        /// <summary>
        /// Asynchronously commits any left over YAML text that has not yet been flushed and releases all resources used by the current instance.
        /// </summary>
        /// <remarks>
        ///   <para>
        ///     In the case of IBufferWriter, this advances the underlying <see cref="IBufferWriter{Byte}" /> based on what has been written so far.
        ///     In the case of Stream, this writes the data to the stream and flushes it.
        ///   </para>
        ///   <para>
        ///     The <see cref="Utf8YamlWriter"/> instance cannot be re-used after disposing.
        ///   </para>
        /// </remarks>
        public async ValueTask DisposeAsync()
        {
            if (_stream is null)
            {
                // The conditions are ordered with stream first as that would be the most common mode
                if (_output is null)
                {
                    return;
                }
            }

            await FlushAsync().ConfigureAwait(false);
            ResetHelper();

            _stream = null;
            _arrayBufferWriter = null;
            _output = null;
        }

        /// <summary>
        /// Asynchronously commits the YAML text written so far which makes it visible to the output destination.
        /// </summary>
        /// <remarks>
        /// In the case of IBufferWriter, this advances the underlying <see cref="IBufferWriter{Byte}" /> based on what has been written so far.
        /// In the case of Stream, this writes the data to the stream and flushes it asynchronously, while monitoring cancellation requests.
        /// </remarks>
        /// <exception cref="ObjectDisposedException">
        ///   The instance of <see cref="Utf8YamlWriter"/> has been disposed.
        /// </exception>
        public async Task FlushAsync(CancellationToken cancellationToken = default)
        {
            CheckNotDisposed();

            KeepColumnAcrossFlush();
            _memory = default;

            if (_stream is not null)
            {
                Debug.Assert(_arrayBufferWriter is not null);
                if (BytesPending != 0)
                {
                    _arrayBufferWriter.Advance(BytesPending);
                    BytesPending = 0;

                    await _stream.WriteAsync(_arrayBufferWriter.WrittenMemory, cancellationToken).ConfigureAwait(false);

                    BytesCommitted += _arrayBufferWriter.WrittenCount;
                    _arrayBufferWriter.Clear();
                }
                await _stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
            else
            {
                Debug.Assert(_output is not null);
                if (BytesPending != 0)
                {
                    _output.Advance(BytesPending);
                    BytesCommitted += BytesPending;
                    BytesPending = 0;
                }
            }
        }

        /// <summary>
        /// Writes the beginning of a YAML array.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the depth of the YAML has exceeded the maximum depth of 1000
        /// OR if this would result in invalid YAML being written (while validation is enabled).
        /// </exception>
        public void WriteStartArray()
        {
            WriteStart(YamlConstants.OpenBracket);
            _tokenType = YamlTokenType.StartArray;
        }

        /// <summary>
        /// Writes the beginning of a YAML object.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the depth of the YAML has exceeded the maximum depth of 1000
        /// OR if this would result in invalid YAML being written (while validation is enabled).
        /// </exception>
        public void WriteStartObject()
        {
            WriteStart(YamlConstants.OpenBrace);
            _tokenType = YamlTokenType.StartObject;
        }

        private void WriteStart(byte token)
        {
            if (CurrentDepth >= _options.MaxDepth)
            {
                ThrowInvalidOperationException_DepthTooLarge();
            }

            if (_startEndSlow)
            {
                WriteStartSlow(token);
            }
            else
            {
                WriteStartMinimized(token);
            }

            _currentDepth &= YamlConstants.RemoveFlagsBitMask;
            _currentDepth++;
        }

        private void WriteStartMinimized(byte token)
        {
            if (_memory.Length - BytesPending < 2)  // 1 start token, and optionally, 1 list separator
            {
                Grow(2);
            }

            Span<byte> output = _memory.Span;
            if (_currentDepth < 0)
            {
                output[BytesPending++] = YamlConstants.ListSeparator;
            }
            output[BytesPending++] = token;
        }

        private void WriteStartSlow(byte token)
        {
            Debug.Assert(_options.Indented || _style == WriterStyleBlock || !_options.SkipValidation);

            if (_style == WriterStyleJsonMinimized)
            {
                Debug.Assert(!_options.SkipValidation);
                ValidateStart();
                UpdateBitStackOnStart(token);
                WriteStartMinimized(token);
            }
            else if (_style == WriterStyleJsonIndented)
            {
                if (!_options.SkipValidation)
                {
                    ValidateStart();
                    UpdateBitStackOnStart(token);
                }
                WriteStartIndented(token);
            }
            else
            {
                WriteStartBlock(token);
            }
        }

        private void ValidateStart()
        {
            // Note that Start[Array|Object] indicates the start of a value, so the same check can be used.
            if (!CanWriteValue)
            {
                OnValidateStartFailed();
            }
        }

        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void OnValidateStartFailed()
        {
            // Make sure a new object or array is not attempted within an unfinalized string.
            if (IsWritingPartialString)
            {
                ThrowInvalidOperationException(ExceptionResource.CannotWriteWithinString);
            }

            Debug.Assert(!HasPartialStringData);

            if (_enclosingContainer == EnclosingContainerType.Object)
            {
                Debug.Assert(_tokenType != YamlTokenType.PropertyName);
                Debug.Assert(_tokenType != YamlTokenType.None && _tokenType != YamlTokenType.StartArray);
                ThrowInvalidOperationException(ExceptionResource.CannotStartObjectArrayWithoutProperty);
            }
            else
            {
                Debug.Assert(_tokenType != YamlTokenType.PropertyName);
                Debug.Assert(_tokenType != YamlTokenType.StartObject);
                Debug.Assert(CurrentDepth == 0 && _tokenType != YamlTokenType.None);
                ThrowInvalidOperationException(ExceptionResource.CannotStartObjectArrayAfterPrimitiveOrClose);
            }
        }

        private void WriteStartIndented(byte token)
        {
            int indent = Indentation;
            Debug.Assert(indent <= _indentLength * _options.MaxDepth);

            int minRequired = indent + 1;   // 1 start token
            int maxRequired = minRequired + 3; // Optionally, 1 list separator and 1-2 bytes for new line

            if (_memory.Length - BytesPending < maxRequired)
            {
                Grow(maxRequired);
            }

            Span<byte> output = _memory.Span;

            if (_currentDepth < 0)
            {
                output[BytesPending++] = YamlConstants.ListSeparator;
            }

            if (_tokenType is not YamlTokenType.PropertyName and not YamlTokenType.None || _commentAfterNoneOrPropertyName)
            {
                WriteNewLine(output);
                WriteIndentation(output.Slice(BytesPending), indent);
                BytesPending += indent;
            }

            output[BytesPending++] = token;
        }

        /// <summary>
        /// Writes the beginning of a YAML array with a pre-encoded property name as the key.
        /// </summary>
        /// <param name="propertyName">The YAML-encoded name of the property to write.</param>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the depth of the YAML has exceeded the maximum depth of 1000
        /// OR if this would result in invalid YAML being written (while validation is enabled).
        /// </exception>
        public void WriteStartArray(YamlEncodedText propertyName)
        {
            WriteStartHelper(propertyName.EncodedUtf8Bytes, YamlConstants.OpenBracket);
            _tokenType = YamlTokenType.StartArray;
        }

        /// <summary>
        /// Writes the beginning of a YAML object with a pre-encoded property name as the key.
        /// </summary>
        /// <param name="propertyName">The YAML-encoded name of the property to write.</param>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the depth of the YAML has exceeded the maximum depth of 1000
        /// OR if this would result in invalid YAML being written (while validation is enabled).
        /// </exception>
        public void WriteStartObject(YamlEncodedText propertyName)
        {
            WriteStartHelper(propertyName.EncodedUtf8Bytes, YamlConstants.OpenBrace);
            _tokenType = YamlTokenType.StartObject;
        }

        private void WriteStartHelper(ReadOnlySpan<byte> utf8PropertyName, byte token)
        {
            Debug.Assert(utf8PropertyName.Length <= YamlConstants.MaxUnescapedTokenSize);

            ValidateDepth();

            WriteStartByOptions(utf8PropertyName, token);

            _currentDepth &= YamlConstants.RemoveFlagsBitMask;
            _currentDepth++;
        }

        /// <summary>
        /// Writes the beginning of a YAML array with a property name as the key.
        /// </summary>
        /// <param name="utf8PropertyName">The UTF-8 encoded property name of the YAML array to be written.</param>
        /// <remarks>
        /// The property name is escaped before writing.
        /// </remarks>
        /// <exception cref="ArgumentException">
        /// Thrown when the specified property name is too large.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the depth of the YAML has exceeded the maximum depth of 1000
        /// OR if this would result in invalid YAML being written (while validation is enabled).
        /// </exception>
        public void WriteStartArray(ReadOnlySpan<byte> utf8PropertyName)
        {
            ValidatePropertyNameAndDepth(utf8PropertyName);

            WriteStartEscape(utf8PropertyName, YamlConstants.OpenBracket);

            _currentDepth &= YamlConstants.RemoveFlagsBitMask;
            _currentDepth++;
            _tokenType = YamlTokenType.StartArray;
        }

        /// <summary>
        /// Writes the beginning of a YAML object with a property name as the key.
        /// </summary>
        /// <param name="utf8PropertyName">The UTF-8 encoded property name of the YAML object to be written.</param>
        /// <remarks>
        /// The property name is escaped before writing.
        /// </remarks>
        /// <exception cref="ArgumentException">
        /// Thrown when the specified property name is too large.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the depth of the YAML has exceeded the maximum depth of 1000
        /// OR if this would result in invalid YAML being written (while validation is enabled).
        /// </exception>
        public void WriteStartObject(ReadOnlySpan<byte> utf8PropertyName)
        {
            ValidatePropertyNameAndDepth(utf8PropertyName);

            WriteStartEscape(utf8PropertyName, YamlConstants.OpenBrace);

            _currentDepth &= YamlConstants.RemoveFlagsBitMask;
            _currentDepth++;
            _tokenType = YamlTokenType.StartObject;
        }

        private void WriteStartEscape(ReadOnlySpan<byte> utf8PropertyName, byte token)
        {
            int propertyIdx = YamlWriterHelper.NeedsEscaping(utf8PropertyName, _options.Encoder);

            Debug.Assert(propertyIdx >= -1 && propertyIdx < utf8PropertyName.Length);

            if (propertyIdx != -1)
            {
                WriteStartEscapeProperty(utf8PropertyName, token, propertyIdx);
            }
            else
            {
                WriteStartByOptions(utf8PropertyName, token);
            }
        }

        private void WriteStartByOptions(ReadOnlySpan<byte> utf8PropertyName, byte token)
        {
            ValidateWritingProperty(token);

            if (_style == WriterStyleJsonMinimized)
            {
                WritePropertyNameMinimized(utf8PropertyName, token);
            }
            else if (_style == WriterStyleJsonIndented)
            {
                WritePropertyNameIndented(utf8PropertyName, token);
            }
            else
            {
                WriteStartBlock(utf8PropertyName, token);
            }
        }

        private unsafe void WriteStartEscapeProperty(ReadOnlySpan<byte> utf8PropertyName, byte token, int firstEscapeIndexProp)
        {
            Debug.Assert(int.MaxValue / YamlConstants.MaxExpansionFactorWhileEscaping >= utf8PropertyName.Length);
            Debug.Assert(firstEscapeIndexProp >= 0 && firstEscapeIndexProp < utf8PropertyName.Length);

            byte[]? propertyArray = null;

            int length = YamlWriterHelper.GetMaxEscapedLength(utf8PropertyName.Length, firstEscapeIndexProp);

            Span<byte> escapedPropertyName = length <= YamlConstants.StackallocByteThreshold ?
                stackalloc byte[YamlConstants.StackallocByteThreshold] :
                (propertyArray = ArrayPool<byte>.Shared.Rent(length));

            YamlWriterHelper.EscapeString(utf8PropertyName, escapedPropertyName, firstEscapeIndexProp, _options.Encoder, out int written);

            WriteStartByOptions(escapedPropertyName.Slice(0, written), token);

            if (propertyArray is not null)
            {
                ArrayPool<byte>.Shared.Return(propertyArray);
            }
        }

        /// <summary>
        /// Writes the beginning of a YAML array with a property name as the key.
        /// </summary>
        /// <param name="propertyName">The name of the property to write.</param>
        /// <remarks>
        /// The property name is escaped before writing.
        /// </remarks>
        /// <exception cref="ArgumentException">
        /// Thrown when the specified property name is too large.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// The <paramref name="propertyName"/> parameter is <see langword="null"/>.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the depth of the YAML has exceeded the maximum depth of 1000
        /// OR if this would result in invalid YAML being written (while validation is enabled).
        /// </exception>
        public void WriteStartArray(string propertyName)
        {
            ArgumentNullException.ThrowIfNull(propertyName);
            WriteStartArray(propertyName.AsSpan());
        }

        /// <summary>
        /// Writes the beginning of a YAML object with a property name as the key.
        /// </summary>
        /// <param name="propertyName">The name of the property to write.</param>
        /// <remarks>
        /// The property name is escaped before writing.
        /// </remarks>
        /// <exception cref="ArgumentException">
        /// Thrown when the specified property name is too large.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// The <paramref name="propertyName"/> parameter is <see langword="null"/>.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the depth of the YAML has exceeded the maximum depth of 1000
        /// OR if this would result in invalid YAML being written (while validation is enabled).
        /// </exception>
        public void WriteStartObject(string propertyName)
        {
            ArgumentNullException.ThrowIfNull(propertyName);
            WriteStartObject(propertyName.AsSpan());
        }

        /// <summary>
        /// Writes the beginning of a YAML array with a property name as the key.
        /// </summary>
        /// <param name="propertyName">The name of the property to write.</param>
        /// <remarks>
        /// The property name is escaped before writing.
        /// </remarks>
        /// <exception cref="ArgumentException">
        /// Thrown when the specified property name is too large.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the depth of the YAML has exceeded the maximum depth of 1000
        /// OR if this would result in invalid YAML being written (while validation is enabled).
        /// </exception>
        public void WriteStartArray(ReadOnlySpan<char> propertyName)
        {
            ValidatePropertyNameAndDepth(propertyName);

            WriteStartEscape(propertyName, YamlConstants.OpenBracket);

            _currentDepth &= YamlConstants.RemoveFlagsBitMask;
            _currentDepth++;
            _tokenType = YamlTokenType.StartArray;
        }

        /// <summary>
        /// Writes the beginning of a YAML object with a property name as the key.
        /// </summary>
        /// <param name="propertyName">The name of the property to write.</param>
        /// <remarks>
        /// The property name is escaped before writing.
        /// </remarks>
        /// <exception cref="ArgumentException">
        /// Thrown when the specified property name is too large.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the depth of the YAML has exceeded the maximum depth of 1000
        /// OR if this would result in invalid YAML being written (while validation is enabled).
        /// </exception>
        public void WriteStartObject(ReadOnlySpan<char> propertyName)
        {
            ValidatePropertyNameAndDepth(propertyName);

            WriteStartEscape(propertyName, YamlConstants.OpenBrace);

            _currentDepth &= YamlConstants.RemoveFlagsBitMask;
            _currentDepth++;
            _tokenType = YamlTokenType.StartObject;
        }

        private void WriteStartEscape(ReadOnlySpan<char> propertyName, byte token)
        {
            int propertyIdx = YamlWriterHelper.NeedsEscaping(propertyName, _options.Encoder);

            Debug.Assert(propertyIdx >= -1 && propertyIdx < propertyName.Length);

            if (propertyIdx != -1)
            {
                WriteStartEscapeProperty(propertyName, token, propertyIdx);
            }
            else
            {
                WriteStartByOptions(propertyName, token);
            }
        }

        private void WriteStartByOptions(ReadOnlySpan<char> propertyName, byte token)
        {
            ValidateWritingProperty(token);

            if (_style == WriterStyleJsonMinimized)
            {
                WritePropertyNameMinimized(propertyName, token);
            }
            else if (_style == WriterStyleJsonIndented)
            {
                WritePropertyNameIndented(propertyName, token);
            }
            else
            {
                WriteStartBlock(propertyName, token);
            }
        }

        private unsafe void WriteStartEscapeProperty(ReadOnlySpan<char> propertyName, byte token, int firstEscapeIndexProp)
        {
            Debug.Assert(int.MaxValue / YamlConstants.MaxExpansionFactorWhileEscaping >= propertyName.Length);
            Debug.Assert(firstEscapeIndexProp >= 0 && firstEscapeIndexProp < propertyName.Length);

            char[]? propertyArray = null;

            int length = YamlWriterHelper.GetMaxEscapedLength(propertyName.Length, firstEscapeIndexProp);

            Span<char> escapedPropertyName = length <= YamlConstants.StackallocCharThreshold ?
                stackalloc char[YamlConstants.StackallocCharThreshold] :
                (propertyArray = ArrayPool<char>.Shared.Rent(length));

            YamlWriterHelper.EscapeString(propertyName, escapedPropertyName, firstEscapeIndexProp, _options.Encoder, out int written);

            WriteStartByOptions(escapedPropertyName.Slice(0, written), token);

            if (propertyArray is not null)
            {
                ArrayPool<char>.Shared.Return(propertyArray);
            }
        }

        /// <summary>
        /// Writes the end of a YAML array.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown if this would result in invalid YAML being written (while validation is enabled).
        /// </exception>
        public void WriteEndArray()
        {
            WriteEnd(YamlConstants.CloseBracket);
            _tokenType = YamlTokenType.EndArray;
        }

        /// <summary>
        /// Writes the end of a YAML object.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown if this would result in invalid YAML being written (while validation is enabled).
        /// </exception>
        public void WriteEndObject()
        {
            WriteEnd(YamlConstants.CloseBrace);
            _tokenType = YamlTokenType.EndObject;
        }

        private void WriteEnd(byte token)
        {
            if (_startEndSlow)
            {
                WriteEndSlow(token);
            }
            else
            {
                WriteEndMinimized(token);
            }

            SetFlagToAddListSeparatorBeforeNextItem();
            // Necessary if WriteEndX is called without a corresponding WriteStartX first.
            if (CurrentDepth != 0)
            {
                _currentDepth--;
            }
        }

        private void WriteEndMinimized(byte token)
        {
            if (_memory.Length - BytesPending < 1) // 1 end token
            {
                Grow(1);
            }

            Span<byte> output = _memory.Span;
            output[BytesPending++] = token;
        }

        private void WriteEndSlow(byte token)
        {
            Debug.Assert(_options.Indented || _style == WriterStyleBlock || !_options.SkipValidation);

            if (_style == WriterStyleJsonMinimized)
            {
                Debug.Assert(!_options.SkipValidation);
                ValidateEnd(token);
                WriteEndMinimized(token);
            }
            else if (_style == WriterStyleJsonIndented)
            {
                if (!_options.SkipValidation)
                {
                    ValidateEnd(token);
                }
                WriteEndIndented(token);
            }
            else
            {
                WriteEndBlock(token);
            }
        }

        // Performance degrades significantly in some scenarios when inlining is allowed.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void ValidateEnd(byte token)
        {
            if (_tokenType == YamlTokenType.PropertyName)
                ThrowInvalidOperationException_MismatchedObjectArray(token);

            if (token == YamlConstants.CloseBracket)
            {
                if (_enclosingContainer != EnclosingContainerType.Array)
                {
                    ThrowInvalidOperationException_MismatchedObjectArray(token);
                }
            }
            else
            {
                Debug.Assert(token == YamlConstants.CloseBrace);

                if (_enclosingContainer != EnclosingContainerType.Object)
                {
                    ThrowInvalidOperationException_MismatchedObjectArray(token);
                }
            }

            EnclosingContainerType container = _bitStack.Pop() ? EnclosingContainerType.Object : EnclosingContainerType.Array;
            _enclosingContainer = _bitStack.CurrentDepth == 0 ? EnclosingContainerType.None : container;
        }

        private void WriteEndIndented(byte token)
        {
            // Do not format/indent empty JSON object/array.
            if (_tokenType == YamlTokenType.StartObject || _tokenType == YamlTokenType.StartArray)
            {
                WriteEndMinimized(token);
            }
            else
            {
                int indent = Indentation;

                // Necessary if WriteEndX is called without a corresponding WriteStartX first.
                if (indent != 0)
                {
                    // The end token should be at an outer indent and since we haven't updated
                    // current depth yet, explicitly subtract here.
                    indent -= _indentLength;
                }

                Debug.Assert(indent <= _indentLength * _options.MaxDepth);
                Debug.Assert(_options.SkipValidation || _tokenType != YamlTokenType.None);

                int maxRequired = indent + 3; // 1 end token, 1-2 bytes for new line

                if (_memory.Length - BytesPending < maxRequired)
                {
                    Grow(maxRequired);
                }

                Span<byte> output = _memory.Span;

                WriteNewLine(output);

                WriteIndentation(output.Slice(BytesPending), indent);
                BytesPending += indent;

                output[BytesPending++] = token;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteNewLine(Span<byte> output)
        {
            // Write '\r\n' OR '\n', depending on the configured new line string
            Debug.Assert(_newLineLength is 1 or 2, "Invalid new line length.");
            if (_newLineLength == 2)
            {
                output[BytesPending++] = YamlConstants.CarriageReturn;
            }
            output[BytesPending++] = YamlConstants.LineFeed;
        }

        private void WriteIndentation(Span<byte> buffer, int indent)
        {
            YamlWriterHelper.WriteIndentation(buffer, indent, _indentByte);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UpdateBitStackOnStart(byte token)
        {
            if (token == YamlConstants.OpenBracket)
            {
                _bitStack.PushFalse();
                _enclosingContainer = EnclosingContainerType.Array;
            }
            else
            {
                Debug.Assert(token == YamlConstants.OpenBrace);
                _bitStack.PushTrue();
                _enclosingContainer = EnclosingContainerType.Object;
            }
        }

        private void Grow(int requiredSize)
        {
            Debug.Assert(requiredSize > 0);

            if (_memory.Length == 0)
            {
                FirstCallToGetMemory(requiredSize);
                return;
            }

            int sizeHint = Math.Max(DefaultGrowthSize, requiredSize);

            // Block style can ask for room before it has written anything: a root collection's start writes nothing.
            Debug.Assert(BytesPending != 0 || _style == WriterStyleBlock);

            if (_stream is not null)
            {
                Debug.Assert(_arrayBufferWriter is not null);

                int needed = BytesPending + sizeHint;
                YamlHelpers.ValidateInt32MaxArrayLength((uint)needed);

                _memory = _arrayBufferWriter.GetMemory(needed);

                Debug.Assert(_memory.Length >= sizeHint);
            }
            else
            {
                Debug.Assert(_output is not null);

                KeepColumnAcrossFlush();
                _output.Advance(BytesPending);
                BytesCommitted += BytesPending;
                BytesPending = 0;

                _memory = _output.GetMemory(sizeHint);

                if (_memory.Length < sizeHint)
                {
                    ThrowHelper.ThrowInvalidOperationException_NeedLargerSpan();
                }
            }
        }

        private void FirstCallToGetMemory(int requiredSize)
        {
            Debug.Assert(_memory.Length == 0);
            Debug.Assert(BytesPending == 0);

            int sizeHint = Math.Max(InitialGrowthSize, requiredSize);

            if (_stream is not null)
            {
                Debug.Assert(_arrayBufferWriter is not null);
                _memory = _arrayBufferWriter.GetMemory(sizeHint);
                Debug.Assert(_memory.Length >= sizeHint);
            }
            else
            {
                Debug.Assert(_output is not null);
                _memory = _output.GetMemory(sizeHint);

                if (_memory.Length < sizeHint)
                {
                    ThrowHelper.ThrowInvalidOperationException_NeedLargerSpan();
                }
            }
        }

        private void SetFlagToAddListSeparatorBeforeNextItem()
        {
            _currentDepth |= 1 << 31;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        [DoesNotReturn]
        private void ThrowInvalidOperationException(ExceptionResource resource)
            => ThrowHelper.ThrowInvalidOperationException(resource, currentDepth: default, maxDepth: _options.MaxDepth, token: default, _tokenType);

        [MethodImpl(MethodImplOptions.NoInlining)]
        [DoesNotReturn]
        private void ThrowInvalidOperationException_MismatchedObjectArray(byte token)
            => ThrowHelper.ThrowInvalidOperationException(ExceptionResource.MismatchedObjectArray, currentDepth: default, maxDepth: _options.MaxDepth, token, _tokenType);

        [MethodImpl(MethodImplOptions.NoInlining)]
        [DoesNotReturn]
        private void ThrowInvalidOperationException_DepthTooLarge()
            => ThrowHelper.ThrowInvalidOperationException(ExceptionResource.DepthTooLarge, _currentDepth, _options.MaxDepth, token: default, tokenType: default);

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string DebuggerDisplay => $"BytesCommitted = {BytesCommitted} BytesPending = {BytesPending} CurrentDepth = {CurrentDepth}";

        /// <summary>
        /// Indicates whether the writer is currently writing a partial string value.
        /// </summary>
        private bool IsWritingPartialString => _enclosingContainer >= EnclosingContainerType.Utf8StringSequence;

        /// <summary>
        /// The type of container that is enclosing the current position. The underlying values have been chosen
        /// to allow <see cref="CanWriteValue"/> to be done using bitwise operations and must be kept in sync with <see cref="YamlTokenType"/>.
        /// </summary>
        internal enum EnclosingContainerType : byte
        {
            /// <summary>
            /// Root level. The choice of <see cref="YamlTokenType.None"/> allows fast validation by equality comparison when writing values
            /// since a value can be written at the root level only if there was no previous token.
            /// </summary>
            None = YamlTokenType.None,

            /// <summary>
            /// YAML object. The choice of <see cref="YamlTokenType.PropertyName"/> allows fast validation by equality comparison when writing values
            /// since a value can be written inside a YAML object only if the previous token is a property name.
            /// </summary>
            Object = YamlTokenType.PropertyName,

            /// <summary>
            /// YAML array. Chosen so that its lower nibble is 0 to ensure it does not conflict with <see cref="YamlTokenType"/> numeric values that currently are less than 16.
            /// </summary>
            Array = 0x10,

            /// <summary>
            /// Partial UTF-8 string. This is a container if viewed as an array of "utf-8 string segment"-typed values. This array can only be one level deep
            /// so <see cref="_bitStack"/> does not need to store its state.
            /// <see cref="IsWritingPartialString"/> relies on the value of the partial string members being the largest values of this enum.
            /// </summary>
            Utf8StringSequence = 0x20,

            /// <summary>
            /// Partial UTF-16 string. This is a container if viewed as an array of "utf-16 string segment"-typed values. This array can only be one level deep
            /// so <see cref="_bitStack"/> does not need to store its state.
            /// <see cref="IsWritingPartialString"/> relies on the value of the partial string members being the largest values of this enum.
            /// </summary>
            Utf16StringSequence = 0x30,

            /// <summary>
            /// Partial Base64 string. This is a container if viewed as an array of "base64 string segment"-typed values. This array can only be one level deep
            /// so <see cref="_bitStack"/> does not need to store its state.
            /// <see cref="IsWritingPartialString"/> relies on the value of the partial string members being the largest values of this enum.
            /// </summary>
            Base64StringSequence = 0x40,
        }
    }
}
