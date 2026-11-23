// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO.Pipelines;
using System.Reflection;
using System.Text.Yaml.Serialization;
using System.Text.Yaml.Serialization.Metadata;

namespace System.Text.Yaml
{
    internal static partial class ThrowHelper
    {
        [DoesNotReturn]
        public static void ThrowArgumentException_DeserializeWrongType(Type type, object value)
        {
            throw new ArgumentException(SR.Format(SR.DeserializeWrongType, type, value.GetType()));
        }

        [DoesNotReturn]
        public static void ThrowArgumentException_SerializerDoesNotSupportComments(string paramName)
        {
            throw new ArgumentException(SR.YamlSerializerDoesNotSupportComments, paramName);
        }

        [DoesNotReturn]
        public static void ThrowNotSupportedException_SerializationNotSupported(Type propertyType)
        {
            throw new NotSupportedException(SR.Format(SR.SerializationNotSupportedType, propertyType));
        }

        [DoesNotReturn]
        public static void ThrowNotSupportedException_TypeRequiresAsyncSerialization(Type propertyType)
        {
            throw new NotSupportedException(SR.Format(SR.TypeRequiresAsyncSerialization, propertyType));
        }

        [DoesNotReturn]
        public static void ThrowNotSupportedException_DictionaryKeyTypeNotSupported(Type keyType, YamlConverter converter)
        {
            throw new NotSupportedException(SR.Format(SR.DictionaryKeyTypeNotSupported, keyType, converter.GetType()));
        }

        [DoesNotReturn]
        public static void ThrowYamlException_DeserializeUnableToConvertValue(Type propertyType)
        {
            throw new YamlException(SR.Format(SR.DeserializeUnableToConvertValue, propertyType)) { AppendPathInformation = true };
        }

