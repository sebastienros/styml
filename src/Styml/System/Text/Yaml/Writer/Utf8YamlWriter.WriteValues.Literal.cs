// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;

namespace System.Text.Yaml
{
    public sealed partial class Utf8YamlWriter
    {
        /// <summary>
        /// Writes the YAML literal "null" as an element of a YAML array.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown if this would result in invalid YAML being written (while validation is enabled).
        /// </exception>
        public void WriteNullValue()
        {
            WriteLiteralByOptions(YamlConstants.NullValue);

            SetFlagToAddListSeparatorBeforeNextItem();
            _tokenType = YamlTokenType.Null;
        }

        // The DOM: a value that was empty in the source (key:, key: , - , key: &a), written back with nothing after its
        // indicator or its properties, and its blank when it had one. At the root, in JSON-compatible style, with a hint, null.
        internal void WriteEmptyValue(bool keepBlank)
        {
            if (_style != WriterStyleBlock || _currentDepth == 0 || _scalarStyleHint != YamlScalarStyle.None)
            {
                WriteNullValue();
                return;
            }

            if (!_options.SkipValidation)
            {
                ValidateWritingValue();
            }

            Span<byte> output = BeginBlockValue(scalar: true, 0);
            if (!keepBlank && BytesPending > 0 && output[BytesPending - 1] == YamlConstants.Space)
            {
                BytesPending--;
            }

            EndBlockScalar(YamlTokenType.Null);
            SetFlagToAddListSeparatorBeforeNextItem();
            _tokenType = YamlTokenType.Null;
        }

        /// <summary>
        /// Writes the <see cref="bool"/> value (as a YAML literal "true" or "false") as an element of a YAML array.
        /// </summary>
        /// <param name="value">The value write.</param>
        /// <exception cref="InvalidOperationException">
        /// Thrown if this would result in invalid YAML being written (while validation is enabled).
        /// </exception>
        public void WriteBooleanValue(bool value)
        {
            if (value)
            {
                WriteLiteralByOptions(YamlConstants.TrueValue);
                _tokenType = YamlTokenType.True;
            }
            else
            {
                WriteLiteralByOptions(YamlConstants.FalseValue);
                _tokenType = YamlTokenType.False;
            }

            SetFlagToAddListSeparatorBeforeNextItem();
        }

        private void WriteLiteralByOptions(ReadOnlySpan<byte> utf8Value)
        {
            if (!_options.SkipValidation)
            {
                ValidateWritingValue();
            }

            if (_style == WriterStyleJsonMinimized)
            {
                WriteLiteralMinimized(utf8Value);
            }
            else if (_style == WriterStyleJsonIndented)
            {
                WriteLiteralIndented(utf8Value);
            }
            else
            {
                WriteLiteralValueBlock(utf8Value);
            }
        }

        private void WriteLiteralMinimized(ReadOnlySpan<byte> utf8Value)
        {
            Debug.Assert(utf8Value.Length <= 5);

            int maxRequired = utf8Value.Length + 1; // Optionally, 1 list separator

            if (_memory.Length - BytesPending < maxRequired)
            {
                Grow(maxRequired);
            }

            Span<byte> output = _memory.Span;

            if (_currentDepth < 0)
            {
                output[BytesPending++] = YamlConstants.ListSeparator;
            }

            utf8Value.CopyTo(output.Slice(BytesPending));
            BytesPending += utf8Value.Length;
        }

        private void WriteLiteralIndented(ReadOnlySpan<byte> utf8Value)
        {
            int indent = Indentation;
            Debug.Assert(indent <= _indentLength * _options.MaxDepth);
            Debug.Assert(utf8Value.Length <= 5);

            int maxRequired = indent + utf8Value.Length + 1 + _newLineLength; // Optionally, 1 list separator and 1-2 bytes for new line

            if (_memory.Length - BytesPending < maxRequired)
            {
                Grow(maxRequired);
            }

            Span<byte> output = _memory.Span;

            if (_currentDepth < 0)
            {
                output[BytesPending++] = YamlConstants.ListSeparator;
            }

            if (_tokenType != YamlTokenType.PropertyName)
            {
                if (_tokenType != YamlTokenType.None)
                {
                    WriteNewLine(output);
                }
                WriteIndentation(output.Slice(BytesPending), indent);
                BytesPending += indent;
            }

            utf8Value.CopyTo(output.Slice(BytesPending));
            BytesPending += utf8Value.Length;
        }
    }
}
