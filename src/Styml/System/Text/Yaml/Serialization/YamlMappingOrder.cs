// Copyright (c) Sébastien Ros. Licensed under the MIT license.

namespace System.Text.Yaml.Serialization
{
    /// <summary>
    /// The order in which the serializer writes an object's members (<see cref="YamlSerializerOptions.MappingOrder"/>),
    /// as SharpYaml's <c>MappingOrder</c>.
    /// </summary>
    public enum YamlMappingOrder
    {
        /// <summary>
        /// The members' declaration order, as arranged by <see cref="YamlPropertyOrderAttribute"/>. The default.
        /// </summary>
        Declaration = 0,

        /// <summary>
        /// By <see cref="YamlPropertyOrderAttribute"/>'s order, then by the written name, ordinal. The extension data's
        /// entries follow the members, sorted among themselves; a type discriminator stays first. Dictionaries keep their
        /// own order. Reading is unaffected.
        /// </summary>
        Sorted = 1,
    }
}
