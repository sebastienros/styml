// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.Yaml.Serialization;
using System.Text.Yaml.Serialization.Metadata;

namespace System.Text.Yaml.Nodes
{
    /// <summary>
    /// Represents a mutable YAML value.
    /// </summary>
    public abstract partial class YamlValue : YamlNode
    {
        internal const string CreateUnreferencedCodeMessage = "Creating YamlValue instances with non-primitive types is not compatible with trimming. It can result in non-primitive types being serialized, which may have their members trimmed.";
        internal const string CreateDynamicCodeMessage = "Creating YamlValue instances with non-primitive types requires generating code at runtime.";

        private protected YamlValue(YamlNodeOptions? options) : base(options) { }

        /// <summary>
        ///   Tries to obtain the current YAML value and returns a value that indicates whether the operation succeeded.
        /// </summary>
        /// <remarks>
        ///   {T} can be the type or base type of the underlying value.
        ///   If the underlying value is a <see cref="YamlElement"/> then {T} can also be the type of any primitive
        ///   value supported by current <see cref="YamlElement"/>.
        ///   Specifying the <see cref="object"/> type for {T} will always succeed and return the underlying value as <see cref="object"/>.<br />
        ///   The underlying value of a <see cref="YamlValue"/> after deserialization is an instance of <see cref="YamlElement"/>,
        ///   otherwise it's the value specified when the <see cref="YamlValue"/> was created.
        /// </remarks>
        /// <seealso cref="YamlNode.GetValue{T}"></seealso>
        /// <typeparam name="T">The type of value to obtain.</typeparam>
        /// <param name="value">When this method returns, contains the parsed value.</param>
        /// <returns><see langword="true"/> if the value can be successfully obtained; otherwise, <see langword="false"/>.</returns>
        public abstract bool TryGetValue<T>([NotNullWhen(true)] out T? value);

        /// <summary>
        ///   Initializes a new instance of the <see cref="YamlValue"/> class that contains the specified value.
        /// </summary>
        /// <returns>
        ///   The new instance of the <see cref="YamlValue"/> class that contains the specified value.
        /// </returns>
        /// <typeparam name="T">The type of value to create.</typeparam>
        /// <param name="value">The value to create.</param>
        /// <param name="options">Options to control the behavior.</param>
        /// <returns>The new instance of the <see cref="YamlValue"/> class that contains the specified value.</returns>
        [RequiresUnreferencedCode(CreateUnreferencedCodeMessage + " Use the overload that takes a YamlTypeInfo, or make sure all of the required types are preserved.")]
        [RequiresDynamicCode(CreateDynamicCodeMessage)]
        public static YamlValue? Create<T>(T? value, YamlNodeOptions? options = null)
        {
            if (value is null)
            {
                return null;
            }

            if (value is YamlNode)
            {
                ThrowHelper.ThrowArgumentException_NodeValueNotAllowed(nameof(value));
            }

            if (value is YamlElement element)
            {
                return CreateFromElement(ref element, options);
            }

            var yamlTypeInfo = YamlSerializerOptions.Default.GetTypeInfo<T>();
            return CreateFromTypeInfo(value, yamlTypeInfo, options);
        }

        /// <summary>
        ///   Initializes a new instance of the <see cref="YamlValue"/> class that contains the specified value.
        /// </summary>
        /// <returns>
        ///   The new instance of the <see cref="YamlValue"/> class that contains the specified value.
        /// </returns>
        /// <typeparam name="T">The type of value to create.</typeparam>
        /// <param name="value">The value to create.</param>
        /// <param name="yamlTypeInfo">The <see cref="YamlTypeInfo"/> that will be used to serialize the value.</param>
        /// <param name="options">Options to control the behavior.</param>
        /// <returns>The new instance of the <see cref="YamlValue"/> class that contains the specified value.</returns>
        public static YamlValue? Create<T>(T? value, YamlTypeInfo<T> yamlTypeInfo, YamlNodeOptions? options = null)
        {
            ArgumentNullException.ThrowIfNull(yamlTypeInfo);

            if (value is null)
            {
                return null;
            }

            if (value is YamlNode)
            {
                ThrowHelper.ThrowArgumentException_NodeValueNotAllowed(nameof(value));
            }

            yamlTypeInfo.EnsureConfigured();

            if (value is YamlElement element && yamlTypeInfo.EffectiveConverter.IsInternalConverter)
            {
                return CreateFromElement(ref element, options);
            }

            return CreateFromTypeInfo(value, yamlTypeInfo, options);
        }

        internal override bool DeepEqualsCore(YamlNode otherNode)
        {
            if (GetValueKind() != otherNode.GetValueKind())
            {
                return false;
            }

            // Fall back to slow path that converts the nodes to YamlElement.
            YamlElement thisElement = ToYamlElement(this, out YamlDocument? thisDocument);
            YamlElement otherElement = ToYamlElement(otherNode, out YamlDocument? otherDocument);
            try
            {
                return YamlElement.DeepEquals(thisElement, otherElement);
            }
            finally
            {
                thisDocument?.Dispose();
                otherDocument?.Dispose();
            }

            static YamlElement ToYamlElement(YamlNode node, out YamlDocument? backingDocument)
            {
                if (node.UnderlyingElement is { } element)
                {
                    backingDocument = null;
                    return element;
                }

                Utf8YamlWriter writer = Utf8YamlWriterCache.RentWriterAndBuffer(
                    Utf8YamlWriterCache.PinnedDocumentOptions,
                    YamlSerializerOptions.BufferSizeDefault,
                    out PooledByteBufferWriter output);

                try
                {
                    node.WriteTo(writer);
                    writer.Flush();
                    Utf8YamlReader reader = new(output.WrittenSpan);
                    backingDocument = YamlDocument.ParseValue(ref reader);
                    return backingDocument.RootElement;
                }
                finally
                {
                    Utf8YamlWriterCache.ReturnWriterAndBuffer(writer, output);
                }
            }
        }

        internal sealed override void GetPath(ref ValueStringBuilder path, YamlNode? child)
        {
            Debug.Assert(child is null);

            Parent?.GetPath(ref path, this);
        }

        internal static YamlValue CreateFromTypeInfo<T>(T value, YamlTypeInfo<T> yamlTypeInfo, YamlNodeOptions? options = null)
        {
            Debug.Assert(yamlTypeInfo.IsConfigured);
            Debug.Assert(value is not null);

            if (YamlValue<T>.TypeIsSupportedPrimitive &&
                yamlTypeInfo is { EffectiveConverter.IsInternalConverter: true } &&
                (yamlTypeInfo.EffectiveNumberHandling & YamlNumberHandling.WriteAsString) is 0)
            {
                // If the type is using the built-in converter for a known primitive,
                // switch to the more efficient YamlValuePrimitive<T> implementation.
                return new YamlValuePrimitive<T>(value, yamlTypeInfo.EffectiveConverter, options);
            }

            return new YamlValueCustomized<T>(value, yamlTypeInfo, options);
        }

        internal static YamlValue? CreateFromElement(ref readonly YamlElement element, YamlNodeOptions? options = null)
        {
            switch (element.ValueKind)
            {
                case YamlValueKind.Null:
                    return null;

                case YamlValueKind.Object or YamlValueKind.Array:
                    // Force usage of YamlArray and YamlObject instead of supporting those in an YamlValue.
                    ThrowHelper.ThrowInvalidOperationException_NodeElementCannotBeObjectOrArray();
                    return null;

                default:
                    return new YamlValueOfElement(element, options);
            }
        }
    }
}
