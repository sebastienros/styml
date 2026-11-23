// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Text.Yaml.Serialization.Metadata;

namespace System.Text.Yaml.Nodes
{
    /// <summary>
    /// A YamlValue that encapsulates arbitrary .NET type configurations.
    /// Paradoxically, instances of this type can be of any YamlValueKind
    /// (including objects and arrays) and introspecting these values is
    /// generally slower compared to the other YamlValue implementations.
    /// </summary>
    internal sealed class YamlValueCustomized<TValue> : YamlValue<TValue>
    {
        private readonly YamlTypeInfo<TValue> _yamlTypeInfo;
        private YamlValueKind? _valueKind;

        public YamlValueCustomized(TValue value, YamlTypeInfo<TValue> yamlTypeInfo, YamlNodeOptions? options = null): base(value, options)
        {
            Debug.Assert(yamlTypeInfo.IsConfigured);
            _yamlTypeInfo = yamlTypeInfo;
        }

        private protected override YamlValueKind GetValueKindCore() => _valueKind ??= ComputeValueKind();
        internal override YamlNode DeepCloneCore() => YamlSerializer.SerializeToNode(Value, _yamlTypeInfo)!;

        public override void WriteTo(Utf8YamlWriter writer, YamlSerializerOptions? options = null)
        {
            WritePropertiesTo(writer);
            ArgumentNullException.ThrowIfNull(writer);

            YamlTypeInfo<TValue> yamlTypeInfo = _yamlTypeInfo;

            if (options is not null && options != yamlTypeInfo.Options)
            {
                options.MakeReadOnly();
                yamlTypeInfo = (YamlTypeInfo<TValue>)options.GetTypeInfoInternal(typeof(TValue));
            }

            yamlTypeInfo.Serialize(writer, Value);
        }

        /// <summary>
        /// Computes the YamlValueKind of the value by serializing it and reading the resultant YAML.
        /// </summary>
        private YamlValueKind ComputeValueKind()
        {
            Utf8YamlWriter writer = Utf8YamlWriterCache.RentWriterAndBuffer(Utf8YamlWriterCache.PinnedDocumentOptions, YamlSerializerOptions.BufferSizeDefault, out PooledByteBufferWriter output);
            try
            {
                WriteTo(writer);
                writer.Flush();
                Utf8YamlReader reader = new(output.WrittenSpan);
                bool success = reader.Read();
                Debug.Assert(success);
                return YamlReaderHelper.ToValueKind(reader.TokenType);
            }
            finally
            {
                Utf8YamlWriterCache.ReturnWriterAndBuffer(writer, output);
            }
        }
    }
}