        [DoesNotReturn]
        public static void ThrowInvalidCastException_DeserializeUnableToAssignValue(Type typeOfValue, Type declaredType)
        {
            throw new InvalidCastException(SR.Format(SR.DeserializeUnableToAssignValue, typeOfValue, declaredType));
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_DeserializeUnableToAssignNull(Type declaredType)
        {
            throw new InvalidOperationException(SR.Format(SR.DeserializeUnableToAssignNull, declaredType));
        }

        [DoesNotReturn]
        public static void ThrowYamlException_PropertyGetterDisallowNull(string propertyName, Type declaringType)
        {
            throw new YamlException(SR.Format(SR.PropertyGetterDisallowNull, propertyName, declaringType)) { AppendPathInformation = true };
        }

        [DoesNotReturn]
        public static void ThrowYamlException_PropertySetterDisallowNull(string propertyName, Type declaringType)
        {
            throw new YamlException(SR.Format(SR.PropertySetterDisallowNull, propertyName, declaringType)) { AppendPathInformation = true };
        }

        [DoesNotReturn]
        public static void ThrowYamlException_ConstructorParameterDisallowNull(string parameterName, Type declaringType)
        {
            throw new YamlException(SR.Format(SR.ConstructorParameterDisallowNull, parameterName, declaringType)) { AppendPathInformation = true };
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_ObjectCreationHandlingPopulateNotSupportedByConverter(YamlPropertyInfo propertyInfo)
        {
            throw new InvalidOperationException(SR.Format(SR.ObjectCreationHandlingPopulateNotSupportedByConverter, propertyInfo.Name, propertyInfo.DeclaringType));
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_ObjectCreationHandlingPropertyMustHaveAGetter(YamlPropertyInfo propertyInfo)
        {
            throw new InvalidOperationException(SR.Format(SR.ObjectCreationHandlingPropertyMustHaveAGetter, propertyInfo.Name, propertyInfo.DeclaringType));
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_ObjectCreationHandlingPropertyValueTypeMustHaveASetter(YamlPropertyInfo propertyInfo)
        {
            throw new InvalidOperationException(SR.Format(SR.ObjectCreationHandlingPropertyValueTypeMustHaveASetter, propertyInfo.Name, propertyInfo.DeclaringType));
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_ObjectCreationHandlingPropertyCannotAllowPolymorphicDeserialization(YamlPropertyInfo propertyInfo)
        {
            throw new InvalidOperationException(SR.Format(SR.ObjectCreationHandlingPropertyCannotAllowPolymorphicDeserialization, propertyInfo.Name, propertyInfo.DeclaringType));
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_ObjectCreationHandlingPropertyCannotAllowReadOnlyMember(YamlPropertyInfo propertyInfo)
        {
            throw new InvalidOperationException(SR.Format(SR.ObjectCreationHandlingPropertyCannotAllowReadOnlyMember, propertyInfo.Name, propertyInfo.DeclaringType));
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_ObjectCreationHandlingPropertyCannotAllowReferenceHandling()
        {
            throw new InvalidOperationException(SR.ObjectCreationHandlingPropertyCannotAllowReferenceHandling);
        }

        [DoesNotReturn]
        public static void ThrowNotSupportedException_ObjectCreationHandlingPropertyDoesNotSupportParameterizedConstructors()
        {
            throw new NotSupportedException(SR.ObjectCreationHandlingPropertyDoesNotSupportParameterizedConstructors);
        }

        [DoesNotReturn]
        public static void ThrowYamlException_SerializationConverterRead(YamlConverter? converter)
        {
            throw new YamlException(SR.Format(SR.SerializationConverterRead, converter)) { AppendPathInformation = true };
        }

        [DoesNotReturn]
        public static void ThrowYamlException_SerializationConverterWrite(YamlConverter? converter)
        {
            throw new YamlException(SR.Format(SR.SerializationConverterWrite, converter)) { AppendPathInformation = true };
        }

        [DoesNotReturn]
        public static void ThrowYamlException_SerializerCycleDetected(int maxDepth)
        {
            throw new YamlException(SR.Format(SR.SerializerCycleDetected, maxDepth)) { AppendPathInformation = true };
        }

        [DoesNotReturn]
        public static void ThrowYamlException(string? message = null)
        {
            throw new YamlException(message) { AppendPathInformation = true };
        }

        [DoesNotReturn]
        public static void ThrowArgumentException_CannotSerializeInvalidType(string paramName, Type typeToConvert, Type? declaringType, string? propertyName)
        {
            if (declaringType == null)
            {
                Debug.Assert(propertyName is null);
                throw new ArgumentException(SR.Format(SR.CannotSerializeInvalidType, typeToConvert), paramName);
            }

            Debug.Assert(propertyName is not null);
            throw new ArgumentException(SR.Format(SR.CannotSerializeInvalidMember, typeToConvert, propertyName, declaringType), paramName);
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_CannotSerializeInvalidType(Type typeToConvert, Type? declaringType, MemberInfo? memberInfo)
        {
            if (declaringType == null)
            {
                Debug.Assert(memberInfo == null);
                throw new InvalidOperationException(SR.Format(SR.CannotSerializeInvalidType, typeToConvert));
            }

            Debug.Assert(memberInfo != null);
            throw new InvalidOperationException(SR.Format(SR.CannotSerializeInvalidMember, typeToConvert, memberInfo.Name, declaringType));
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_SerializationConverterNotCompatible(Type converterType, Type type)
        {
            throw new InvalidOperationException(SR.Format(SR.SerializationConverterNotCompatible, converterType, type));
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_ResolverTypeNotCompatible(Type requestedType, Type actualType)
        {
            throw new InvalidOperationException(SR.Format(SR.ResolverTypeNotCompatible, actualType, requestedType));
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_ResolverTypeInfoOptionsNotCompatible()
        {
            throw new InvalidOperationException(SR.ResolverTypeInfoOptionsNotCompatible);
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_YamlSerializerOptionsNoTypeInfoResolverSpecified()
        {
            throw new InvalidOperationException(SR.YamlSerializerOptionsNoTypeInfoResolverSpecified);
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_YamlSerializerIsReflectionDisabled()
        {
            throw new InvalidOperationException(SR.YamlSerializerIsReflectionDisabled);
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_SerializationConverterOnAttributeInvalid(Type classType, MemberInfo? memberInfo)
        {
            string location = classType.ToString();
            if (memberInfo != null)
            {
                location += $".{memberInfo.Name}";
            }

            throw new InvalidOperationException(SR.Format(SR.SerializationConverterOnAttributeInvalid, location));
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_SerializationConverterOnAttributeNotCompatible(Type classTypeAttributeIsOn, MemberInfo? memberInfo, Type typeToConvert)
        {
            string location = classTypeAttributeIsOn.ToString();

            if (memberInfo != null)
            {
                location += $".{memberInfo.Name}";
            }

            throw new InvalidOperationException(SR.Format(SR.SerializationConverterOnAttributeNotCompatible, location, typeToConvert));
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_SerializationConverterOnAttributeOpenGenericNotCompatible(Type classType, MemberInfo? memberInfo, Type converterType)
        {
            string location = classType.ToString();
            if (memberInfo != null)
            {
                location += $".{memberInfo.Name}";
            }

            throw new InvalidOperationException(SR.Format(SR.SerializationConverterOnAttributeOpenGenericNotCompatible, location, converterType));
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_SerializerOptionsReadOnly(YamlSerializerContext? context)
        {
            string message = context is null
                ? SR.SerializerOptionsReadOnly
                : SR.SerializerContextOptionsReadOnly;

            throw new InvalidOperationException(message);
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_DefaultTypeInfoResolverImmutable()
        {
            throw new InvalidOperationException(SR.DefaultTypeInfoResolverImmutable);
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_TypeInfoResolverChainImmutable()
        {
            throw new InvalidOperationException(SR.TypeInfoResolverChainImmutable);
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_TypeInfoImmutable()
        {
            throw new InvalidOperationException(SR.TypeInfoImmutable);
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_InvalidChainedResolver()
        {
            throw new InvalidOperationException(SR.SerializerOptions_InvalidChainedResolver);
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_SerializerPropertyNameConflict(Type type, string propertyName)
        {
            throw new InvalidOperationException(SR.Format(SR.SerializerPropertyNameConflict, type, propertyName));
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_SerializerPropertyNameNull(YamlPropertyInfo yamlPropertyInfo)
        {
            throw new InvalidOperationException(SR.Format(SR.SerializerPropertyNameNull, yamlPropertyInfo.DeclaringType, yamlPropertyInfo.MemberName));
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_YamlPropertyRequiredAndNotDeserializable(YamlPropertyInfo yamlPropertyInfo)
        {
            throw new InvalidOperationException(SR.Format(SR.YamlPropertyRequiredAndNotDeserializable, yamlPropertyInfo.Name, yamlPropertyInfo.DeclaringType));
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_YamlPropertyRequiredAndExtensionData(YamlPropertyInfo yamlPropertyInfo)
        {
            throw new InvalidOperationException(SR.Format(SR.YamlPropertyRequiredAndExtensionData, yamlPropertyInfo.Name, yamlPropertyInfo.DeclaringType));
        }

        [DoesNotReturn]
        public static void ThrowYamlException_YamlRequiredPropertyMissing(YamlTypeInfo parent, BitArray assignedOrNotRequiredPropertiesSet)
        {
            StringBuilder builder = new();

            foreach (YamlPropertyInfo property in parent.PropertyCache)
            {
                if (!assignedOrNotRequiredPropertiesSet[property.PropertyIndex])
                {
                    if (!AppendMissingProperty(builder, property.Name))
                    {
                        break;
                    }
                }
            }

            throw new YamlException(SR.Format(SR.YamlRequiredPropertiesMissing, parent.Type, builder.ToString()));
        }

        [DoesNotReturn]
        public static void ThrowYamlException_YamlRequiredPropertyMissing(Type type, string propertyList)
        {
            throw new YamlException(SR.Format(SR.YamlRequiredPropertiesMissing, type, propertyList));
        }

        /// <summary>
        /// Appends a property name to a missing-properties list with culture-aware separators and a soft length cut-off.
        /// Returns false when the cut-off is reached and no more names should be appended.
        /// </summary>
        internal static bool AppendMissingProperty(StringBuilder builder, string propertyName)
        {
            const int CutOffLength = 60;

            if (builder.Length > 0)
            {
                builder.Append(CultureInfo.CurrentUICulture.TextInfo.ListSeparator);
                builder.Append(' ');
            }

            builder.Append('\'');
            builder.Append(propertyName);
            builder.Append('\'');

            return builder.Length < CutOffLength;
        }

        [DoesNotReturn]
        public static void ThrowYamlException_DuplicatePropertyNotAllowed(YamlPropertyInfo property)
        {
            throw new YamlException(SR.Format(SR.DuplicatePropertiesNotAllowed_YamlPropertyInfo, property.Name, property.DeclaringType));
        }

        [DoesNotReturn]
        public static void ThrowYamlException_ComplexKeyNotReadable(string rawKey, Type keyType)
        {
            throw new YamlException(SR.Format(SR.YamlComplexKeyNotReadable, rawKey, keyType));
        }

        [DoesNotReturn]
        public static void ThrowYamlException_DuplicatePropertyNotAllowed()
        {
            throw new YamlException(SR.DuplicatePropertiesNotAllowed);
        }

        [DoesNotReturn]
        public static void ThrowYamlException_DuplicatePropertyNotAllowed(string name)
        {
            throw new YamlException(SR.Format(SR.DuplicatePropertiesNotAllowed_NameSpan, Truncate(name)));
        }

        [DoesNotReturn]
        public static void ThrowYamlException_DuplicatePropertyNotAllowed(ReadOnlySpan<byte> nameBytes)
        {
            string name = Encoding.UTF8.GetString(nameBytes);
            throw new YamlException(SR.Format(SR.DuplicatePropertiesNotAllowed_NameSpan, Truncate(name)));
        }

        private static unsafe string Truncate(ReadOnlySpan<char> str)
        {
            const int MaxLength = 15;

            if (str.Length <= MaxLength)
            {
                return str.ToString();
            }

            Span<char> builder = stackalloc char[MaxLength + 3];
            str.Slice(0, MaxLength).CopyTo(builder);
            builder[MaxLength] = builder[MaxLength + 1] = builder[MaxLength + 2] = '.';
            return builder.ToString();
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_NamingPolicyReturnNull(YamlNamingPolicy namingPolicy)
        {
            throw new InvalidOperationException(SR.Format(SR.NamingPolicyReturnNull, namingPolicy));
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_SerializerConverterFactoryReturnsNull(Type converterType)
        {
            throw new InvalidOperationException(SR.Format(SR.SerializerConverterFactoryReturnsNull, converterType));
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_SerializerConverterFactoryReturnsYamlConverterFactory(Type converterType)
        {
            throw new InvalidOperationException(SR.Format(SR.SerializerConverterFactoryReturnsYamlConverterFactory, converterType));
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_MultiplePropertiesBindToConstructorParameters(
            Type parentType,
            string parameterName,
            string firstMatchName,
            string secondMatchName)
        {
            throw new InvalidOperationException(
                SR.Format(
                    SR.MultipleMembersBindWithConstructorParameter,
                    firstMatchName,
                    secondMatchName,
                    parentType,
                    parameterName));
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_ConstructorParameterIncompleteBinding(Type parentType)
        {
            throw new InvalidOperationException(SR.Format(SR.ConstructorParamIncompleteBinding, parentType));
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_ExtensionDataCannotBindToCtorParam(string propertyName, YamlPropertyInfo yamlPropertyInfo)
        {
            throw new InvalidOperationException(SR.Format(SR.ExtensionDataCannotBindToCtorParam, propertyName, yamlPropertyInfo.DeclaringType));
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_YamlIncludeOnInaccessibleProperty(string memberName, Type declaringType)
        {
            throw new InvalidOperationException(SR.Format(SR.YamlIncludeOnInaccessibleProperty, memberName, declaringType));
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_EmptyCollectionConditionOnValueTypeInvalid(string clrPropertyName, Type propertyDeclaringType)
        {
            throw new InvalidOperationException(SR.Format(SR.EmptyCollectionConditionOnValueTypeInvalid, clrPropertyName, propertyDeclaringType));
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_YamlCommentInvalid(string memberName, Type declaringType)
        {
            throw new InvalidOperationException(SR.Format(SR.YamlCommentInvalid, memberName, declaringType));
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_YamlScalarStyleAttributeNone(string memberName, Type declaringType)
        {
            throw new InvalidOperationException(SR.Format(SR.YamlScalarStyleAttributeNone, memberName, declaringType));
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_IgnoreConditionOnValueTypeInvalid(string clrPropertyName, Type propertyDeclaringType)
        {
            throw new InvalidOperationException(SR.Format(SR.IgnoreConditionOnValueTypeInvalid, clrPropertyName, propertyDeclaringType));
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_NumberHandlingOnPropertyInvalid(YamlPropertyInfo yamlPropertyInfo)
        {
            Debug.Assert(!yamlPropertyInfo.IsForTypeInfo);
            throw new InvalidOperationException(SR.Format(SR.NumberHandlingOnPropertyInvalid, yamlPropertyInfo.MemberName, yamlPropertyInfo.DeclaringType));
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_ConverterCanConvertMultipleTypes(Type runtimePropertyType, YamlConverter yamlConverter)
        {
            throw new InvalidOperationException(SR.Format(SR.ConverterCanConvertMultipleTypes, yamlConverter.GetType(), yamlConverter.Type, runtimePropertyType));
        }

        [DoesNotReturn]
        public static void ThrowNotSupportedException_ObjectWithParameterizedCtorRefMetadataNotSupported(
            ReadOnlySpan<byte> propertyName,
            ref Utf8YamlReader reader,
            scoped ref ReadStack state)
        {
            YamlTypeInfo yamlTypeInfo = state.GetTopYamlTypeInfoWithParameterizedConstructor();
            state.Current.YamlPropertyName = propertyName.ToArray();

            NotSupportedException ex = new NotSupportedException(
                SR.Format(SR.ObjectWithParameterizedCtorRefMetadataNotSupported, yamlTypeInfo.Type));
            ThrowNotSupportedException(ref state, reader, ex);
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_YamlTypeInfoOperationNotPossibleForKind(YamlTypeInfoKind kind)
        {
            throw new InvalidOperationException(SR.Format(SR.InvalidYamlTypeInfoOperationForKind, kind));
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_YamlTypeInfoOnDeserializingCallbacksNotSupported(Type type)
        {
            throw new InvalidOperationException(SR.Format(SR.OnDeserializingCallbacksNotSupported, type));
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_CreateObjectConverterNotCompatible(Type type)
        {
            throw new InvalidOperationException(SR.Format(SR.CreateObjectConverterNotCompatible, type));
        }

        [DoesNotReturn]
        public static void ReThrowWithPath(scoped ref ReadStack state, YamlReaderException ex)
        {
            Debug.Assert(ex.Path is null);

            string path = state.YamlPath();
            string message = ex.Message;

            // Insert the "Path" portion before "LineNumber" and "BytePositionInLine".
#if NET
            int iPos = message.AsSpan().LastIndexOf(" LineNumber: ");
#else
            int iPos = message.LastIndexOf(" LineNumber: ", StringComparison.Ordinal);
#endif
            if (iPos >= 0)
            {
                message = $"{message.Substring(0, iPos)} Path: {path} |{message.Substring(iPos)}";
            }
            else
            {
                message += $" Path: {path}.";
            }

            throw new YamlException(message, path, ex.LineNumber, ex.BytePositionInLine, ex);
        }

        [DoesNotReturn]
        public static void ReThrowWithPath(scoped ref ReadStack state, in Utf8YamlReader reader, Exception ex)
        {
            YamlException yamlException = new YamlException(null, ex);
            AddYamlExceptionInformation(ref state, reader, yamlException);
            throw yamlException;
        }

        public static void AddYamlExceptionInformation(scoped ref ReadStack state, in Utf8YamlReader reader, YamlException ex)
        {
            Debug.Assert(ex.Path is null); // do not overwrite existing path information

            long lineNumber = reader.CurrentState._lineNumber;
            ex.LineNumber = lineNumber;

            long bytePositionInLine = reader.CurrentState._bytePositionInLine;
            ex.BytePositionInLine = bytePositionInLine;

            string path = state.YamlPath();
            ex.Path = path;

            string? message = ex._message;

            if (string.IsNullOrEmpty(message))
            {
                // Use a default message.
                Type propertyType = state.Current.YamlPropertyInfo?.PropertyType ??
                    state.Current.CtorArgumentState?.YamlParameterInfo?.ParameterType ??
                    state.Current.YamlTypeInfo.Type;
                message = SR.Format(SR.DeserializeUnableToConvertValue, propertyType);
                ex.AppendPathInformation = true;
            }

            if (ex.AppendPathInformation)
            {
                message += $" Path: {path} | LineNumber: {lineNumber} | BytePositionInLine: {bytePositionInLine}.";
                ex.SetMessage(message);
            }
        }

        [DoesNotReturn]
        public static void ReThrowWithPath(ref WriteStack state, Exception ex)
        {
            YamlException yamlException = new YamlException(null, ex);
            AddYamlExceptionInformation(ref state, yamlException);
            throw yamlException;
        }

        public static void AddYamlExceptionInformation(ref WriteStack state, YamlException ex)
        {
            Debug.Assert(ex.Path is null); // do not overwrite existing path information

            string path = state.PropertyPath();
            ex.Path = path;

            string? message = ex._message;
            if (string.IsNullOrEmpty(message))
            {
                // Use a default message.
                message = SR.SerializeUnableToSerialize;
                ex.AppendPathInformation = true;
            }

            if (ex.AppendPathInformation)
            {
                message += $" Path: {path}.";
                ex.SetMessage(message);
            }
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_SerializationDuplicateAttribute(Type attribute, MemberInfo memberInfo)
        {
            string location = memberInfo is Type type ? type.ToString() : $"{memberInfo.DeclaringType}.{memberInfo.Name}";
            throw new InvalidOperationException(SR.Format(SR.SerializationDuplicateAttribute, attribute, location));
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_SerializationDuplicateTypeAttribute(Type classType, Type attribute)
        {
            throw new InvalidOperationException(SR.Format(SR.SerializationDuplicateTypeAttribute, classType, attribute));
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_SerializationDuplicateTypeAttribute<TAttribute>(Type classType)
        {
            throw new InvalidOperationException(SR.Format(SR.SerializationDuplicateTypeAttribute, classType, typeof(TAttribute)));
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_ExtensionDataConflictsWithUnmappedMemberHandling(Type classType, YamlPropertyInfo yamlPropertyInfo)
        {
            throw new InvalidOperationException(SR.Format(SR.ExtensionDataConflictsWithUnmappedMemberHandling, classType, yamlPropertyInfo.MemberName));
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_SerializationDataExtensionPropertyInvalid(YamlPropertyInfo yamlPropertyInfo)
        {
            throw new InvalidOperationException(SR.Format(SR.SerializationDataExtensionPropertyInvalid, yamlPropertyInfo.PropertyType, yamlPropertyInfo.MemberName));
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_PropertyTypeNotNullable(YamlPropertyInfo yamlPropertyInfo)
        {
            throw new InvalidOperationException(SR.Format(SR.PropertyTypeNotNullable, yamlPropertyInfo.PropertyType));
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_NodeYamlObjectCustomConverterNotAllowedOnExtensionProperty()
        {
            throw new InvalidOperationException(SR.NodeYamlObjectCustomConverterNotAllowedOnExtensionProperty);
        }

        [DoesNotReturn]
        public static void ThrowNotSupportedException(scoped ref ReadStack state, in Utf8YamlReader reader, Exception innerException)
        {
            string message = innerException.Message;

            // The caller should check to ensure path is not already set.
            Debug.Assert(!message.Contains(" Path: "));

            // Obtain the type to show in the message.
            Type propertyType = state.Current.YamlPropertyInfo?.PropertyType ?? state.Current.YamlTypeInfo.Type;

            if (!message.Contains(propertyType.ToString()))
            {
                if (message.Length > 0)
                {
                    message += " ";
                }

                message += SR.Format(SR.SerializationNotSupportedParentType, propertyType);
            }

            long lineNumber = reader.CurrentState._lineNumber;
            long bytePositionInLine = reader.CurrentState._bytePositionInLine;
            message += $" Path: {state.YamlPath()} | LineNumber: {lineNumber} | BytePositionInLine: {bytePositionInLine}.";

            throw new NotSupportedException(message, innerException);
        }

        [DoesNotReturn]
        public static void ThrowNotSupportedException(ref WriteStack state, Exception innerException)
        {
            string message = innerException.Message;

            // The caller should check to ensure path is not already set.
            Debug.Assert(!message.Contains(" Path: "));

            // Obtain the type to show in the message.
            Type propertyType = state.Current.YamlPropertyInfo?.PropertyType ?? state.Current.YamlTypeInfo.Type;

            if (!message.Contains(propertyType.ToString()))
            {
                if (message.Length > 0)
                {
                    message += " ";
                }

                message += SR.Format(SR.SerializationNotSupportedParentType, propertyType);
            }

            message += $" Path: {state.PropertyPath()}.";

            throw new NotSupportedException(message, innerException);
        }

        [DoesNotReturn]
        public static void ThrowNotSupportedException_DeserializeNoConstructor(YamlTypeInfo typeInfo, ref Utf8YamlReader reader, scoped ref ReadStack state)
        {
            Type type = typeInfo.Type;
            string message;

            if (type.IsInterface || type.IsAbstract)
            {
                if (typeInfo.PolymorphicTypeResolver?.UsesTypeDiscriminators is true)
                {
                    message = SR.Format(SR.DeserializationMustSpecifyTypeDiscriminator, type);
                }
                else if (typeInfo.Kind is YamlTypeInfoKind.Enumerable or YamlTypeInfoKind.Dictionary)
                {
                    message = SR.Format(SR.CannotPopulateCollection, type);
                }
                else
                {
                    message = SR.Format(SR.DeserializeInterfaceOrAbstractType, type);
                }
            }
            else
            {
                message = SR.Format(SR.DeserializeNoConstructor, nameof(YamlConstructorAttribute), type);
            }

            ThrowNotSupportedException(ref state, reader, new NotSupportedException(message));
        }

        [DoesNotReturn]
        public static void ThrowNotSupportedException_CannotPopulateCollection(Type type, ref Utf8YamlReader reader, scoped ref ReadStack state)
        {
            ThrowNotSupportedException(ref state, reader, new NotSupportedException(SR.Format(SR.CannotPopulateCollection, type)));
        }

        [DoesNotReturn]
        public static void ThrowYamlException_MetadataValuesInvalidToken(YamlTokenType tokenType)
        {
            ThrowYamlException(SR.Format(SR.MetadataInvalidTokenAfterValues, tokenType));
        }

        [DoesNotReturn]
        public static void ThrowYamlException_MetadataReferenceNotFound(string id)
        {
            ThrowYamlException(SR.Format(SR.MetadataReferenceNotFound, id));
        }

        [DoesNotReturn]
        public static void ThrowYamlException_MetadataValueWasNotString(YamlTokenType tokenType)
        {
            ThrowYamlException(SR.Format(SR.MetadataValueWasNotString, tokenType));
        }

        [DoesNotReturn]
        public static void ThrowYamlException_MetadataValueWasNotString(YamlValueKind valueKind)
        {
            ThrowYamlException(SR.Format(SR.MetadataValueWasNotString, valueKind));
        }

        [DoesNotReturn]
        public static void ThrowYamlException_MetadataReferenceObjectCannotContainOtherProperties(ReadOnlySpan<byte> propertyName, scoped ref ReadStack state)
        {
            state.Current.YamlPropertyName = propertyName.ToArray();
            ThrowYamlException_MetadataReferenceObjectCannotContainOtherProperties();
        }

        [DoesNotReturn]
        public static void ThrowYamlException_MetadataUnexpectedProperty(ReadOnlySpan<byte> propertyName, scoped ref ReadStack state)
        {
            state.Current.YamlPropertyName = propertyName.ToArray();
            ThrowYamlException(SR.MetadataUnexpectedProperty);
        }

        [DoesNotReturn]
        public static void ThrowYamlException_UnmappedYamlProperty(Type type, string unmappedPropertyName)
        {
            throw new YamlException(SR.Format(SR.UnmappedYamlProperty, unmappedPropertyName, type));
        }

        [DoesNotReturn]
        public static void ThrowYamlException_MetadataReferenceObjectCannotContainOtherProperties()
        {
            ThrowYamlException(SR.MetadataReferenceCannotContainOtherProperties);
        }

        [DoesNotReturn]
        public static void ThrowYamlException_MetadataIdCannotBeCombinedWithRef(ReadOnlySpan<byte> propertyName, scoped ref ReadStack state)
        {
            state.Current.YamlPropertyName = propertyName.ToArray();
            ThrowYamlException(SR.MetadataIdCannotBeCombinedWithRef);
        }

        [DoesNotReturn]
        public static void ThrowYamlException_MetadataStandaloneValuesProperty(scoped ref ReadStack state, ReadOnlySpan<byte> propertyName)
        {
            state.Current.YamlPropertyName = propertyName.ToArray();
            ThrowYamlException(SR.MetadataStandaloneValuesProperty);
        }

        [DoesNotReturn]
        public static void ThrowYamlException_MetadataInvalidPropertyWithLeadingDollarSign(ReadOnlySpan<byte> propertyName, scoped ref ReadStack state, in Utf8YamlReader reader)
        {
            // Set PropertyInfo or KeyName to write down the conflicting property name in YamlException.Path
            if (state.Current.IsProcessingDictionary())
            {
                state.Current.YamlPropertyNameAsString = reader.GetString();
            }
            else
            {
                state.Current.YamlPropertyName = propertyName.ToArray();
            }

            ThrowYamlException(SR.MetadataInvalidPropertyWithLeadingDollarSign);
        }

        [DoesNotReturn]
        public static void ThrowYamlException_MetadataDuplicateIdFound(string id)
        {
            ThrowYamlException(SR.Format(SR.MetadataDuplicateIdFound, id));
        }

        [DoesNotReturn]
        public static void ThrowYamlException_DuplicateMetadataProperty(ReadOnlySpan<byte> utf8PropertyName)
        {
            ThrowYamlException(SR.Format(SR.DuplicateMetadataProperty, Encoding.UTF8.GetString(utf8PropertyName)));
        }

        [DoesNotReturn]
        public static void ThrowYamlException_MetadataInvalidReferenceToValueType(Type propertyType)
        {
            ThrowYamlException(SR.Format(SR.MetadataInvalidReferenceToValueType, propertyType));
        }

        [DoesNotReturn]
        public static void ThrowYamlException_MetadataInvalidPropertyInArrayMetadata(scoped ref ReadStack state, Type propertyType, in Utf8YamlReader reader)
        {
            state.Current.YamlPropertyName = reader.HasValueSequence ? reader.ValueSequence.ToArray() : reader.ValueSpan.ToArray();
            string propertyNameAsString = reader.GetString()!;

            ThrowYamlException(SR.Format(SR.MetadataPreservedArrayFailed,
                SR.Format(SR.MetadataInvalidPropertyInArrayMetadata, propertyNameAsString),
                SR.Format(SR.DeserializeUnableToConvertValue, propertyType)));
        }

        [DoesNotReturn]
        public static void ThrowYamlException_MetadataPreservedArrayValuesNotFound(scoped ref ReadStack state, Type propertyType)
        {
            // Missing $values, JSON path should point to the property's object.
            state.Current.YamlPropertyName = null;

            ThrowYamlException(SR.Format(SR.MetadataPreservedArrayFailed,
                SR.MetadataStandaloneValuesProperty,
                SR.Format(SR.DeserializeUnableToConvertValue, propertyType)));
        }

        [DoesNotReturn]
        public static void ThrowYamlException_MetadataCannotParsePreservedObjectIntoImmutable(Type propertyType)
        {
            ThrowYamlException(SR.Format(SR.MetadataCannotParsePreservedObjectToImmutable, propertyType));
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_MetadataReferenceOfTypeCannotBeAssignedToType(string referenceId, Type currentType, Type typeToConvert)
        {
            throw new InvalidOperationException(SR.Format(SR.MetadataReferenceOfTypeCannotBeAssignedToType, referenceId, currentType, typeToConvert));
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_YamlPropertyInfoIsBoundToDifferentYamlTypeInfo(YamlPropertyInfo propertyInfo)
        {
            Debug.Assert(propertyInfo.DeclaringTypeInfo is not null, "We should not throw this exception when ParentTypeInfo is null");
            throw new InvalidOperationException(SR.Format(SR.YamlPropertyInfoBoundToDifferentParent, propertyInfo.Name, propertyInfo.DeclaringTypeInfo.Type.FullName));
        }

        [DoesNotReturn]
        internal static void ThrowUnexpectedMetadataException(
            ReadOnlySpan<byte> propertyName,
            ref Utf8YamlReader reader,
            scoped ref ReadStack state)
        {

            MetadataPropertyName name = YamlSerializer.GetMetadataPropertyName(propertyName, state.Current.BaseYamlTypeInfo.PolymorphicTypeResolver);
            if (name != 0)
            {
                ThrowYamlException_MetadataUnexpectedProperty(propertyName, ref state);
            }
            else
            {
                ThrowYamlException_MetadataInvalidPropertyWithLeadingDollarSign(propertyName, ref state, reader);
            }
        }

        [DoesNotReturn]
        public static void ThrowNotSupportedException_NoMetadataForType(Type type, IYamlTypeInfoResolver? resolver)
        {
            throw new NotSupportedException(SR.Format(SR.NoMetadataForType, type, resolver?.ToString() ?? "<null>"));
        }

        public static NotSupportedException GetNotSupportedException_AmbiguousMetadataForType(Type type, Type match1, Type match2)
        {
            return new NotSupportedException(SR.Format(SR.AmbiguousMetadataForType, type, match1, match2));
        }

        [DoesNotReturn]
        public static void ThrowNotSupportedException_ConstructorContainsNullParameterNames(Type declaringType)
        {
            throw new NotSupportedException(SR.Format(SR.ConstructorContainsNullParameterNames, declaringType));
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_NoMetadataForType(Type type, IYamlTypeInfoResolver? resolver)
        {
            throw new InvalidOperationException(SR.Format(SR.NoMetadataForType, type, resolver?.ToString() ?? "<null>"));
        }

        public static Exception GetInvalidOperationException_NoMetadataForTypeProperties(IYamlTypeInfoResolver? resolver, Type type)
        {
            return new InvalidOperationException(SR.Format(SR.NoMetadataForTypeProperties, resolver?.ToString() ?? "<null>", type));
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_NoMetadataForTypeProperties(IYamlTypeInfoResolver? resolver, Type type)
        {
            throw GetInvalidOperationException_NoMetadataForTypeProperties(resolver, type);
        }

        [DoesNotReturn]
        public static void ThrowMissingMemberException_MissingFSharpCoreMember(string missingFsharpCoreMember)
        {
            throw new MissingMemberException(SR.Format(SR.MissingFSharpCoreMember, missingFsharpCoreMember));
        }

        [DoesNotReturn]
        public static void ThrowNotSupportedException_BaseConverterDoesNotSupportMetadata(Type derivedType)
        {
            throw new NotSupportedException(SR.Format(SR.Polymorphism_DerivedConverterDoesNotSupportMetadata, derivedType));
        }

        [DoesNotReturn]
        public static void ThrowNotSupportedException_DerivedConverterDoesNotSupportMetadata(Type derivedType)
        {
            throw new NotSupportedException(SR.Format(SR.Polymorphism_DerivedConverterDoesNotSupportMetadata, derivedType));
        }

        [DoesNotReturn]
        public static void ThrowNotSupportedException_RuntimeTypeNotSupported(Type baseType, Type runtimeType)
        {
            throw new NotSupportedException(SR.Format(SR.Polymorphism_RuntimeTypeNotSupported, runtimeType, baseType));
        }

        [DoesNotReturn]
        public static void ThrowNotSupportedException_RuntimeTypeDiamondAmbiguity(Type baseType, Type runtimeType, Type derivedType1, Type derivedType2)
        {
            throw new NotSupportedException(SR.Format(SR.Polymorphism_RuntimeTypeDiamondAmbiguity, runtimeType, derivedType1, derivedType2, baseType));
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_TypeDoesNotSupportPolymorphism(Type baseType)
        {
            throw new InvalidOperationException(SR.Format(SR.Polymorphism_TypeDoesNotSupportPolymorphism, baseType));
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_DerivedTypeNotSupported(Type baseType, Type derivedType)
        {
            throw new InvalidOperationException(SR.Format(SR.Polymorphism_DerivedTypeIsNotSupported, derivedType, baseType));
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_DerivedTypeIsAlreadySpecified(Type baseType, Type derivedType)
        {
            throw new InvalidOperationException(SR.Format(SR.Polymorphism_DerivedTypeIsAlreadySpecified, baseType, derivedType));
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_OpenGenericDerivedTypeCouldNotBeResolved(Type baseType, Type derivedType, string reason)
        {
            throw new InvalidOperationException(SR.Format(SR.Polymorphism_OpenGenericDerivedTypeCouldNotBeResolved, derivedType, baseType, reason));
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_TypeDicriminatorIdIsAlreadySpecified(Type baseType, object typeDiscriminator)
        {
            throw new InvalidOperationException(SR.Format(SR.Polymorphism_TypeDicriminatorIdIsAlreadySpecified, baseType, typeDiscriminator));
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_InferredDerivedTypeIsNotAccessible(Type baseType, Type derivedType)
        {
            throw new InvalidOperationException(SR.Format(SR.Polymorphism_InferredDerivedTypeIsNotAccessible, derivedType, baseType));
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_InferClosedTypePolymorphismOnNonClosedType(Type baseType)
        {
            throw new InvalidOperationException(SR.Format(SR.Polymorphism_InferClosedTypePolymorphismOnNonClosedType, baseType));
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_InvalidCustomTypeDiscriminatorPropertyName()
        {
            throw new InvalidOperationException(SR.Polymorphism_InvalidCustomTypeDiscriminatorPropertyName);
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_PropertyConflictsWithMetadataPropertyName(Type type, string propertyName)
        {
            throw new InvalidOperationException(SR.Format(SR.Polymorphism_PropertyConflictsWithMetadataPropertyName, type, propertyName));
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_PolymorphicTypeConfigurationDoesNotSpecifyDerivedTypes(Type baseType)
        {
            throw new InvalidOperationException(SR.Format(SR.Polymorphism_ConfigurationDoesNotSpecifyDerivedTypes, baseType));
        }

        [DoesNotReturn]
        public static void ThrowYamlException_UnionDoesNotAcceptNull(Type unionType)
        {
            ThrowYamlException(SR.Format(SR.UnionDoesNotAcceptNull, unionType));
        }

        [DoesNotReturn]
        public static void ThrowYamlException_UnionRuntimeTypeNotMatchedToCase(Type unionType, Type runtimeType)
        {
            ThrowYamlException(SR.Format(SR.UnionRuntimeTypeNotMatchedToCase, runtimeType, unionType));
        }

        [DoesNotReturn]
        public static void ThrowYamlException_UnionCannotCreateValue(Type unionType)
        {
            ThrowYamlException(SR.Format(SR.UnionCannotCreateValue, unionType));
        }

        [DoesNotReturn]
        public static void ThrowYamlException_UnionCannotReadValue(Type unionType)
        {
            ThrowYamlException(SR.Format(SR.UnionCannotReadValue, unionType));
        }

        [DoesNotReturn]
        public static void ThrowYamlException_UnionTypeClassifierReturnedNull(Type unionType, YamlTokenType tokenType)
        {
            ThrowYamlException(SR.Format(SR.UnionTypeClassifierReturnedNull, unionType, tokenType));
        }

        [DoesNotReturn]
        public static void ThrowYamlException_UnionAmbiguousYamlValueType(Type unionType, YamlValueType valueType)
        {
            ThrowYamlException(SR.Format(SR.UnionAmbiguousYamlValueType, valueType, unionType));
        }

        [DoesNotReturn]
        public static void ThrowYamlException_UnionYamlTokenTypeNotSupported(Type unionType, YamlTokenType tokenType)
        {
            ThrowYamlException(SR.Format(SR.UnionYamlTokenTypeNotSupported, tokenType, unionType));
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_UnionCasesNotPopulated(Type unionType)
        {
            throw new InvalidOperationException(SR.Format(SR.UnionCasesNotPopulated, unionType));
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_UnionCannotCreateValue(Type unionType)
        {
            throw new InvalidOperationException(SR.Format(SR.UnionCannotCreateValue, unionType));
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_UnionCannotReadValue(Type unionType)
        {
            throw new InvalidOperationException(SR.Format(SR.UnionCannotReadValue, unionType));
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_UnionTypeStructuralClassifierOnlyForUnions(Type unionType)
        {
            throw new InvalidOperationException(SR.Format(SR.UnionTypeStructuralClassifierOnlyForUnions, unionType));
        }

        [DoesNotReturn]
        public static void ThrowNotSupportedException_UnionTypeStructuralClassifierCaseNotSupported(
            Type unionType,
            Type caseType)
        {
            throw new NotSupportedException(
                SR.Format(
                    SR.UnionTypeStructuralClassifierCaseNotSupported,
                    unionType,
                    caseType));
        }

        [DoesNotReturn]
        public static void ThrowNotSupportedException_UnionTypeStructuralClassifierPreserveReferencesNotSupported(Type unionType)
        {
            throw new NotSupportedException(
                SR.Format(
                    SR.UnionTypeStructuralClassifierPreserveReferencesNotSupported,
                    unionType));
        }

        [DoesNotReturn]
        public static void ThrowNotSupportedException_UnionTypeStructuralClassifierAmbiguousCases(
            Type unionType,
            Type conflictingCaseType,
            Type caseType,
            YamlValueType valueType)
        {
            throw new NotSupportedException(
                SR.Format(
                    SR.UnionTypeStructuralClassifierAmbiguousCases,
                    unionType,
                    conflictingCaseType,
                    caseType,
                    valueType));
        }

        [DoesNotReturn]
        public static void ThrowNotSupportedException_UnionTypeStructuralClassifierUnreachableObjectCase(
            Type unionType,
            Type unreachableCaseType,
            Type shadowingCaseType)
        {
            throw new NotSupportedException(
                SR.Format(
                    SR.UnionTypeStructuralClassifierUnreachableObjectCase,
                    unionType,
                    unreachableCaseType,
                    shadowingCaseType));
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_TypeClassifierMustDeriveFromYamlTypeClassifierFactory(Type classifierType, Type type)
        {
            throw new InvalidOperationException(SR.Format(SR.TypeClassifierMustDeriveFromYamlTypeClassifierFactory, classifierType, type));
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_TypeClassifierNotSupported(Type classifierType, Type type)
        {
            throw new InvalidOperationException(SR.Format(SR.TypeClassifierNotSupported, classifierType, type));
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_UnsupportedEnumIdentifier(Type enumType, string? enumName)
        {
            throw new InvalidOperationException(SR.Format(SR.UnsupportedEnumIdentifier, enumType.Name, enumName));
        }

        [DoesNotReturn]
        public static void ThrowYamlException_UnrecognizedTypeDiscriminator(object typeDiscriminator)
        {
            ThrowYamlException(SR.Format(SR.Polymorphism_UnrecognizedTypeDiscriminator, typeDiscriminator));
        }

        [DoesNotReturn]
        public static void ThrowArgumentException_YamlPolymorphismOptionsAssociatedWithDifferentYamlTypeInfo(string parameterName)
        {
            throw new ArgumentException(SR.YamlPolymorphismOptionsAssociatedWithDifferentYamlTypeInfo, paramName: parameterName);
        }

        [DoesNotReturn]
        public static void ThrowOperationCanceledException_PipeWriteCanceled()
        {
            throw new OperationCanceledException(SR.PipeWriterCanceled);
        }

        [DoesNotReturn]
        public static void ThrowOperationCanceledException_PipeReadCanceled()
        {
            throw new OperationCanceledException(SR.PipeReaderCanceled);
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_PipeWriterDoesNotImplementUnflushedBytes(PipeWriter pipeWriter)
        {
            throw new InvalidOperationException(SR.Format(SR.PipeWriter_DoesNotImplementUnflushedBytes, pipeWriter.GetType().Name));
        }

        [DoesNotReturn]
        public static void ThrowNotSupportedException_YamlSchemaExporterDoesNotSupportReferenceHandlerPreserve()
        {
            throw new NotSupportedException(SR.YamlSchemaExporter_ReferenceHandlerPreserve_NotSupported);
        }

        [DoesNotReturn]
        public static void ThrowInvalidOperationException_YamlSchemaExporterDepthTooLarge()
        {
            throw new InvalidOperationException(SR.YamlSchemaExporter_DepthTooLarge);
        }
    }
}
