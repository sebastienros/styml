// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Text.Yaml.Serialization
{
    /// <summary>
    /// Base class for non-enumerable, non-primitive objects where public properties
    /// are (de)serialized as a YAML object.
    /// </summary>
    internal abstract class YamlObjectConverter<T> : YamlResumableConverter<T>
    {
        private protected sealed override ConverterStrategy GetDefaultConverterStrategy() => ConverterStrategy.Object;
        internal sealed override YamlValueType GetSupportedYamlValueTypes(YamlNumberHandling _) => YamlValueType.Object;
        internal override bool CanPopulate => true;
    }
}
