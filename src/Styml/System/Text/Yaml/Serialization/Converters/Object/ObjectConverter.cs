// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Text.Yaml.Nodes;
using System.Text.Yaml.Schema;
using System.Text.Yaml.Serialization.Metadata;

namespace System.Text.Yaml.Serialization.Converters
{
    internal abstract class ObjectConverter : YamlConverter<object?>
    {
        internal override bool IsUntypedConverter => true;

        private protected override ConverterStrategy GetDefaultConverterStrategy() => ConverterStrategy.Object;

        public ObjectConverter()
        {
            CanBePolymorphic = true;
        }

        public sealed override object ReadAsPropertyName(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options) =>
            ReadAsPropertyNameCore(ref reader, typeToConvert, options);

        internal sealed override object ReadAsPropertyNameCore(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
        {
            // M5e (decided under the parity rule): a scalar key read into an
            // object key is its text, as both libraries read it.
            return reader.GetString()!;
        }

        public sealed override void Write(Utf8YamlWriter writer, object? value, YamlSerializerOptions options)
        {
            if (value is null)
            {
                writer.WriteNullValue();
                return;
            }

            writer.WriteStartObject();
            writer.WriteEndObject();
        }

        public sealed override void WriteAsPropertyName(Utf8YamlWriter writer, object value, YamlSerializerOptions options)
        {
            WriteAsPropertyNameCore(writer, value, options, isWritingExtensionDataProperty: false);
        }

        internal sealed override void WriteAsPropertyNameCore(Utf8YamlWriter writer, object value, YamlSerializerOptions options, bool isWritingExtensionDataProperty)
        {
            ArgumentNullException.ThrowIfNull(value);

            Type runtimeType = value.GetType();
            if (runtimeType == Type)
            {
                ThrowHelper.ThrowNotSupportedException_DictionaryKeyTypeNotSupported(runtimeType, this);
            }

            YamlConverter runtimeConverter = options.GetConverterInternal(runtimeType);
            runtimeConverter.WriteAsPropertyNameCoreAsObject(writer, value, options, isWritingExtensionDataProperty);
        }
    }

    /// <summary>
    /// Defines an object converter that only supports (polymorphic) serialization but not deserialization.
    /// This is done to avoid rooting dependencies to YamlNode/YamlElement necessary to drive object deserialization.
    /// Source generator users need to explicitly declare support for object so that the derived converter gets used.
    /// </summary>
    internal sealed class SlimObjectConverter : ObjectConverter
    {
        // Keep track of the originating resolver so that the converter surfaces
        // an accurate error message whenever deserialization is attempted.
        private readonly IYamlTypeInfoResolver _originatingResolver;

        public SlimObjectConverter(IYamlTypeInfoResolver originatingResolver)
            => _originatingResolver = originatingResolver;

        public override object? Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
        {
            ThrowHelper.ThrowNotSupportedException_NoMetadataForType(typeToConvert, _originatingResolver);
            return null;
        }
    }

    /// <summary>
    /// Defines an object converter that supports deserialization via YamlElement/YamlNode representations.
    /// Used as the default in reflection or if object is declared in the YamlSerializerContext type graph.
    /// </summary>
    internal sealed class DefaultObjectConverter : ObjectConverter
    {
        /// <summary>
        /// M5d: a tag <c>TagMappings</c> names reads its type, through the
        /// options' contract for it (never by name); an unmapped tag keeps the element, with its tag.
        /// </summary>
        private static bool TryReadMapped(ref Utf8YamlReader reader, YamlSerializerOptions options, out object? value)
        {
            Span<byte> buffer = stackalloc byte[128];
            ReadOnlySpan<byte> tag = YamlTags.ExpandedApplicationTag(in reader, buffer, out _);
            if (!tag.IsEmpty && options.TagMappingTable!.Find(tag) is Type type && type != typeof(object))
            {
                value = YamlSerializer.ReadAsObject(ref reader, options.GetTypeInfoInternal(type));
                return true;
            }

            value = null;
            return false;
        }

        public DefaultObjectConverter()
        {
            // YamlElement/YamlNode parsing does not support async; force read ahead for now.
            RequiresReadAhead = true;
        }

        public override object? Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
        {
            if (options.HasTagMappings && !reader.Tag.IsEmpty && TryReadMapped(ref reader, options, out object? mapped))
            {
                return mapped;
            }

            if (options.UnknownTypeHandling == YamlUnknownTypeHandling.YamlElement)
            {
                return YamlAliasReader.ParseElement(ref reader, options.DuplicateKeyHandling, options);
            }

            Debug.Assert(options.UnknownTypeHandling == YamlUnknownTypeHandling.YamlNode);
            return YamlNodeConverter.Instance.Read(ref reader, typeToConvert, options);
        }

        internal override bool OnTryRead(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options, scoped ref ReadStack state, out object? value)
        {
            object? referenceValue;

            if (options.HasTagMappings && !reader.Tag.IsEmpty && TryReadMapped(ref reader, options, out value))
            {
                return true;
            }

            if (options.UnknownTypeHandling == YamlUnknownTypeHandling.YamlElement)
            {
                YamlElement element = YamlAliasReader.ParseElement(ref reader, options.DuplicateKeyHandling, options);

                // Edge case where we want to lookup for a reference when parsing into typeof(object)
                if (options.ReferenceHandlingStrategy == YamlKnownReferenceHandler.Preserve &&
                    YamlSerializer.TryHandleReferenceFromYamlElement(ref reader, ref state, element, out referenceValue))
                {
                    value = referenceValue;
                }
                else
                {
                    value = element;
                }

                return true;
            }

            Debug.Assert(options.UnknownTypeHandling == YamlUnknownTypeHandling.YamlNode);

            YamlNode? node = YamlNodeConverter.Instance.Read(ref reader, typeToConvert, options);

            if (options.ReferenceHandlingStrategy == YamlKnownReferenceHandler.Preserve &&
                YamlSerializer.TryHandleReferenceFromYamlNode(ref reader, ref state, node, out referenceValue))
            {
                value = referenceValue;
            }
            else
            {
                value = node;
            }

            return true;
        }

        internal override YamlSchema? GetSchema(YamlNumberHandling _) => YamlSchema.CreateTrueSchema();
    }
}
