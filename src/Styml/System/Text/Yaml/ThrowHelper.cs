// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using static System.Text.Yaml.Utf8YamlWriter;

namespace System.Text.Yaml
{
    internal static partial class ThrowHelper
    {
        // If the exception source is this value, the serializer will re-throw as YamlException.
        public const string ExceptionSourceValueToRethrowAsYamlException = "System.Text.Yaml.Rethrowable";

        [DoesNotReturn]
        public static void ThrowArgumentOutOfRangeException_NewLine(string parameterName)
        {
            throw GetArgumentOutOfRangeException(parameterName, SR.InvalidNewLine);
        }

        [DoesNotReturn]
        public static void ThrowArgumentOutOfRangeException_IndentCharacter(string parameterName)
        {
            throw GetArgumentOutOfRangeException(parameterName, SR.InvalidIndentCharacter);
        }

        [DoesNotReturn]
        public static void ThrowArgumentOutOfRangeException_IndentSize(string parameterName, int minimumSize, int maximumSize)
        {
            throw GetArgumentOutOfRangeException(parameterName, SR.Format(SR.InvalidIndentSize, minimumSize, maximumSize));
        }

        [DoesNotReturn]
        public static void ThrowArgumentOutOfRangeException_MaxDepthMustBePositive(string parameterName)
        {
            throw GetArgumentOutOfRangeException(parameterName, SR.MaxDepthMustBePositive);
        }

        [DoesNotReturn]
        public static void ThrowArgumentOutOfRangeException_YamlNumberExponentTooLarge(string parameterName)
        {
            throw GetArgumentOutOfRangeException(parameterName, SR.YamlNumberExponentTooLarge);
        }

        private static ArgumentOutOfRangeException GetArgumentOutOfRangeException(string parameterName, string message)
        {
            return new ArgumentOutOfRangeException(parameterName, message);
        }

        [DoesNotReturn]
        public static void ThrowArgumentOutOfRangeException_CommentEnumMustBeInRange(string parameterName)
        {
            throw GetArgumentOutOfRangeException(parameterName, SR.CommentHandlingMustBeValid);
        }

        [DoesNotReturn]
        public static void ThrowArgumentOutOfRangeException_ArrayIndexNegative(string paramName)
        {
            throw new ArgumentOutOfRangeException(paramName, SR.ArrayIndexNegative);
        }

        [DoesNotReturn]
        public static void ThrowArgumentOutOfRangeException_YamlConverterFactory_TypeNotSupported(Type typeToConvert)
        {
            throw new ArgumentOutOfRangeException(nameof(typeToConvert), SR.Format(SR.SerializerConverterFactoryInvalidArgument, typeToConvert.FullName));
        }

        [DoesNotReturn]
        public static void ThrowArgumentOutOfRangeException_NeedNonNegNum(string paramName)
        {
            throw new ArgumentOutOfRangeException(paramName, SR.ArgumentOutOfRange_Generic_MustBeNonNegative);
        }

        [DoesNotReturn]
        public static void ThrowArgumentException_InvalidOffLen()
        {
            throw new ArgumentException(SR.Argument_InvalidOffLen);
        }

        [DoesNotReturn]
        public static void ThrowArgumentException_ArrayTooSmall(string paramName)
        {
            throw new ArgumentException(SR.ArrayTooSmall, paramName);
        }

        private static ArgumentException GetArgumentException(string message)
        {
            return new ArgumentException(message);
        }

        [DoesNotReturn]
        public static void ThrowArgumentException(string message)
        {
            throw GetArgumentException(message);
        }

        public static InvalidOperationException GetInvalidOperationException_CallFlushFirst(int _buffered)
        {
            return GetInvalidOperationException(SR.Format(SR.CallFlushToAvoidDataLoss, _buffered));
        }

        [DoesNotReturn]
        public static void ThrowArgumentException_DestinationTooShort()
        {
            throw GetArgumentException(SR.DestinationTooShort);
        }

