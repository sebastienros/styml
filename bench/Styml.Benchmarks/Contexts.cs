// Copyright (c) Sébastien Ros. Licensed under the MIT license.

// The source-generated contexts of Styml, System.Text.Json, SharpYaml and YamlDotNet (VYaml generates into the model
// types). Every context uses camelCase keys, with the per-property names of the model.
using System.Collections.Generic;
using Styml.Benchmarks.Model;

namespace Styml.Benchmarks
{
    [System.Text.Yaml.Serialization.YamlSourceGenerationOptions(PropertyNamingPolicy = System.Text.Yaml.Serialization.YamlKnownNamingPolicy.CamelCase)]
    [System.Text.Yaml.Serialization.YamlSerializable(typeof(K8sList))]
    [System.Text.Yaml.Serialization.YamlSerializable(typeof(ComposeFile))]
    [System.Text.Yaml.Serialization.YamlSerializable(typeof(Workflow))]
    [System.Text.Yaml.Serialization.YamlSerializable(typeof(OpenApiDocument))]
    [System.Text.Yaml.Serialization.YamlSerializable(typeof(List<Record>))]
    internal partial class StymlContext : System.Text.Yaml.Serialization.YamlSerializerContext
    {
    }

    // M4.0's Serialize rows: the same types, with null members left out by every library.
    [System.Text.Yaml.Serialization.YamlSourceGenerationOptions(PropertyNamingPolicy = System.Text.Yaml.Serialization.YamlKnownNamingPolicy.CamelCase, DefaultIgnoreCondition = System.Text.Yaml.Serialization.YamlIgnoreCondition.WhenWritingNull)]
    [System.Text.Yaml.Serialization.YamlSerializable(typeof(K8sList))]
    [System.Text.Yaml.Serialization.YamlSerializable(typeof(ComposeFile))]
    [System.Text.Yaml.Serialization.YamlSerializable(typeof(Workflow))]
    [System.Text.Yaml.Serialization.YamlSerializable(typeof(OpenApiDocument))]
    [System.Text.Yaml.Serialization.YamlSerializable(typeof(List<Record>))]
    internal partial class StymlWriteContext : System.Text.Yaml.Serialization.YamlSerializerContext
    {
    }

