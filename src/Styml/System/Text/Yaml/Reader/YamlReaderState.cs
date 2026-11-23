// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Text.Yaml
{
    /// <summary>
    /// Defines an opaque type that holds and saves all the relevant state information which must be provided
    /// to the <see cref="Utf8YamlReader"/> to continue reading after processing incomplete data.
    /// This type is required to support reentrancy when reading incomplete data, and to continue
    /// reading once more data is available. Unlike the <see cref="Utf8YamlReader"/>, which is a ref struct,
    /// this type can survive across async/await boundaries and hence this type is required to provide
    /// support for reading in more data asynchronously before continuing with a new instance of the <see cref="Utf8YamlReader"/>.
    /// </summary>
    public readonly struct YamlReaderState
    {
        internal readonly long _lineNumber;
        internal readonly long _bytePositionInLine;
        internal readonly bool _inObject;
        internal readonly bool _valueIsEscaped;
        internal readonly YamlTokenType _tokenType;
        internal readonly YamlTokenType _previousTokenType;
        internal readonly YamlReaderOptions _readerOptions;
        internal readonly BitStack _bitStack;
        internal readonly YamlParseState _yaml;

        /// <summary>
        /// Constructs a new <see cref="YamlReaderState"/> instance.
        /// </summary>
        /// <param name="options">Defines the customized behavior of the <see cref="Utf8YamlReader"/>
        /// that is different from the defaults (for example how to handle comments or maximum depth allowed when reading).
        /// By default, the <see cref="Utf8YamlReader"/> follows the YAML 1.2 specification and reads up to a maximum depth of 64.</param>
        /// <remarks>
        /// An instance of this state must be passed to the <see cref="Utf8YamlReader"/> ctor with the YAML data.
        /// Unlike the <see cref="Utf8YamlReader"/>, which is a ref struct, the state can survive
        /// across async/await boundaries and hence this type is required to provide support for reading
        /// in more data asynchronously before continuing with a new instance of the <see cref="Utf8YamlReader"/>.
        /// </remarks>
        public YamlReaderState(YamlReaderOptions options = default)
        {
            _lineNumber = default;
            _bytePositionInLine = default;
            _inObject = default;
            _valueIsEscaped = default;
            _tokenType = default;
            _previousTokenType = default;
            _readerOptions = options;

            // Only allocate if the user reads a JSON payload beyond the depth that the _allocationFreeContainer can handle.
            // This way we avoid allocations in the common, default cases, and allocate lazily.
            _bitStack = default;
            _yaml = default;
        }

        internal YamlReaderState(
            long lineNumber,
            long bytePositionInLine,
            bool inObject,
            bool valueIsEscaped,
            YamlTokenType tokenType,
            YamlTokenType previousTokenType,
            YamlReaderOptions readerOptions,
            BitStack bitStack,
            YamlParseState yaml = default)
        {
            _lineNumber = lineNumber;
            _bytePositionInLine = bytePositionInLine;
            _inObject = inObject;
            _valueIsEscaped = valueIsEscaped;
            _tokenType = tokenType;
            _previousTokenType = previousTokenType;
            _readerOptions = readerOptions;
            _bitStack = bitStack;
            _yaml = yaml;
        }

        /// <summary>
        /// Gets the custom behavior when reading YAML using
        /// the <see cref="Utf8YamlReader"/> that may deviate from strict adherence
        /// to the YAML specification, which is the default behavior.
        /// </summary>
        public YamlReaderOptions Options => _readerOptions;
    }
}
