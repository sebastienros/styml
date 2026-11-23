// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Yaml.Nodes;
using System.Text.Yaml.Schema;
using System.Text.Yaml.Serialization.Metadata;

namespace System.Text.Yaml.Serialization.Converters
{
    internal sealed class YamlObjectConverter : YamlConverter<YamlObject?>
    {
        internal override bool IsUntypedConverter => true;

        internal override void ConfigureYamlTypeInfo(YamlTypeInfo yamlTypeInfo, YamlSerializerOptions options)
        {
            yamlTypeInfo.CreateObjectForExtensionDataProperty = () => new YamlObject(options.GetNodeOptions());
        }

        internal override void ReadElementAndSetProperty(
            object obj,
            string propertyName,
            ref Utf8YamlReader reader,
            YamlSerializerOptions options,
            scoped ref ReadStack state)
        {
            bool success = YamlNodeConverter.Instance.TryRead(ref reader, typeof(YamlNode), options, ref state, out YamlNode? value, out _);
            Debug.Assert(success); // Node converters are not resumable.

            Debug.Assert(obj is YamlObject);
            YamlObject jObject = (YamlObject)obj;

            Debug.Assert(value is null || value is YamlNode);
            YamlNode? jNodeValue = value;

            HashSet<string>? documentKeys = YamlPropertyInfo.ExtensionDocumentKeys(jObject, ref state, options);
            if (documentKeys is not null)
            {
                // M5c: seeded extension data; a key that appears once overrides the seed.
                if (!documentKeys.Add(propertyName) && options.DuplicateKeyHandling == YamlDuplicateKeyHandling.Error)
                {
                    ThrowHelper.ThrowYamlException_DuplicatePropertyNotAllowed(propertyName);
                }

                jObject[propertyName] = jNodeValue;
            }
            else if (options.AllowDuplicateProperties)
            {
                jObject[propertyName] = jNodeValue;
            }
            else if (!jObject.TryAdd(propertyName, jNodeValue))
            {
                ThrowHelper.ThrowYamlException_DuplicatePropertyNotAllowed(propertyName);
            }
        }

        internal override void WriteExtensionDataValue(Utf8YamlWriter writer, YamlObject? value, YamlSerializerOptions options)
        {
            Debug.Assert(value is not null);
            value.WriteContentsTo(writer, options);
        }

        public override void Write(Utf8YamlWriter writer, YamlObject? value, YamlSerializerOptions options)
        {
            if (value is null)
            {
                writer.WriteNullValue();
                return;
            }

            value.WriteTo(writer, options);
        }

        public override YamlObject? Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
        {
            switch (reader.TokenType)
            {
                case YamlTokenType.StartObject:
                    return (YamlObject)YamlNodeConverter.ReadAsBuiltNode(ref reader, options)!;
                case YamlTokenType.Null:
                    return null;
                default:
                    throw ThrowHelper.GetInvalidOperationException_ExpectedObject(reader.TokenType);
            }
        }

        internal override YamlValueType GetSupportedYamlValueTypes(YamlNumberHandling _) => YamlValueType.Object;

        internal override YamlSchema? GetSchema(YamlNumberHandling _) => new() { Type = YamlSchemaType.Object };
    }
}
