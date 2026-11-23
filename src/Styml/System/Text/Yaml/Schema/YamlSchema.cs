// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Yaml.Nodes;

namespace System.Text.Yaml.Schema
{
    internal sealed class YamlSchema
    {
        internal const string RefPropertyName = "$ref";
        internal const string CommentPropertyName = "$comment";
        internal const string TypePropertyName = "type";
        internal const string FormatPropertyName = "format";
        internal const string PatternPropertyName = "pattern";
        internal const string PropertiesPropertyName = "properties";
        internal const string RequiredPropertyName = "required";
        internal const string ItemsPropertyName = "items";
        internal const string AdditionalPropertiesPropertyName = "additionalProperties";
        internal const string EnumPropertyName = "enum";
        internal const string NotPropertyName = "not";
        internal const string AnyOfPropertyName = "anyOf";
        internal const string ConstPropertyName = "const";
        internal const string DefaultPropertyName = "default";
        internal const string ContentEncodingPropertyName = "contentEncoding";
        internal const string MinLengthPropertyName = "minLength";
        internal const string MaxLengthPropertyName = "maxLength";
        internal const string DeprecatedPropertyName = "deprecated";

        public static YamlSchema CreateFalseSchema() => new(false);
        public static YamlSchema CreateTrueSchema() => new(true);

        public YamlSchema() { }
        private YamlSchema(bool trueOrFalse) { _trueOrFalse = trueOrFalse; }

        public bool IsTrue => _trueOrFalse is true;
        public bool IsFalse => _trueOrFalse is false;

        /// <summary>
        /// Per the JSON schema core specification section 4.3
        /// (https://json-schema.org/draft/2020-12/json-schema-core#name-json-schema-documents)
        /// A JSON schema must either be an object or a boolean.
        /// We represent false and true schemas using this flag.
        /// It is not possible to specify keywords in boolean schemas.
        /// </summary>
        private readonly bool? _trueOrFalse;

        public string? Ref { get; set { VerifyMutable(); field = value; } }

        public string? Comment { get; set { VerifyMutable(); field = value; } }

        public YamlSchemaType Type { get; set { VerifyMutable(); field = value; } } = YamlSchemaType.Any;

        public string? Format { get; set { VerifyMutable(); field = value; } }

        public string? Pattern { get; set { VerifyMutable(); field = value; } }

        public YamlNode? Constant { get; set { VerifyMutable(); field = value; } }

        public List<KeyValuePair<string, YamlSchema>>? Properties { get; set { VerifyMutable(); field = value; } }

        public List<string>? Required { get; set { VerifyMutable(); field = value; } }

        public YamlSchema? Items { get; set { VerifyMutable(); field = value; } }

        public YamlSchema? AdditionalProperties { get; set { VerifyMutable(); field = value; } }

        public YamlArray? Enum { get; set { VerifyMutable(); field = value; } }

        public YamlSchema? Not { get; set { VerifyMutable(); field = value; } }

        public List<YamlSchema>? AnyOf { get; set { VerifyMutable(); field = value; } }

        public bool HasDefaultValue { get; set { VerifyMutable(); field = value; } }

        public YamlNode? DefaultValue { get; set { VerifyMutable(); field = value; } }

        public int? MinLength { get; set { VerifyMutable(); field = value; } }

        public int? MaxLength { get; set { VerifyMutable(); field = value; } }

        public bool? Deprecated { get; set { VerifyMutable(); field = value; } }

        public string? ContentEncoding { get; set { VerifyMutable(); field = value; } }

        public YamlSchemaExporterContext? ExporterContext { get; set; }

        public int KeywordCount
        {
            get
            {
                if (_trueOrFalse is not null)
                {
                    // Boolean schemas admit no keywords
                    return 0;
                }

                int count = 0;
                Count(Ref is not null);
                Count(Comment is not null);
                Count(Type != YamlSchemaType.Any);
                Count(Format is not null);
                Count(Pattern is not null);
                Count(Constant is not null);
                Count(Properties is not null);
                Count(Required is not null);
                Count(Items is not null);
                Count(AdditionalProperties is not null);
                Count(Enum is not null);
                Count(Not is not null);
                Count(AnyOf is not null);
                Count(HasDefaultValue);
                Count(MinLength is not null);
                Count(MaxLength is not null);
                Count(Deprecated is not null);
                Count(ContentEncoding is not null);

                return count;

                void Count(bool isKeywordSpecified)
                {
                    count += isKeywordSpecified ? 1 : 0;
                }
            }
        }

        public void MakeNullable()
        {
            if (_trueOrFalse is not null)
            {
                // boolean schemas do not admit type keywords.
                return;
            }

            if (Type != YamlSchemaType.Any)
            {
                Type |= YamlSchemaType.Null;
            }
        }

