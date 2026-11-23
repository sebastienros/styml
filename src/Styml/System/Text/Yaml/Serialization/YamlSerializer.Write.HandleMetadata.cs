// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Text.Yaml.Serialization.Metadata;
using System.Text.Yaml.Serialization;

namespace System.Text.Yaml
{
    public static partial class YamlSerializer
    {
        /// <summary>
        /// M5d: the tag of a polymorphic value in block style. A hierarchy under
        /// <c>Tag</c> or <c>Both</c> writes its derived type's tag, and under <c>Tag</c> no <c>$type</c> (so a collection
        /// has no <c>$type</c>/<c>$values</c> wrapper); an <c>object</c> site writes the mapped tag of the runtime type.
        /// The JSON-compatible writer never gets here: it keeps <c>$type</c>.
        /// </summary>
        internal static void WritePolymorphicTag(Utf8YamlWriter writer, object value, YamlConverter polymorphicConverter, YamlTypeInfo yamlTypeInfo, YamlSerializerOptions options, ref WriteStack state)
        {
            if (yamlTypeInfo.PolymorphicTypeResolver is { ReadsTags: true } resolver)
            {
                if (resolver.WrittenTagOf(polymorphicConverter.Type!) is string tag)
                {
                    writer.WriteTag(tag);
                    if (resolver.Style == YamlTypeDiscriminatorStyle.Tag)
                    {
                        state.PolymorphicTypeDiscriminator = null;
                        state.PolymorphicTypeResolver = null;
                    }
                }
            }
            else if (yamlTypeInfo.Type == typeof(object) && options.TagMappingTable?.WrittenTagOf(value.GetType()) is string mapped)
            {
                writer.WriteTag(mapped);
            }
        }

        // Pre-encoded metadata properties.
        internal static readonly YamlEncodedText s_metadataId = YamlEncodedText.Encode(IdPropertyName, encoder: null);
        internal static readonly YamlEncodedText s_metadataRef = YamlEncodedText.Encode(RefPropertyName, encoder: null);
        internal static readonly YamlEncodedText s_metadataType = YamlEncodedText.Encode(TypePropertyName, encoder: null);
        internal static readonly YamlEncodedText s_metadataValues = YamlEncodedText.Encode(ValuesPropertyName, encoder: null);

        internal static MetadataPropertyName WriteMetadataForObject(
            YamlConverter yamlConverter,
            ref WriteStack state,
            Utf8YamlWriter writer)
        {
            Debug.Assert(yamlConverter.CanHaveMetadata);
            Debug.Assert(!state.IsContinuation);
            Debug.Assert(state.CurrentContainsMetadata);

            MetadataPropertyName writtenMetadata = MetadataPropertyName.None;

            if (state.NewReferenceId is not null)
            {
                writer.WriteString(s_metadataId, state.NewReferenceId);
                writtenMetadata |= MetadataPropertyName.Id;
                state.NewReferenceId = null;
            }

            if (state.PolymorphicTypeDiscriminator is object discriminator)
            {
                Debug.Assert(state.PolymorphicTypeResolver is not null);

                YamlEncodedText propertyName =
                    state.PolymorphicTypeResolver.CustomTypeDiscriminatorPropertyNameYamlEncoded is YamlEncodedText customPropertyName
                    ? customPropertyName
                    : s_metadataType;

                if (discriminator is string stringId)
                {
                    writer.WriteString(propertyName, stringId);
                }
                else
                {
                    Debug.Assert(discriminator is int);
                    writer.WriteNumber(propertyName, (int)discriminator);
                }

                writtenMetadata |= MetadataPropertyName.Type;
                state.PolymorphicTypeDiscriminator = null;
            }

            Debug.Assert(writtenMetadata != MetadataPropertyName.None);
            return writtenMetadata;
        }

        internal static MetadataPropertyName WriteMetadataForCollection(
            YamlConverter yamlConverter,
            ref WriteStack state,
            Utf8YamlWriter writer)
        {
            // For collections with metadata, we nest the array payload within a JSON object.
            writer.WriteStartObject();
            MetadataPropertyName writtenMetadata = WriteMetadataForObject(yamlConverter, ref state, writer);
            writer.WritePropertyName(s_metadataValues); // property name containing nested array values.
            return writtenMetadata;
        }

        /// <summary>
        /// Compute reference id for the next value to be serialized.
        /// </summary>
        internal static bool TryGetReferenceForValue(object currentValue, ref WriteStack state, Utf8YamlWriter writer)
        {
            Debug.Assert(state.NewReferenceId is null);

            string referenceId = state.ReferenceResolver.GetReference(currentValue, out bool alreadyExists);
            Debug.Assert(referenceId is not null);

            if (alreadyExists)
            {
                // Instance already serialized, write as { "$ref" : "referenceId" }
                writer.WriteStartObject();
                writer.WriteString(s_metadataRef, referenceId);
                writer.WriteEndObject();

                // clear out any polymorphism state.
                state.PolymorphicTypeDiscriminator = null;
                state.PolymorphicTypeResolver = null;
            }
            else
            {
                // New instance, store computed reference id in the state
                state.NewReferenceId = referenceId;
            }

            return alreadyExists;
        }
    }
}
