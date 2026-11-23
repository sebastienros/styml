// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

namespace System.Text.Yaml.Serialization
{
    /// <summary>
    /// This class defines how the <see cref="YamlSerializer"/> deals with references on serialization and deserialization.
    /// </summary>
    public abstract class ReferenceHandler
    {
        /// <summary>
        /// Indicates whether this ReferenceHandler implementation should use <see cref="YamlKnownReferenceHandler.Preserve"/> semantics or <see cref="YamlKnownReferenceHandler.IgnoreCycles"/> semantics.
        /// The defualt is Preserve.
        /// </summary>
        internal YamlKnownReferenceHandler HandlingStrategy = YamlKnownReferenceHandler.Preserve;

        /// <summary>
        /// How a Preserve-strategy handler writes references in block style (M5b): the built-in handlers write anchors and
        /// aliases; a custom handler keeps <c>$id</c>/<c>$ref</c>/<c>$values</c> with its resolver's contract.
        /// </summary>
        internal YamlAnchorMode AnchorMode;

        /// <summary>
        /// Metadata properties will be honored when deserializing YAML objects and arrays into reference types and written when serializing reference types. This is necessary to create round-trippable YAML from objects that contain cycles or duplicate references.
        /// </summary>
        /// <remarks>
        /// In YAML block style (M5b), an object or collection referenced
        /// more than once is written once with an anchor (<c>&amp;1</c>), and each later reference as an alias
        /// (<c>*1</c>); a first pass over the graph finds the repeated objects. <see cref="PreserveAll"/> anchors every
        /// object instead. On read, aliases are read with any handler; with this one, an alias inside the node it refers
        /// to builds the reference cycle. The <c>$id</c>/<c>$ref</c> description below is the JSON-compatible style's
        /// output, which both handlers also read in any style.
        /// * On Serialize:
        /// When writing complex reference types, the serializer also writes metadata properties (`$id`, `$values`, and `$ref`) within them.
        /// The output YAML will contain an extra `$id` property for every object, and for every enumerable type the YAML array emitted will be nested within a YAML object containing an `$id` and `$values` property.
        /// <see cref="object.ReferenceEquals(object?, object?)"/> is used to determine whether objects are identical.
        /// When an object is identical to a previously serialized one, a pointer (`$ref`) to the identifier (`$id`) of such object is written instead.
        /// No metadata properties are written for value types.
        /// * On Deserialize:
        /// The metadata properties within the YAML that are used to preserve duplicated references and cycles will be honored as long as they are well-formed**.
        /// For YAML objects that don't contain any metadata properties, the deserialization behavior is identical to <see langword="null"/>.
        /// For value types:
        ///   * The `$id` metadata property is ignored.
        ///   * A <see cref="YamlException"/> is thrown if a `$ref` metadata property is found within the YAML object.
        ///   * For enumerable value types, the `$values` metadata property is ignored.
        /// ** For the metadata properties within the YAML to be considered well-formed, they must follow these rules:
        ///   1) The `$id` metadata property must be the first property in the YAML object.
        ///   2) A YAML object that contains a `$ref` metadata property must not contain any other properties.
        ///   3) The value of the `$ref` metadata property must refer to an `$id` that has appeared earlier in the YAML.
        ///   4) The value of the `$id` and `$ref` metadata properties must be a YAML string.
        ///   5) For enumerable types, such as <see cref="List{T}"/>, the YAML array must be nested within a YAML object containing an `$id` and `$values` metadata property, in that order.
        ///   6) For enumerable types, the `$values` metadata property must be a YAML array.
        ///   7) The `$values` metadata property is only valid when referring to enumerable types.
        /// If the YAML is not well-formed, a <see cref="YamlException"/> is thrown.
        /// </remarks>
        public static ReferenceHandler Preserve { get; } = new PreserveReferenceHandler(YamlAnchorMode.Repeated);

        /// <summary>
        /// Writes YAML anchors and aliases for every object and collection, the root included, in one pass, and reads
        /// aliases and System.Text.Json's reference metadata as <see cref="Preserve"/> does.
        /// </summary>
        /// <remarks>
        /// <see cref="Preserve"/> anchors only the objects referenced more than once, after a first pass over the graph.
        /// In the JSON-compatible style, which has no anchors, both write <c>$id</c>, <c>$ref</c> and <c>$values</c> for
        /// every object, as System.Text.Json does.
        /// </remarks>
        public static ReferenceHandler PreserveAll { get; } = new PreserveReferenceHandler(YamlAnchorMode.All);

        /// <summary>
        /// Ignores an object when a reference cycle is detected during serialization.
        /// </summary>
        public static ReferenceHandler IgnoreCycles { get; } = new IgnoreReferenceHandler();

        /// <summary>
        /// Returns the <see cref="ReferenceResolver "/> used for each serialization call.
        /// </summary>
        /// <returns>The resolver to use for serialization and deserialization.</returns>
        public abstract ReferenceResolver CreateResolver();

        /// <summary>
        /// Optimization for the resolver used when <see cref="Preserve"/> is set in <see cref="YamlSerializerOptions.ReferenceHandler"/>;
        /// we pass a flag signaling whether this is called from serialization or deserialization to save one dictionary instantiation.
        /// </summary>
        internal virtual ReferenceResolver CreateResolver(bool writing) => CreateResolver();
    }

    /// <summary>How a Preserve-strategy reference handler writes references in block style (M5b).</summary>
    internal enum YamlAnchorMode : byte
    {
        /// <summary><c>$id</c>, <c>$ref</c> and <c>$values</c>: a custom handler.</summary>
        None,

        /// <summary>Anchors for objects referenced more than once: <see cref="ReferenceHandler.Preserve"/>.</summary>
        Repeated,

        /// <summary>Anchors for every object: <see cref="ReferenceHandler.PreserveAll"/>.</summary>
        All,
    }
}