    [System.Text.Json.Serialization.JsonSourceGenerationOptions(PropertyNamingPolicy = System.Text.Json.Serialization.JsonKnownNamingPolicy.CamelCase, DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    [System.Text.Json.Serialization.JsonSerializable(typeof(K8sList))]
    [System.Text.Json.Serialization.JsonSerializable(typeof(ComposeFile))]
    [System.Text.Json.Serialization.JsonSerializable(typeof(Workflow))]
    [System.Text.Json.Serialization.JsonSerializable(typeof(OpenApiDocument))]
    [System.Text.Json.Serialization.JsonSerializable(typeof(List<Record>))]
    internal partial class JsonWriteContext : System.Text.Json.Serialization.JsonSerializerContext
    {
    }

    [SharpYaml.Serialization.YamlSourceGenerationOptions(PropertyNamingPolicy = System.Text.Json.Serialization.JsonKnownNamingPolicy.CamelCase, DefaultIgnoreCondition = SharpYaml.YamlIgnoreCondition.WhenWritingNull)]
    [SharpYaml.Serialization.YamlSerializable(typeof(K8sList))]
    [SharpYaml.Serialization.YamlSerializable(typeof(ComposeFile))]
    [SharpYaml.Serialization.YamlSerializable(typeof(Workflow))]
    [SharpYaml.Serialization.YamlSerializable(typeof(OpenApiDocument))]
    [SharpYaml.Serialization.YamlSerializable(typeof(List<Record>))]
    internal partial class SharpYamlWriteContext : SharpYaml.Serialization.YamlSerializerContext
    {
    }

    [System.Text.Json.Serialization.JsonSourceGenerationOptions(PropertyNamingPolicy = System.Text.Json.Serialization.JsonKnownNamingPolicy.CamelCase)]
    [System.Text.Json.Serialization.JsonSerializable(typeof(K8sList))]
    [System.Text.Json.Serialization.JsonSerializable(typeof(ComposeFile))]
    [System.Text.Json.Serialization.JsonSerializable(typeof(Workflow))]
    [System.Text.Json.Serialization.JsonSerializable(typeof(OpenApiDocument))]
    [System.Text.Json.Serialization.JsonSerializable(typeof(List<Record>))]
    internal partial class JsonContext : System.Text.Json.Serialization.JsonSerializerContext
    {
    }

    [SharpYaml.Serialization.YamlSourceGenerationOptions(PropertyNamingPolicy = System.Text.Json.Serialization.JsonKnownNamingPolicy.CamelCase)]
    [SharpYaml.Serialization.YamlSerializable(typeof(K8sList))]
    [SharpYaml.Serialization.YamlSerializable(typeof(ComposeFile))]
    [SharpYaml.Serialization.YamlSerializable(typeof(Workflow))]
    [SharpYaml.Serialization.YamlSerializable(typeof(OpenApiDocument))]
    [SharpYaml.Serialization.YamlSerializable(typeof(List<Record>))]
    internal partial class SharpYamlContext : SharpYaml.Serialization.YamlSerializerContext
    {
    }

    [YamlDotNet.Serialization.YamlStaticContext]
    [YamlDotNet.Serialization.YamlSerializable(typeof(K8sList))]
    [YamlDotNet.Serialization.YamlSerializable(typeof(Deployment))]
    [YamlDotNet.Serialization.YamlSerializable(typeof(ObjectMeta))]
    [YamlDotNet.Serialization.YamlSerializable(typeof(DeploymentSpec))]
    [YamlDotNet.Serialization.YamlSerializable(typeof(LabelSelector))]
    [YamlDotNet.Serialization.YamlSerializable(typeof(DeploymentStrategy))]
    [YamlDotNet.Serialization.YamlSerializable(typeof(RollingUpdate))]
    [YamlDotNet.Serialization.YamlSerializable(typeof(PodTemplate))]
    [YamlDotNet.Serialization.YamlSerializable(typeof(PodSpec))]
    [YamlDotNet.Serialization.YamlSerializable(typeof(Container))]
    [YamlDotNet.Serialization.YamlSerializable(typeof(ContainerPortSpec))]
    [YamlDotNet.Serialization.YamlSerializable(typeof(EnvVar))]
    [YamlDotNet.Serialization.YamlSerializable(typeof(ResourceRequirements))]
    [YamlDotNet.Serialization.YamlSerializable(typeof(Probe))]
    [YamlDotNet.Serialization.YamlSerializable(typeof(HttpGetAction))]
    [YamlDotNet.Serialization.YamlSerializable(typeof(ComposeFile))]
    [YamlDotNet.Serialization.YamlSerializable(typeof(ComposeService))]
    [YamlDotNet.Serialization.YamlSerializable(typeof(Healthcheck))]
    [YamlDotNet.Serialization.YamlSerializable(typeof(ComposeNetwork))]
    [YamlDotNet.Serialization.YamlSerializable(typeof(ComposeVolume))]
    [YamlDotNet.Serialization.YamlSerializable(typeof(Workflow))]
    [YamlDotNet.Serialization.YamlSerializable(typeof(WorkflowTriggers))]
    [YamlDotNet.Serialization.YamlSerializable(typeof(BranchFilter))]
    [YamlDotNet.Serialization.YamlSerializable(typeof(Concurrency))]
    [YamlDotNet.Serialization.YamlSerializable(typeof(WorkflowJob))]
    [YamlDotNet.Serialization.YamlSerializable(typeof(JobStrategy))]
    [YamlDotNet.Serialization.YamlSerializable(typeof(WorkflowStep))]
    [YamlDotNet.Serialization.YamlSerializable(typeof(OpenApiDocument))]
    [YamlDotNet.Serialization.YamlSerializable(typeof(OpenApiInfo))]
    [YamlDotNet.Serialization.YamlSerializable(typeof(OpenApiServer))]
    [YamlDotNet.Serialization.YamlSerializable(typeof(OpenApiPathItem))]
    [YamlDotNet.Serialization.YamlSerializable(typeof(OpenApiOperation))]
    [YamlDotNet.Serialization.YamlSerializable(typeof(OpenApiParameter))]
    [YamlDotNet.Serialization.YamlSerializable(typeof(OpenApiRequestBody))]
    [YamlDotNet.Serialization.YamlSerializable(typeof(OpenApiResponse))]
    [YamlDotNet.Serialization.YamlSerializable(typeof(OpenApiMediaType))]
    [YamlDotNet.Serialization.YamlSerializable(typeof(OpenApiComponents))]
    [YamlDotNet.Serialization.YamlSerializable(typeof(OpenApiSchema))]
    [YamlDotNet.Serialization.YamlSerializable(typeof(Record))]
    [YamlDotNet.Serialization.YamlSerializable(typeof(Address))]
    public partial class YamlDotNetContext : YamlDotNet.Serialization.StaticContext
    {
    }
}
