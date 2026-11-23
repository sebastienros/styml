// Copyright (c) Sébastien Ros. Licensed under the MIT license.

namespace System.Text.Yaml.Serialization
{
    /// <summary>
    /// Adds an entry to the source-generated options' <c>YamlSerializerOptions.TagMappings</c>: the YAML tag
    /// given first reads and writes the type given second, which the context then also generates.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
#if BUILDING_SOURCE_GENERATOR
    internal
#else
    public
#endif
    sealed class YamlTagMappingAttribute : YamlAttribute
    {
        /// <summary>Initializes a new instance with the tag and the type it selects.</summary>
        /// <param name="tag">A local tag (<c>!dog</c>) or a global tag URI (<c>tag:example.com,2000:dog</c>).</param>
        /// <param name="type">The type the tag selects.</param>
        public YamlTagMappingAttribute(string tag, Type type)
        {
            Tag = tag;
            Type = type;
        }

        /// <summary>The tag.</summary>
        public string Tag { get; }

        /// <summary>The type the tag selects.</summary>
        public Type Type { get; }
    }
}
