// Copyright (c) Sébastien Ros. Licensed under the MIT license.

namespace System.Text.Yaml.Serialization
{
    /// <summary>
    /// The scalar style in which the member's string value is written in block style, as YamlDotNet's
    /// <c>[YamlMember(ScalarStyle = …)]</c>.
    /// </summary>
    /// <remarks>
    /// The style applies to the member's own value when it is written as a string (a string, a character, a date, a
    /// <see cref="Guid"/>, an enum written as its name…), where that style holds the value and reads back as the same
    /// string; otherwise the writer's default choice is written, so the value never reads differently. It does not
    /// reach a number, a boolean or a null, a collection's items, a nested object, or keys, and it is ignored in
    /// JSON-compatible style.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
    public sealed class YamlScalarStyleAttribute : YamlAttribute
    {
        /// <summary>
        /// Initializes a new instance of <see cref="YamlScalarStyleAttribute"/> with the style.
        /// </summary>
        /// <param name="style">
        /// <see cref="YamlScalarStyle.Plain"/>, <see cref="YamlScalarStyle.SingleQuoted"/>,
        /// <see cref="YamlScalarStyle.DoubleQuoted"/>, <see cref="YamlScalarStyle.Literal"/> or
        /// <see cref="YamlScalarStyle.Folded"/>; another value is an <see cref="InvalidOperationException"/> when the
        /// contract is configured.
        /// </param>
        public YamlScalarStyleAttribute(YamlScalarStyle style)
        {
            Style = style;
        }

        /// <summary>
        /// The scalar style.
        /// </summary>
        public YamlScalarStyle Style { get; }
    }
}
