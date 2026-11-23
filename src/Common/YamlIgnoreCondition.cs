// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Text.Yaml.Serialization
{
    /// <summary>
    /// When specified on <see cref="YamlSerializerOptions.DefaultIgnoreCondition"/>,
    /// determines when properties and fields across the type graph are ignored.
    /// When specified on <see cref="YamlIgnoreAttribute.Condition"/>, controls whether
    /// a property or field is ignored during serialization and deserialization. This option
    /// overrides the setting on <see cref="YamlSerializerOptions.DefaultIgnoreCondition"/>.
    /// </summary>
    public enum YamlIgnoreCondition
    {
        /// <summary>
        /// Property is never ignored during serialization or deserialization.
        /// </summary>
        Never = 0,
        /// <summary>
        /// Property is always ignored during serialization and deserialization.
        /// </summary>
        Always = 1,
        /// <summary>
        /// If the value is the default, the property is ignored during serialization.
        /// This is applied to both reference and value-type properties and fields.
        /// </summary>
        WhenWritingDefault = 2,
        /// <summary>
        /// If the value is <see langword="null"/>, the property is ignored during serialization.
        /// This is applied only to reference-type properties and fields.
        /// </summary>
        WhenWritingNull = 3,
        /// <summary>
        /// Property is ignored during serialization
        /// </summary>
        WhenWriting = 4,
        /// <summary>
        /// Property is ignored during deserialization
        /// </summary>
        WhenReading = 5,
        /// <summary>
        /// If the value is <see langword="null"/> or an empty collection, the property is ignored during serialization
        /// as YamlDotNet's <c>OmitNull | OmitEmptyCollections</c>. A
        /// collection is empty when its count, known without enumerating it, is 0 (an array, an <c>ICollection</c>, an
        /// <c>IReadOnlyCollection&lt;T&gt;</c>, a default or empty <c>ImmutableArray&lt;T&gt;</c>); a collection with no known
        /// count is written. An <see langword="object"/> member is judged by its value's runtime type. On a member that is
        /// not a collection this acts as <see cref="WhenWritingNull"/>. An omitted member is not in the output, so on read
        /// it keeps what the constructor or its initializer gave it.
        /// </summary>
        WhenWritingEmptyCollection = 6,
    }
}