        public YamlNode ToYamlNode(YamlSchemaExporterOptions options)
        {
            if (_trueOrFalse is { } boolSchema)
            {
                return CompleteSchema((YamlNode)boolSchema);
            }

            var objSchema = new YamlObject();

            if (Ref is not null)
            {
                objSchema.Add(RefPropertyName, Ref);
            }

            if (Comment is not null)
            {
                objSchema.Add(CommentPropertyName, Comment);
            }

            if (MapSchemaType(Type) is YamlNode type)
            {
                objSchema.Add(TypePropertyName, type);
            }

            if (Format is not null)
            {
                objSchema.Add(FormatPropertyName, Format);
            }

            if (Pattern is not null)
            {
                objSchema.Add(PatternPropertyName, Pattern);
            }

            if (Constant is not null)
            {
                objSchema.Add(ConstPropertyName, Constant);
            }

            if (Properties is not null)
            {
                var properties = new YamlObject();
                foreach (KeyValuePair<string, YamlSchema> property in Properties)
                {
                    properties.Add(property.Key, property.Value.ToYamlNode(options));
                }

                objSchema.Add(PropertiesPropertyName, properties);
            }

            if (Required is not null)
            {
                var requiredArray = new YamlArray();
                foreach (string requiredProperty in Required)
                {
                    requiredArray.Add((YamlNode)requiredProperty);
                }

                objSchema.Add(RequiredPropertyName, requiredArray);
            }

            if (Items is not null)
            {
                objSchema.Add(ItemsPropertyName, Items.ToYamlNode(options));
            }

            if (AdditionalProperties is not null)
            {
                objSchema.Add(AdditionalPropertiesPropertyName, AdditionalProperties.ToYamlNode(options));
            }

            if (Enum is not null)
            {
                objSchema.Add(EnumPropertyName, Enum);
            }

            if (Not is not null)
            {
                objSchema.Add(NotPropertyName, Not.ToYamlNode(options));
            }

            if (AnyOf is not null)
            {
                YamlArray anyOfArray = [];
                foreach (YamlSchema schema in AnyOf)
                {
                    anyOfArray.Add(schema.ToYamlNode(options));
                }

                objSchema.Add(AnyOfPropertyName, anyOfArray);
            }

            if (HasDefaultValue)
            {
                objSchema.Add(DefaultPropertyName, DefaultValue);
            }

            if (MinLength is int minLength)
            {
                objSchema.Add(MinLengthPropertyName, (YamlNode)minLength);
            }

            if (MaxLength is int maxLength)
            {
                objSchema.Add(MaxLengthPropertyName, (YamlNode)maxLength);
            }

            if (Deprecated is { } deprecated)
            {
                objSchema.Add(DeprecatedPropertyName, (YamlNode)deprecated);
            }

            if (ContentEncoding is not null)
            {
                objSchema.Add(ContentEncodingPropertyName, ContentEncoding);
            }

            return CompleteSchema(objSchema);

            YamlNode CompleteSchema(YamlNode schema)
            {
                if (ExporterContext is { } context)
                {
                    Debug.Assert(options.TransformSchemaNode is not null, "context should only be populated if a callback is present.");
                    // Apply any user-defined transformations to the schema.
                    return options.TransformSchemaNode(context, schema);
                }

                return schema;
            }
        }

        /// <summary>
        /// If the schema is boolean, replaces it with a semantically
        /// equivalent object schema that allows appending keywords.
        /// </summary>
        public static void EnsureMutable(ref YamlSchema schema)
        {
            switch (schema._trueOrFalse)
            {
                case false:
                    schema = new YamlSchema { Not = CreateTrueSchema() };
                    break;
                case true:
                    schema = new YamlSchema();
                    break;
            }
        }

        private static ReadOnlySpan<YamlSchemaType> s_schemaValues =>
        [
            // NB the order of these values influences order of types in the rendered schema
            YamlSchemaType.String,
            YamlSchemaType.Integer,
            YamlSchemaType.Number,
            YamlSchemaType.Boolean,
            YamlSchemaType.Array,
            YamlSchemaType.Object,
            YamlSchemaType.Null,
        ];

        private void VerifyMutable()
        {
            Debug.Assert(_trueOrFalse is null, "Schema is not mutable");
            if (_trueOrFalse is not null)
            {
                Throw();
                static void Throw() => throw new InvalidOperationException();
            }
        }

        public static YamlNode? MapSchemaType(YamlSchemaType schemaType)
        {
            if (schemaType is YamlSchemaType.Any)
            {
                return null;
            }

            if (ToIdentifier(schemaType) is string identifier)
            {
                return identifier;
            }

            var array = new YamlArray();
            foreach (YamlSchemaType type in s_schemaValues)
            {
                if ((schemaType & type) != 0)
                {
                    array.Add((YamlNode)ToIdentifier(type)!);
                }
            }

            return array;

            static string? ToIdentifier(YamlSchemaType schemaType)
            {
                return schemaType switch
                {
                    YamlSchemaType.Null => "null",
                    YamlSchemaType.Boolean => "boolean",
                    YamlSchemaType.Integer => "integer",
                    YamlSchemaType.Number => "number",
                    YamlSchemaType.String => "string",
                    YamlSchemaType.Array => "array",
                    YamlSchemaType.Object => "object",
                    _ => null,
                };
            }
        }
    }
}
