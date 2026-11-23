// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Yaml.Reflection;
using System.Text.Yaml.Serialization;
using System.Text.Yaml.Serialization.Metadata;

namespace System.Text.Yaml
{
    public static partial class YamlSerializer
    {
        /// <summary>
        /// Lookup the property given its name (obtained from the reader) and return it.
        /// Also sets state.Current.YamlPropertyInfo to a non-null value.
        /// </summary>
        internal static YamlPropertyInfo LookupProperty(
            object? obj,
            ReadOnlySpan<byte> unescapedPropertyName,
            ref ReadStack state,
            YamlSerializerOptions options,
            out bool useExtensionProperty,
            bool createExtensionProperty = true)
        {
            YamlTypeInfo yamlTypeInfo = state.Current.YamlTypeInfo;
            useExtensionProperty = false;

            YamlPropertyInfo? yamlPropertyInfo = yamlTypeInfo.GetProperty(
                unescapedPropertyName,
                ref state.Current,
                out byte[] utf8PropertyName);

            // Increment PropertyIndex so GetProperty() checks the next property first when called again.
            state.Current.PropertyIndex++;

            // For case insensitive and missing property support of YamlPath, remember the value on the temporary stack.
            state.Current.YamlPropertyName = utf8PropertyName;

            // Handle missing properties
            if (yamlPropertyInfo is null)
            {
                if (yamlTypeInfo.EffectiveUnmappedMemberHandling is YamlUnmappedMemberHandling.Disallow)
                {
                    Debug.Assert(yamlTypeInfo.ExtensionDataProperty is null, "jsonTypeInfo.Configure() should have caught conflicting configuration.");
                    string stringPropertyName = Encoding.UTF8.GetString(unescapedPropertyName);
                    ThrowHelper.ThrowYamlException_UnmappedYamlProperty(yamlTypeInfo.Type, stringPropertyName);
                }

                // Determine if we should use the extension property.
                if (yamlTypeInfo.ExtensionDataProperty is YamlPropertyInfo { HasGetter: true, HasSetter: true } dataExtProperty)
                {
                    state.Current.YamlPropertyNameAsString = Encoding.UTF8.GetString(unescapedPropertyName);

                    if (createExtensionProperty)
                    {
                        Debug.Assert(obj is not null, "obj is null");
                        CreateExtensionDataProperty(obj, dataExtProperty, options);
                    }

                    yamlPropertyInfo = dataExtProperty;
                    useExtensionProperty = true;
                }
                else
                {
                    // Populate with a placeholder value required by YamlPath calculations
                    yamlPropertyInfo = YamlPropertyInfo.s_missingProperty;
                }
            }

            else if (options.DuplicateKeyHandling == YamlDuplicateKeyHandling.FirstWins && state.Current.IsPropertyAssigned(yamlPropertyInfo))
            {
                // M5c, FirstWins: a later occurrence takes the unmapped path,
                // so its value is read for syntax and skipped, not converted.
                yamlPropertyInfo = YamlPropertyInfo.s_missingProperty;
            }

            state.Current.YamlPropertyInfo = yamlPropertyInfo;
            state.Current.NumberHandling = yamlPropertyInfo.EffectiveNumberHandling;
            return yamlPropertyInfo;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ReadOnlySpan<byte> GetPropertyName(
            scoped ref ReadStack state,
            ref Utf8YamlReader reader,
            YamlSerializerOptions options,
            out bool isAlreadyReadMetadataProperty)
        {
            // M5b: an alias key (*k : v) names its anchored scalar's text. M5e: a collection key names no member.
            ReadOnlySpan<byte> propertyName = reader.YamlIsAliasToken
                ? YamlAliasReader.ReadAliasPropertyName(ref reader, options, state.StreamPosition)
                : reader.KeyKind == YamlKeyKind.Complex
                    ? YamlComplexKeys.MemberSiteName(ref state, ref reader, options)
                    : reader.GetUnescapedSpan();
            isAlreadyReadMetadataProperty = false;

            if (state.Current.CanContainMetadata)
            {
                if (IsMetadataPropertyName(propertyName, state.Current.BaseYamlTypeInfo.PolymorphicTypeResolver))
                {
                    if (options.AllowOutOfOrderMetadataProperties)
                    {
                        isAlreadyReadMetadataProperty = true;
                    }
                    else
                    {
                        ThrowHelper.ThrowUnexpectedMetadataException(propertyName, ref reader, ref state);
                    }
                }
            }

            return propertyName;
        }

        internal static void CreateExtensionDataProperty(
            object obj,
            YamlPropertyInfo yamlPropertyInfo,
            YamlSerializerOptions options)
        {
            Debug.Assert(yamlPropertyInfo is not null);

            object? extensionData = yamlPropertyInfo.GetValueAsObject(obj);

            // For IReadOnlyDictionary, if there's an existing non-null instance, we need to create a new mutable
            // Dictionary seeded with the existing contents so we can add the deserialized extension data to it.
            bool isReadOnlyDictionary = yamlPropertyInfo.PropertyType == typeof(IReadOnlyDictionary<string, object>) ||
                                        yamlPropertyInfo.PropertyType == typeof(IReadOnlyDictionary<string, YamlElement>);

            if (extensionData is null || (isReadOnlyDictionary && extensionData is not null))
            {
                // Create the appropriate dictionary type. We already verified the types.
#if DEBUG
                Type? underlyingIDictionaryType = yamlPropertyInfo.PropertyType.GetCompatibleGenericInterface(typeof(IDictionary<,>))
                    ?? yamlPropertyInfo.PropertyType.GetCompatibleGenericInterface(typeof(IReadOnlyDictionary<,>));
                Debug.Assert(underlyingIDictionaryType is not null);
                Type[] genericArgs = underlyingIDictionaryType.GetGenericArguments();

                Debug.Assert(underlyingIDictionaryType.IsGenericType);
                Debug.Assert(genericArgs.Length == 2);
                Debug.Assert(genericArgs[0].UnderlyingSystemType == typeof(string));
                Debug.Assert(
                    genericArgs[1].UnderlyingSystemType == YamlTypeInfo.ObjectType ||
                    genericArgs[1].UnderlyingSystemType == typeof(YamlElement) ||
                    genericArgs[1].UnderlyingSystemType == typeof(Nodes.YamlNode));
#endif

                Func<object>? createObjectForExtensionDataProp = yamlPropertyInfo.YamlTypeInfo.CreateObject
                    ?? yamlPropertyInfo.YamlTypeInfo.CreateObjectForExtensionDataProperty;

                if (createObjectForExtensionDataProp is null)
                {
                    // Avoid a reference to the YamlNode type for trimming
                    if (yamlPropertyInfo.PropertyType.FullName == YamlTypeInfo.YamlObjectTypeName)
                    {
                        ThrowHelper.ThrowInvalidOperationException_NodeYamlObjectCustomConverterNotAllowedOnExtensionProperty();
                    }
                    // For IReadOnlyDictionary<string, object> or IReadOnlyDictionary<string, YamlElement> interface types,
                    // create a Dictionary<TKey, TValue> instance seeded with any existing contents.
                    else if (yamlPropertyInfo.PropertyType == typeof(IReadOnlyDictionary<string, object>))
                    {
                        if (extensionData is not null)
                        {
                            var existing = (IReadOnlyDictionary<string, object>)extensionData;
                            var newDict = new Dictionary<string, object>();
                            foreach (KeyValuePair<string, object> kvp in existing)
                            {
                                newDict[kvp.Key] = kvp.Value;
                            }
                            extensionData = newDict;
                        }
                        else
                        {
                            extensionData = new Dictionary<string, object>();
                        }
                        Debug.Assert(yamlPropertyInfo.Set is not null);
                        yamlPropertyInfo.Set(obj, extensionData);
                        return;
                    }
                    else if (yamlPropertyInfo.PropertyType == typeof(IReadOnlyDictionary<string, YamlElement>))
                    {
                        if (extensionData is not null)
                        {
                            var existing = (IReadOnlyDictionary<string, YamlElement>)extensionData;
                            var newDict = new Dictionary<string, YamlElement>();
                            foreach (KeyValuePair<string, YamlElement> kvp in existing)
                            {
                                newDict[kvp.Key] = kvp.Value;
                            }
                            extensionData = newDict;
                        }
                        else
                        {
                            extensionData = new Dictionary<string, YamlElement>();
                        }
                        Debug.Assert(yamlPropertyInfo.Set is not null);
                        yamlPropertyInfo.Set(obj, extensionData);
                        return;
                    }
                    else
                    {
                        ThrowHelper.ThrowNotSupportedException_SerializationNotSupported(yamlPropertyInfo.PropertyType);
                    }
                }

                extensionData = createObjectForExtensionDataProp();
                Debug.Assert(yamlPropertyInfo.Set is not null);
                yamlPropertyInfo.Set(obj, extensionData);
            }

            // We don't add the value to the dictionary here because we need to support the read-ahead functionality for Streams.
        }
    }
}
