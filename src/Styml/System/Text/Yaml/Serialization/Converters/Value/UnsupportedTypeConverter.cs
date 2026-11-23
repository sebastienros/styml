// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Yaml.Schema;
using System.Text.Yaml.Nodes;

namespace System.Text.Yaml.Serialization.Converters
{
    internal sealed class UnsupportedTypeConverter<T> : YamlConverter<T>
    {
        private readonly string? _errorMessage;

        public UnsupportedTypeConverter(string? errorMessage = null) => _errorMessage = errorMessage;

        public string ErrorMessage => _errorMessage ?? SR.Format(SR.SerializeTypeInstanceNotSupported, typeof(T).FullName);

        public override T Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options) =>
            throw new NotSupportedException(ErrorMessage);

        public override void Write(Utf8YamlWriter writer, T value, YamlSerializerOptions options) =>
            throw new NotSupportedException(ErrorMessage);

        internal override YamlSchema? GetSchema(YamlNumberHandling _) =>
            new YamlSchema { Comment = "Unsupported .NET type", Not = YamlSchema.CreateTrueSchema() };
    }
}
