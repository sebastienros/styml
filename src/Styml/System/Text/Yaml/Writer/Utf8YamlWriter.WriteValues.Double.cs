// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers;
using System.Buffers.Text;
using System.Diagnostics;
using System.Globalization;

namespace System.Text.Yaml
{
    public sealed partial class Utf8YamlWriter
    {
        /// <summary>
        /// Writes the <see cref="double"/> value (as a YAML number) as an element of a YAML array.
        /// </summary>
        /// <param name="value">The value to write.</param>
        /// <exception cref="InvalidOperationException">
        /// Thrown if this would result in invalid YAML being written (while validation is enabled).
        /// </exception>
        /// <remarks>
        /// Writes the <see cref="double"/> using the default <see cref="StandardFormat"/> on .NET Core 3 or higher
        /// and 'G17' on any other framework.
        /// </remarks>
        public void WriteNumberValue(double value)
        {
            ValidateFiniteUnlessBlock(value);

            if (!_options.SkipValidation)
            {
                ValidateWritingValue();
            }

            if (_style == WriterStyleJsonMinimized)
            {
                WriteNumberValueMinimized(value);
            }
            else if (_style == WriterStyleJsonIndented)
            {
                WriteNumberValueIndented(value);
            }
            else
            {
                WriteNumberValueBlock(value);
            }

            SetFlagToAddListSeparatorBeforeNextItem();
            _tokenType = YamlTokenType.Number;
        }

        private void WriteNumberValueMinimized(double value)
        {
            int maxRequired = YamlConstants.MaximumFormatDoubleLength + 1; // Optionally, 1 list separator

            if (_memory.Length - BytesPending < maxRequired)
            {
                Grow(maxRequired);
            }

            Span<byte> output = _memory.Span;

            if (_currentDepth < 0)
            {
                output[BytesPending++] = YamlConstants.ListSeparator;
            }

            bool result = TryFormatDouble(value, output.Slice(BytesPending), out int bytesWritten);
            Debug.Assert(result);
            BytesPending += bytesWritten;
        }

        private void WriteNumberValueIndented(double value)
        {
            int indent = Indentation;
            Debug.Assert(indent <= _indentLength * _options.MaxDepth);

            int maxRequired = indent + YamlConstants.MaximumFormatDoubleLength + 1 + _newLineLength; // Optionally, 1 list separator and 1-2 bytes for new line

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

            bool result = TryFormatDouble(value, output.Slice(BytesPending), out int bytesWritten);
            Debug.Assert(result);
            BytesPending += bytesWritten;
        }

        private static bool TryFormatDouble(double value, Span<byte> destination, out int bytesWritten)
        {
            // Frameworks that are not .NET Core 3.0 or higher do not produce roundtrippable strings by
            // default. Further, the Utf8Formatter on older frameworks does not support taking a precision
            // specifier for 'G' nor does it represent other formats such as 'R'. As such, we duplicate
            // the .NET Core 3.0 logic of forwarding to the UTF16 formatter and transcoding it back to UTF8,
            // with some additional changes to remove dependencies on Span APIs which don't exist downlevel.

#if NET
            return Utf8Formatter.TryFormat(value, destination, out bytesWritten);
#else
            string utf16Text = value.ToString(YamlConstants.DoubleFormatString, CultureInfo.InvariantCulture);

            // Copy the value to the destination, if it's large enough.

            if (utf16Text.Length > destination.Length)
            {
                bytesWritten = 0;
                return false;
            }

            try
            {
                byte[] bytes = Encoding.UTF8.GetBytes(utf16Text);

                if (bytes.Length > destination.Length)
                {
                    bytesWritten = 0;
                    return false;
                }

                bytes.CopyTo(destination);
                bytesWritten = bytes.Length;

                return true;
            }
            catch
            {
                bytesWritten = 0;
                return false;
            }
#endif
        }

        internal unsafe void WriteNumberValueAsString(double value)
        {
            Span<byte> utf8Number = stackalloc byte[YamlConstants.MaximumFormatDoubleLength];
            bool result = TryFormatDouble(value, utf8Number, out int bytesWritten);
            Debug.Assert(result);
            WriteNumberValueAsStringUnescaped(utf8Number.Slice(0, bytesWritten));
        }

        internal void WriteFloatingPointConstant(double value)
        {
            if (_style == WriterStyleBlock)
            {
                // Decision 8: .nan, .inf and -.inf, not the JSON-compatible strings.
                WriteNumberValue(value);
                return;
            }

            if (double.IsNaN(value))
            {
                WriteNumberValueAsStringUnescaped(YamlConstants.NaNValue);
            }
            else if (double.IsPositiveInfinity(value))
            {
                WriteNumberValueAsStringUnescaped(YamlConstants.PositiveInfinityValue);
            }
            else if (double.IsNegativeInfinity(value))
            {
                WriteNumberValueAsStringUnescaped(YamlConstants.NegativeInfinityValue);
            }
            else
            {
                WriteNumberValue(value);
            }
        }
    }
}