        // Block style.
        [DoesNotReturn]
        public static void ThrowArgumentOutOfRangeException_MaxLineWidthMustNotBeNegative(string paramName)
        {
            throw new ArgumentOutOfRangeException(paramName, SR.MaxLineWidthMustNotBeNegative);
        }

        [DoesNotReturn]
        public static void ThrowArgumentOutOfRangeException_ScalarStyle(string paramName)
        {
            throw new ArgumentOutOfRangeException(paramName, SR.Format(SR.ScalarStyleNotValid, paramName));
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_ScalarStyleHintPending()
        {
            throw GetInvalidOperationException(SR.ScalarStyleHintPending);
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_ScalarStyleHintNotApplicable()
        {
            throw GetInvalidOperationException(SR.ScalarStyleHintNotApplicable);
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_DocumentStartNotAtRoot()
        {
            throw GetInvalidOperationException(SR.DocumentStartNotAtRoot);
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_UnityStrippedMarkerPosition()
        {
            throw GetInvalidOperationException(SR.UnityStrippedMarkerPosition);
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_NotSupportedInJsonCompatibleStyle(string member)
        {
            throw GetInvalidOperationException(SR.Format(SR.NotSupportedInJsonCompatibleStyle, member));
        }

        [DoesNotReturn]
        public static void ThrowArgumentException_InvalidAnchorName()
        {
            throw new ArgumentException(SR.InvalidAnchorName);
        }

        [DoesNotReturn]
        public static void ThrowArgumentException_InvalidTag()
        {
            throw new ArgumentException(SR.InvalidTag);
        }

        [DoesNotReturn]
        public static void ThrowArgumentException_InvalidDirective()
        {
            throw new ArgumentException(SR.InvalidDirective);
        }

        [DoesNotReturn]
        public static void ThrowArgumentException_InvalidBlockComment()
        {
            throw new ArgumentException(SR.InvalidBlockComment);
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_NodePropertyPending()
        {
            throw GetInvalidOperationException(SR.NodePropertyPending);
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_NodePropertiesWithoutNode()
        {
            throw GetInvalidOperationException(SR.NodePropertiesWithoutNode);
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_NodePropertiesNotApplicable()
        {
            throw GetInvalidOperationException(SR.NodePropertiesNotApplicable);
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_ExplicitKeyNotOpen()
        {
            throw GetInvalidOperationException(SR.ExplicitKeyNotOpen);
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_ExplicitKeyOneNode()
        {
            throw GetInvalidOperationException(SR.ExplicitKeyOneNode);
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_ExplicitKeyNotClosed()
        {
            throw GetInvalidOperationException(SR.ExplicitKeyNotClosed);
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_DocumentEndNotAtRoot()
        {
            throw GetInvalidOperationException(SR.DocumentEndNotAtRoot);
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_DirectiveNotAllowed()
        {
            throw GetInvalidOperationException(SR.DirectiveNotAllowed);
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_DuplicateYamlDirective()
        {
            throw GetInvalidOperationException(SR.DuplicateYamlDirective);
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_DocumentStartExpected()
        {
            throw GetInvalidOperationException(SR.DocumentStartExpectedAfterDirectives);
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_CollectionStyleHintPending()
        {
            throw GetInvalidOperationException(SR.CollectionStyleHintPending);
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_CollectionStyleHintNotApplicable()
        {
            throw GetInvalidOperationException(SR.CollectionStyleHintNotApplicable);
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_CommentBetweenKeyPropertiesAndKey()
        {
            throw GetInvalidOperationException(SR.CommentBetweenKeyPropertiesAndKey);
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_RawYamlInFlowCollection()
        {
            throw GetInvalidOperationException(SR.RawYamlInFlowCollection);
        }

        [DoesNotReturn]
        public static void ThrowArgumentOutOfRangeException_CollectionStyle(string paramName)
        {
            throw new ArgumentOutOfRangeException(paramName, SR.Format(SR.CollectionStyleNotValid, paramName));
        }

        [DoesNotReturn]
        public static void ThrowArgumentException_BlockStyleIndentSizeTooSmall()
        {
            throw GetArgumentException(SR.BlockStyleIndentSizeTooSmall);
        }

        [DoesNotReturn]
        public static void ThrowArgumentException_BlockStyleTabIndentation()
        {
            throw GetArgumentException(SR.BlockStyleTabIndentation);
        }

        [DoesNotReturn]
        public static void ThrowArgumentException_PropertyNameTooLarge(int tokenLength)
        {
            throw GetArgumentException(SR.Format(SR.PropertyNameTooLarge, tokenLength));
        }

        [DoesNotReturn]
        public static void ThrowArgumentException_ValueTooLarge(long tokenLength)
        {
            throw GetArgumentException(SR.Format(SR.ValueTooLarge, tokenLength));
        }

        [DoesNotReturn]
        public static void ThrowArgumentException_ValueNotSupported()
        {
            throw GetArgumentException(SR.SpecialNumberValuesNotSupported);
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_NeedLargerSpan()
        {
            throw GetInvalidOperationException(SR.FailedToGetLargerSpan);
        }

        [DoesNotReturn]
        public static void ThrowPropertyNameTooLargeArgumentException(int length)
        {
            throw GetArgumentException(SR.Format(SR.PropertyNameTooLarge, length));
        }

        [DoesNotReturn]
        public static void ThrowArgumentException(ReadOnlySpan<byte> propertyName, ReadOnlySpan<byte> value)
        {
            if (propertyName.Length > YamlConstants.MaxUnescapedTokenSize)
            {
                ThrowArgumentException(SR.Format(SR.PropertyNameTooLarge, propertyName.Length));
            }
            else
            {
                Debug.Assert(value.Length > YamlConstants.MaxUnescapedTokenSize);
                ThrowArgumentException(SR.Format(SR.ValueTooLarge, value.Length));
            }
        }

        [DoesNotReturn]
        public static void ThrowArgumentException(ReadOnlySpan<byte> propertyName, ReadOnlySpan<char> value)
        {
            if (propertyName.Length > YamlConstants.MaxUnescapedTokenSize)
            {
                ThrowArgumentException(SR.Format(SR.PropertyNameTooLarge, propertyName.Length));
            }
            else
            {
                Debug.Assert(value.Length > YamlConstants.MaxCharacterTokenSize);
                ThrowArgumentException(SR.Format(SR.ValueTooLarge, value.Length));
            }
        }

        [DoesNotReturn]
        public static void ThrowArgumentException(ReadOnlySpan<char> propertyName, ReadOnlySpan<byte> value)
        {
            if (propertyName.Length > YamlConstants.MaxCharacterTokenSize)
            {
                ThrowArgumentException(SR.Format(SR.PropertyNameTooLarge, propertyName.Length));
            }
            else
            {
                Debug.Assert(value.Length > YamlConstants.MaxUnescapedTokenSize);
                ThrowArgumentException(SR.Format(SR.ValueTooLarge, value.Length));
            }
        }

        [DoesNotReturn]
        public static void ThrowArgumentException(ReadOnlySpan<char> propertyName, ReadOnlySpan<char> value)
        {
            if (propertyName.Length > YamlConstants.MaxCharacterTokenSize)
            {
                ThrowArgumentException(SR.Format(SR.PropertyNameTooLarge, propertyName.Length));
            }
            else
            {
                Debug.Assert(value.Length > YamlConstants.MaxCharacterTokenSize);
                ThrowArgumentException(SR.Format(SR.ValueTooLarge, value.Length));
            }
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationOrArgumentException(ReadOnlySpan<byte> propertyName, int currentDepth, int maxDepth)
        {
            currentDepth &= YamlConstants.RemoveFlagsBitMask;
            if (currentDepth >= maxDepth)
            {
                ThrowInvalidOperationException(SR.Format(SR.DepthTooLarge, currentDepth, maxDepth));
            }
            else
            {
                Debug.Assert(propertyName.Length > YamlConstants.MaxCharacterTokenSize);
                ThrowArgumentException(SR.Format(SR.PropertyNameTooLarge, propertyName.Length));
            }
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException(int currentDepth, int maxDepth)
        {
            currentDepth &= YamlConstants.RemoveFlagsBitMask;
            Debug.Assert(currentDepth >= maxDepth);
            ThrowInvalidOperationException(SR.Format(SR.DepthTooLarge, currentDepth, maxDepth));
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException(string message)
        {
            throw GetInvalidOperationException(message);
        }

        private static InvalidOperationException GetInvalidOperationException(string message)
        {
            return new InvalidOperationException(message) { Source = ExceptionSourceValueToRethrowAsYamlException };
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_DepthNonZeroOrEmptyYaml(int currentDepth)
        {
            throw GetInvalidOperationException(currentDepth);
        }

        private static InvalidOperationException GetInvalidOperationException(int currentDepth)
        {
            currentDepth &= YamlConstants.RemoveFlagsBitMask;
            if (currentDepth != 0)
            {
                return GetInvalidOperationException(SR.ZeroDepthAtEnd);
            }
            else
            {
                return GetInvalidOperationException(SR.EmptyYamlIsInvalid);
            }
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationOrArgumentException(ReadOnlySpan<char> propertyName, int currentDepth, int maxDepth)
        {
            currentDepth &= YamlConstants.RemoveFlagsBitMask;
            if (currentDepth >= maxDepth)
            {
                ThrowInvalidOperationException(SR.Format(SR.DepthTooLarge, currentDepth, maxDepth));
            }
            else
            {
                Debug.Assert(propertyName.Length > YamlConstants.MaxCharacterTokenSize);
                ThrowArgumentException(SR.Format(SR.PropertyNameTooLarge, propertyName.Length));
            }
        }

        public static InvalidOperationException GetInvalidOperationException_ExpectedArray(YamlTokenType tokenType)
        {
            return GetInvalidOperationException("array", tokenType);
        }

        public static InvalidOperationException GetInvalidOperationException_ExpectedObject(YamlTokenType tokenType)
        {
            return GetInvalidOperationException("object", tokenType);
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_ExpectedNumber(YamlTokenType tokenType)
        {
            throw GetInvalidOperationException("number", tokenType);
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_ExpectedBoolean(YamlTokenType tokenType)
        {
            throw GetInvalidOperationException("boolean", tokenType);
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_ExpectedString(YamlTokenType tokenType)
        {
            throw GetInvalidOperationException("string", tokenType);
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_ExpectedPropertyName(YamlTokenType tokenType)
        {
            throw GetInvalidOperationException("propertyName", tokenType);
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_ExpectedStringComparison(YamlTokenType tokenType)
        {
            throw GetInvalidOperationException(tokenType);
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_ExpectedComment(YamlTokenType tokenType)
        {
            throw GetInvalidOperationException("comment", tokenType);
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_CannotSkipOnPartial()
        {
            throw GetInvalidOperationException(SR.CannotSkip);
        }

        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ThrowInvalidOperationException_CannotMixEncodings(EnclosingContainerType previousEncoding, EnclosingContainerType currentEncoding)
        {
            throw GetInvalidOperationException(SR.Format(SR.CannotMixEncodings, GetEncodingName(previousEncoding), GetEncodingName(currentEncoding)));

            static string GetEncodingName(EnclosingContainerType encoding)
            {
                switch (encoding)
                {
                    case EnclosingContainerType.Utf8StringSequence: return "UTF-8";
                    case EnclosingContainerType.Utf16StringSequence: return "UTF-16";
                    case EnclosingContainerType.Base64StringSequence: return "Base64";
                    default:
                        Debug.Fail("Unknown encoding.");
                        return "Unknown";
                };
            }
        }

        private static InvalidOperationException GetInvalidOperationException(string message, YamlTokenType tokenType)
        {
            return GetInvalidOperationException(SR.Format(SR.InvalidCast, tokenType, message));
        }

        private static InvalidOperationException GetInvalidOperationException(YamlTokenType tokenType)
        {
            return GetInvalidOperationException(SR.Format(SR.InvalidComparison, tokenType));
        }

        [DoesNotReturn]
        internal static void ThrowYamlElementWrongTypeException(
            YamlTokenType expectedType,
            YamlTokenType actualType)
        {
            throw GetYamlElementWrongTypeException(expectedType.ToValueKind(), actualType.ToValueKind());
        }

        internal static InvalidOperationException GetYamlElementWrongTypeException(
            YamlValueKind expectedType,
            YamlValueKind actualType)
        {
            return GetInvalidOperationException(
                SR.Format(SR.YamlElementHasWrongType, expectedType, actualType));
        }

        internal static InvalidOperationException GetYamlElementWrongTypeException(
            string expectedTypeName,
            YamlValueKind actualType)
        {
            return GetInvalidOperationException(
                SR.Format(SR.YamlElementHasWrongType, expectedTypeName, actualType));
        }

        [DoesNotReturn]
        public static void ThrowYamlReaderException(ref Utf8YamlReader yaml, ExceptionResource resource, byte nextByte = default, ReadOnlySpan<byte> bytes = default)
        {
            throw GetYamlReaderException(ref yaml, resource, nextByte, bytes);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static YamlException GetYamlReaderException(ref Utf8YamlReader yaml, ExceptionResource resource, byte nextByte, ReadOnlySpan<byte> bytes)
        {
            string message = GetResourceString(ref yaml, resource, nextByte, Encoding.UTF8.GetString(bytes));

            long lineNumber = yaml.CurrentState._lineNumber;
            long bytePositionInLine = yaml.CurrentState._bytePositionInLine;

            message += $" LineNumber: {lineNumber} | BytePositionInLine: {bytePositionInLine}.";
            return new YamlReaderException(message, lineNumber, bytePositionInLine);
        }

        private static bool IsPrintable(byte value) => value is >= 0x20 and < 0x7F;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static string GetPrintableString(byte value)
        {
            return IsPrintable(value) ? ((char)value).ToString() : $"0x{value:X2}";
        }

        // This function will convert an ExceptionResource enum value to the resource string.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static string GetResourceString(ref Utf8YamlReader yaml, ExceptionResource resource, byte nextByte, string characters)
        {
            string character = GetPrintableString(nextByte);

            string message = "";
            switch (resource)
            {
                case ExceptionResource.ArrayDepthTooLarge:
                    message = SR.Format(SR.ArrayDepthTooLarge, yaml.CurrentState.Options.MaxDepth);
                    break;
                case ExceptionResource.MismatchedObjectArray:
                    message = SR.Format(SR.MismatchedObjectArray, character);
                    break;
                case ExceptionResource.ExpectedStartOfValueNotFound:
                    message = SR.Format(SR.ExpectedStartOfValueNotFound, character);
                    break;
                case ExceptionResource.ObjectDepthTooLarge:
                    message = SR.Format(SR.ObjectDepthTooLarge, yaml.CurrentState.Options.MaxDepth);
                    break;
                case ExceptionResource.ExpectedYamlTokens:
                    message = SR.ExpectedYamlTokens;
                    break;
                case ExceptionResource.NotEnoughData:
                    message = SR.NotEnoughData;
                    break;
                case ExceptionResource.ExpectedOneCompleteToken:
                    message = SR.ExpectedOneCompleteToken;
                    break;
                default:
                    Debug.Fail($"The ExceptionResource enum value: {resource} is not part of the switch. Add the appropriate case and exception message.");
                    break;
            }

            return message;
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException(ExceptionResource resource, int currentDepth, int maxDepth, byte token, YamlTokenType tokenType)
        {
            throw GetInvalidOperationException(resource, currentDepth, maxDepth, token, tokenType);
        }

        [DoesNotReturn]
        public static void ThrowArgumentException_InvalidCommentValue()
        {
            throw new ArgumentException(SR.CannotWriteCommentWithEmbeddedDelimiter);
        }

        [DoesNotReturn]
        public static void ThrowArgumentException_InvalidUTF8(ReadOnlySpan<byte> value)
        {
            var builder = new StringBuilder();

            int printFirst10 = Math.Min(value.Length, 10);

            for (int i = 0; i < printFirst10; i++)
            {
                byte nextByte = value[i];
                if (IsPrintable(nextByte))
                {
                    builder.Append((char)nextByte);
                }
                else
                {
                    builder.Append($"0x{nextByte:X2}");
                }
            }

            if (printFirst10 < value.Length)
            {
                builder.Append("...");
            }

            throw new ArgumentException(SR.Format(SR.CannotEncodeInvalidUTF8, builder));
        }

        [DoesNotReturn]
        public static void ThrowArgumentException_InvalidUTF16(int charAsInt)
        {
            throw new ArgumentException(SR.Format(SR.CannotEncodeInvalidUTF16, $"0x{charAsInt:X2}"));
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_ReadInvalidUTF16(int charAsInt)
        {
            throw GetInvalidOperationException(SR.Format(SR.CannotReadInvalidUTF16, $"0x{charAsInt:X2}"));
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_ReadIncompleteUTF16()
        {
            throw GetInvalidOperationException(SR.CannotReadIncompleteUTF16);
        }

        public static InvalidOperationException GetInvalidOperationException_ReadInvalidUTF8(DecoderFallbackException? innerException = null)
        {
            return GetInvalidOperationException(SR.CannotTranscodeInvalidUtf8, innerException);
        }

        public static ArgumentException GetArgumentException_ReadInvalidUTF16(EncoderFallbackException innerException)
        {
            return new ArgumentException(SR.CannotTranscodeInvalidUtf16, innerException);
        }

        public static InvalidOperationException GetInvalidOperationException(string message, Exception? innerException)
        {
            InvalidOperationException ex = new InvalidOperationException(message, innerException);
            ex.Source = ExceptionSourceValueToRethrowAsYamlException;
            return ex;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static InvalidOperationException GetInvalidOperationException(ExceptionResource resource, int currentDepth, int maxDepth, byte token, YamlTokenType tokenType)
        {
            string message = GetResourceString(resource, currentDepth, maxDepth, token, tokenType);
            InvalidOperationException ex = GetInvalidOperationException(message);
            ex.Source = ExceptionSourceValueToRethrowAsYamlException;
            return ex;
        }

        [DoesNotReturn]
        public static void ThrowOutOfMemoryException(uint capacity)
        {
            throw new OutOfMemoryException(SR.Format(SR.BufferMaximumSizeExceeded, capacity));
        }

        // This function will convert an ExceptionResource enum value to the resource string.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static string GetResourceString(ExceptionResource resource, int currentDepth, int maxDepth, byte token, YamlTokenType tokenType)
        {
            string message = "";
            switch (resource)
            {
                case ExceptionResource.MismatchedObjectArray:
                    Debug.Assert(token is YamlConstants.CloseBracket or YamlConstants.CloseBrace);
                    message = (tokenType == YamlTokenType.PropertyName) ?
                        SR.Format(SR.CannotWriteEndAfterProperty, (char)token) :
                        SR.Format(SR.MismatchedObjectArray, (char)token);
                    break;
                case ExceptionResource.DepthTooLarge:
                    message = SR.Format(SR.DepthTooLarge, currentDepth & YamlConstants.RemoveFlagsBitMask, maxDepth);
                    break;
                case ExceptionResource.CannotStartObjectArrayWithoutProperty:
                    message = SR.Format(SR.CannotStartObjectArrayWithoutProperty, tokenType);
                    break;
                case ExceptionResource.CannotStartObjectArrayAfterPrimitiveOrClose:
                    message = SR.Format(SR.CannotStartObjectArrayAfterPrimitiveOrClose, tokenType);
                    break;
                case ExceptionResource.CannotWriteValueWithinObject:
                    message = SR.Format(SR.CannotWriteValueWithinObject, tokenType);
                    break;
                case ExceptionResource.CannotWritePropertyWithinArray:
                    message = (tokenType == YamlTokenType.PropertyName) ?
                        SR.CannotWritePropertyAfterProperty :
                        SR.Format(SR.CannotWritePropertyWithinArray, tokenType);
                    break;
                case ExceptionResource.CannotWriteValueAfterPrimitiveOrClose:
                    message = SR.Format(SR.CannotWriteValueAfterPrimitiveOrClose, tokenType);
                    break;
                case ExceptionResource.CannotWriteWithinString:
                    message = SR.CannotWriteWithinString;
                    break;
                default:
                    Debug.Fail($"The ExceptionResource enum value: {resource} is not part of the switch. Add the appropriate case and exception message.");
                    break;
            }

            return message;
        }

        [DoesNotReturn]
        public static void ThrowFormatException()
        {
            throw new FormatException { Source = ExceptionSourceValueToRethrowAsYamlException };
        }

        [DoesNotReturn]
        public static void ThrowFormatException(NumericType numericType)
        {
            throw new FormatException(SR.Format(SR.FormatNumericType, numericType)) { Source = ExceptionSourceValueToRethrowAsYamlException };
        }

        [DoesNotReturn]
        public static void ThrowFormatException(DataType dataType)
        {
            string message = "";

            switch (dataType)
            {
                case DataType.Boolean:
                case DataType.DateOnly:
                case DataType.DateTime:
                case DataType.DateTimeOffset:
                case DataType.TimeOnly:
                case DataType.TimeSpan:
                case DataType.Guid:
                case DataType.Version:
                    message = SR.Format(SR.UnsupportedFormat, dataType);
                    break;
                case DataType.Base64String:
                    message = SR.CannotDecodeInvalidBase64;
                    break;
                default:
                    Debug.Fail($"The DataType enum value: {dataType} is not part of the switch. Add the appropriate case and exception message.");
                    break;
            }

            throw new FormatException(message) { Source = ExceptionSourceValueToRethrowAsYamlException };
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_ExpectedChar(YamlTokenType tokenType)
        {
            throw GetInvalidOperationException("char", tokenType);
        }

        [DoesNotReturn]
        public static void ThrowObjectDisposedException_Utf8YamlWriter()
        {
            throw new ObjectDisposedException(nameof(Utf8YamlWriter));
        }

        [DoesNotReturn]
        public static void ThrowObjectDisposedException_YamlDocument()
        {
            throw new ObjectDisposedException(nameof(YamlDocument));
        }

        [DoesNotReturn]
        public static void ThrowInsufficientExecutionStackException_YamlElementDeepEqualsInsufficientExecutionStack()
        {
            throw new InsufficientExecutionStackException(SR.YamlElementDeepEqualsInsufficientExecutionStack);
        }
    }

    internal enum ExceptionResource
    {
        ArrayDepthTooLarge,
        ExpectedStartOfValueNotFound,
        MismatchedObjectArray,
        ObjectDepthTooLarge,
        DepthTooLarge,
        CannotStartObjectArrayWithoutProperty,
        CannotStartObjectArrayAfterPrimitiveOrClose,
        CannotWriteValueWithinObject,
        CannotWriteValueAfterPrimitiveOrClose,
        CannotWritePropertyWithinArray,
        ExpectedYamlTokens,
        ExpectedOneCompleteToken,
        NotEnoughData,
        CannotWriteWithinString,
    }

    internal enum NumericType
    {
        Byte,
        SByte,
        Int16,
        Int32,
        Int64,
        Int128,
        UInt16,
        UInt32,
        UInt64,
        UInt128,
        Half,
        Single,
        Double,
        Decimal,
        BFloat16,
        Decimal32,
        Decimal64,
        Decimal128
    }

    internal enum DataType
    {
        Boolean,
        DateOnly,
        DateTime,
        DateTimeOffset,
        TimeOnly,
        TimeSpan,
        Base64String,
        Guid,
        Version,
    }
}
