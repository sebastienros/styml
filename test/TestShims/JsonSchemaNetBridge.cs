// Copyright (c) Sébastien Ros. Licensed under the MIT license.

// Local deviation (see UPSTREAM.md): JsonSchema.Net is built on the real System.Text.Json types. After the rename
// the schema-exporter tests hold System.Text.Yaml.Nodes.YamlNode instances, so instances are handed to
// JsonSchema.Net through a JSON-text round trip. Because JsonSchema.Evaluate(JsonNode?, ...) is not applicable to a
// YamlNode argument, C# overload resolution falls back to this extension method and the test sources stay
// unchanged. The library still writes JSON grammar at this stage, so ToYamlString() produces JSON text.

using Json.Schema;
using System.Text.Yaml.Nodes;

namespace System.Text.Yaml.Schema.Tests
{
    internal static class JsonSchemaNetBridge
    {
        public static EvaluationResults Evaluate(this JsonSchema schema, YamlNode? instance, EvaluationOptions? options = null)
        {
            System.Text.Json.Nodes.JsonNode? jsonInstance = instance is null
                ? null
                : System.Text.Json.Nodes.JsonNode.Parse(instance.ToYamlString());

            return schema.Evaluate(jsonInstance, options);
        }
    }
}
