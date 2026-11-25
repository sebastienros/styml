// Copyright (c) Sébastien Ros. Licensed under the MIT license.

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace System.Text.Yaml
{
    internal static partial class ThrowHelper
    {
        [DoesNotReturn]
        public static void ThrowYamlGrammarException(YamlGrammarError error, long lineNumber, long bytePositionInLine, string? argument = null)
        {
            throw GetYamlGrammarException(error, lineNumber, bytePositionInLine, argument);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static YamlException GetYamlGrammarException(YamlGrammarError error, long lineNumber, long bytePositionInLine, string? argument)
        {
            string format = error switch
            {
                YamlGrammarError.UnexpectedCharacter => SR.YamlGrammar_UnexpectedCharacter,
                YamlGrammarError.ReservedIndicator => SR.YamlGrammar_ReservedIndicator,
                YamlGrammarError.TabIndentation => SR.YamlGrammar_TabIndentation,
                YamlGrammarError.BadIndentation => SR.YamlGrammar_BadIndentation,
                YamlGrammarError.UnexpectedContentAfterNode => SR.YamlGrammar_UnexpectedContentAfterNode,
                YamlGrammarError.ExpectedColon => SR.YamlGrammar_ExpectedColon,
                YamlGrammarError.ExpectedSequenceEntry => SR.YamlGrammar_ExpectedSequenceEntry,
                YamlGrammarError.SequenceEntryNotAllowed => SR.YamlGrammar_SequenceEntryNotAllowed,
                YamlGrammarError.MappingValueNotAllowed => SR.YamlGrammar_MappingValueNotAllowed,
                YamlGrammarError.MultilineImplicitKey => SR.YamlGrammar_MultilineImplicitKey,
                YamlGrammarError.ImplicitKeyTooLong => SR.YamlGrammar_ImplicitKeyTooLong,
                YamlGrammarError.KeysNestedTooDeeply => SR.YamlGrammar_KeysNestedTooDeeply,
                YamlGrammarError.ImplicitCollectionKeyDisallowed => SR.YamlGrammar_ImplicitCollectionKeyDisallowed,
                YamlGrammarError.PropertiesOnAlias => SR.YamlGrammar_PropertiesOnAlias,
                YamlGrammarError.DuplicateProperty => SR.YamlGrammar_DuplicateProperty,
                YamlGrammarError.InvalidAnchor => SR.YamlGrammar_InvalidAnchor,
                YamlGrammarError.InvalidTag => SR.YamlGrammar_InvalidTag,
                YamlGrammarError.UnterminatedQuotedScalar => SR.YamlGrammar_UnterminatedQuotedScalar,
                YamlGrammarError.UnterminatedComment => SR.YamlGrammar_UnterminatedComment,
                YamlGrammarError.LineSeparatorInComment => SR.YamlGrammar_LineSeparatorInComment,
                YamlGrammarError.InvalidEscape => SR.YamlGrammar_InvalidEscape,
                YamlGrammarError.InvalidCharacterInScalar => SR.YamlGrammar_InvalidCharacterInScalar,
                YamlGrammarError.QuotedScalarIndentation => SR.YamlGrammar_QuotedScalarIndentation,
                YamlGrammarError.DocumentMarkerInScalar => SR.YamlGrammar_DocumentMarkerInScalar,
                YamlGrammarError.InvalidBlockScalarHeader => SR.YamlGrammar_InvalidBlockScalarHeader,
                YamlGrammarError.BlockScalarIndentation => SR.YamlGrammar_BlockScalarIndentation,
                YamlGrammarError.UnexpectedEndOfFlow => SR.YamlGrammar_UnexpectedEndOfFlow,
                YamlGrammarError.ExpectedFlowSeparator => SR.YamlGrammar_ExpectedFlowSeparator,
                YamlGrammarError.FlowIndentation => SR.YamlGrammar_FlowIndentation,
                YamlGrammarError.DocumentMarkerInFlow => SR.YamlGrammar_DocumentMarkerInFlow,
                YamlGrammarError.BlockInFlow => SR.YamlGrammar_BlockInFlow,
                YamlGrammarError.ExpectedDocumentEnd => SR.YamlGrammar_ExpectedDocumentEnd,
                YamlGrammarError.MultipleDocuments => SR.YamlGrammar_MultipleDocuments,
                YamlGrammarError.InvalidDirective => SR.YamlGrammar_InvalidDirective,
                YamlGrammarError.DirectiveWithoutDocument => SR.YamlGrammar_DirectiveWithoutDocument,
                YamlGrammarError.DirectiveAfterContent => SR.YamlGrammar_DirectiveAfterContent,
                YamlGrammarError.UnsupportedYamlVersion => SR.YamlGrammar_UnsupportedYamlVersion,
                YamlGrammarError.TagMismatch => SR.YamlGrammar_TagMismatch,
                YamlGrammarError.UndefinedTagHandle => SR.YamlGrammar_UndefinedTagHandle,
                YamlGrammarError.UnresolvedJsonSchemaScalar => SR.YamlGrammar_UnresolvedJsonSchemaScalar,
                _ => SR.YamlGrammar_UnexpectedCharacter,
            };

            string message = SR.Format(format, argument ?? string.Empty);
            message += $" LineNumber: {lineNumber} | BytePositionInLine: {bytePositionInLine}.";
            return new YamlReaderException(message, lineNumber, bytePositionInLine);
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_YamlAliasKey()
        {
            // Marked like the reader's other InvalidOperationExceptions, so the serializer rethrows it as a
            // YamlException with path and position.
            throw GetInvalidOperationException(SR.YamlAliasKeyHasNoText);
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_YamlComplexKey()
        {
            throw GetInvalidOperationException(SR.YamlComplexKeyHasNoText);
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_ExpectedComplexKey()
        {
            throw new InvalidOperationException(SR.YamlExpectedComplexKey);
        }

        [DoesNotReturn]
        public static void ThrowNotSupportedException_YamlComplexKeyJsonCompatible()
        {
            throw new NotSupportedException(SR.YamlComplexKeyJsonCompatible);
        }

        [DoesNotReturn]
        public static void ThrowNotSupportedException_YamlComplexKey()
        {
            throw new NotSupportedException(SR.YamlComplexKeyNotSupported);
        }

        [DoesNotReturn]
        public static void ThrowNotSupportedException_YamlAlias()
        {
            throw new NotSupportedException(SR.YamlAliasNotSupported);
        }

        [DoesNotReturn]
        public static void ThrowYamlException_UnknownAlias(string name)
        {
            throw new YamlException(SR.Format(SR.YamlUnknownAlias, name));
        }

        /// <summary>The DOM's unknown alias: the serializer catches it to expand an alias to an anchor outside the value (M5b).</summary>
        [DoesNotReturn]
        public static void ThrowYamlUnknownAliasException(string name)
        {
            throw new YamlException(SR.Format(SR.YamlUnknownAlias, name)) { IsUnknownAlias = true };
        }

        [DoesNotReturn]
        public static void ThrowYamlException_RecursiveAliasExpansion(string name)
        {
            throw new YamlException(SR.Format(SR.YamlRecursiveAliasExpansion, name));
        }

        [DoesNotReturn]
        public static void ThrowYamlException_SerializerAliasExpansionLimit(int limit)
        {
            throw new YamlException(SR.Format(SR.YamlSerializerAliasExpansionLimit, limit));
        }

        [DoesNotReturn]
        public static void ThrowNotSupportedException_MergeKeysInAsyncEnumerable()
        {
            throw new NotSupportedException(SR.YamlMergeKeysInAsyncEnumerable);
        }

        [DoesNotReturn]
        public static void ThrowYamlException_MergeValueNotAMapping()
        {
            throw new YamlException(SR.YamlMergeValueNotAMapping);
        }

        [DoesNotReturn]
        public static void ThrowYamlException_RecursiveAliasNeedsPreserve(string name)
        {
            throw new YamlException(SR.Format(SR.YamlRecursiveAliasNeedsPreserve, name));
        }

        [DoesNotReturn]
        public static void ThrowYamlException_RecursiveAliasNoInstance(string name, Type type)
        {
            throw new YamlException(SR.Format(SR.YamlRecursiveAliasNoInstance, name, type));
        }

        [DoesNotReturn]
        public static void ThrowYamlException_AliasTraversalTooDeep(int maxDepth)
        {
            throw new YamlException(SR.Format(SR.YamlAliasTraversalTooDeep, maxDepth));
        }

        [DoesNotReturn]
        public static void ThrowYamlException_AliasExpansionLimit(int limit)
        {
            throw new YamlException(SR.Format(SR.YamlAliasExpansionLimit, limit));
        }
    }

    /// <summary>Syntax errors of the YAML grammar (messages: Strings.resx, YamlGrammar_*).</summary>
    internal enum YamlGrammarError
    {
        UnexpectedCharacter,
        ReservedIndicator,
        TabIndentation,
        BadIndentation,
        UnexpectedContentAfterNode,
        ExpectedColon,
        ExpectedSequenceEntry,
        SequenceEntryNotAllowed,
        MappingValueNotAllowed,
        MultilineImplicitKey,
        ImplicitKeyTooLong,
        KeysNestedTooDeeply,
        ImplicitCollectionKeyDisallowed,
        PropertiesOnAlias,
        DuplicateProperty,
        InvalidAnchor,
        InvalidTag,
        UnterminatedQuotedScalar,
        UnterminatedComment,
        LineSeparatorInComment,
        InvalidEscape,
        InvalidCharacterInScalar,
        QuotedScalarIndentation,
        DocumentMarkerInScalar,
        InvalidBlockScalarHeader,
        BlockScalarIndentation,
        UnexpectedEndOfFlow,
        ExpectedFlowSeparator,
        FlowIndentation,
        DocumentMarkerInFlow,
        BlockInFlow,
        ExpectedDocumentEnd,
        MultipleDocuments,
        InvalidDirective,
        DirectiveWithoutDocument,
        DirectiveAfterContent,
        UnsupportedYamlVersion,
        TagMismatch,
        UndefinedTagHandle,
        UnresolvedJsonSchemaScalar,
    }
}
