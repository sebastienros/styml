// Copyright (c) Sébastien Ros. Licensed under the MIT license.

// One object model for every library. Keys are camelCase by default in every
// library; a key that differs carries the mapping attribute of each library:
//   JN  System.Text.Json and SharpYaml      SN  Styml
//   YDM YamlDotNet                          VM  VYaml
using System.Collections.Generic;
using VYaml.Annotations;
using JN = System.Text.Json.Serialization.JsonPropertyNameAttribute;
using SN = System.Text.Yaml.Serialization.YamlPropertyNameAttribute;
using VM = VYaml.Annotations.YamlMemberAttribute;
using YDM = YamlDotNet.Serialization.YamlMemberAttribute;

namespace Styml.Benchmarks.Model
{
    // ---------------------------------------------------------------------------------------------------------------
    // Kubernetes: a List of Deployments.

    [YamlObject(NamingConvention.LowerCamelCase)]
    public partial class K8sList
    {
        public string? ApiVersion { get; set; }
        public string? Kind { get; set; }
        public List<Deployment>? Items { get; set; }
    }

    [YamlObject(NamingConvention.LowerCamelCase)]
    public partial class Deployment
    {
        public string? ApiVersion { get; set; }
        public string? Kind { get; set; }
        public ObjectMeta? Metadata { get; set; }
        public DeploymentSpec? Spec { get; set; }
    }

    [YamlObject(NamingConvention.LowerCamelCase)]
    public partial class ObjectMeta
    {
        public string? Name { get; set; }
        public string? Namespace { get; set; }
        public Dictionary<string, string>? Labels { get; set; }
        public Dictionary<string, string>? Annotations { get; set; }
    }

    [YamlObject(NamingConvention.LowerCamelCase)]
    public partial class DeploymentSpec
    {
        public int? Replicas { get; set; }
        public LabelSelector? Selector { get; set; }
        public DeploymentStrategy? Strategy { get; set; }
        public PodTemplate? Template { get; set; }
    }

    [YamlObject(NamingConvention.LowerCamelCase)]
    public partial class LabelSelector
    {
        public Dictionary<string, string>? MatchLabels { get; set; }
    }

    [YamlObject(NamingConvention.LowerCamelCase)]
    public partial class DeploymentStrategy
    {
        public string? Type { get; set; }
        public RollingUpdate? RollingUpdate { get; set; }
    }

    [YamlObject(NamingConvention.LowerCamelCase)]
    public partial class RollingUpdate
    {
        public string? MaxSurge { get; set; }
        public string? MaxUnavailable { get; set; }
    }

    [YamlObject(NamingConvention.LowerCamelCase)]
    public partial class PodTemplate
    {
        public ObjectMeta? Metadata { get; set; }
        public PodSpec? Spec { get; set; }
    }

    [YamlObject(NamingConvention.LowerCamelCase)]
    public partial class PodSpec
    {
        public string? ServiceAccountName { get; set; }
        public int? TerminationGracePeriodSeconds { get; set; }
        public Dictionary<string, string>? NodeSelector { get; set; }
        public List<Container>? Containers { get; set; }
    }

    [YamlObject(NamingConvention.LowerCamelCase)]
    public partial class Container
    {
        public string? Name { get; set; }
        public string? Image { get; set; }
        public string? ImagePullPolicy { get; set; }
        public List<string>? Args { get; set; }
        public List<ContainerPortSpec>? Ports { get; set; }
        public List<EnvVar>? Env { get; set; }
        public ResourceRequirements? Resources { get; set; }
        public Probe? LivenessProbe { get; set; }
        public Probe? ReadinessProbe { get; set; }
    }

    [YamlObject(NamingConvention.LowerCamelCase)]
    public partial class ContainerPortSpec
    {
        public string? Name { get; set; }
        public int? ContainerPort { get; set; }
        public string? Protocol { get; set; }
    }

    [YamlObject(NamingConvention.LowerCamelCase)]
    public partial class EnvVar
    {
        public string? Name { get; set; }
        public string? Value { get; set; }
    }

    [YamlObject(NamingConvention.LowerCamelCase)]
    public partial class ResourceRequirements
    {
        public Dictionary<string, string>? Limits { get; set; }
        public Dictionary<string, string>? Requests { get; set; }
    }

    [YamlObject(NamingConvention.LowerCamelCase)]
    public partial class Probe
    {
        public HttpGetAction? HttpGet { get; set; }
        public int? InitialDelaySeconds { get; set; }
        public int? PeriodSeconds { get; set; }
        public int? FailureThreshold { get; set; }
    }

    [YamlObject(NamingConvention.LowerCamelCase)]
    public partial class HttpGetAction
    {
        public string? Path { get; set; }
        public int? Port { get; set; }
    }

    // ---------------------------------------------------------------------------------------------------------------
    // Compose file (snake_case keys where the specification uses them).

    [YamlObject(NamingConvention.LowerCamelCase)]
    public partial class ComposeFile
    {
        public string? Name { get; set; }
        public Dictionary<string, ComposeService>? Services { get; set; }
        public Dictionary<string, ComposeNetwork>? Networks { get; set; }
        public Dictionary<string, ComposeVolume>? Volumes { get; set; }
    }

    [YamlObject(NamingConvention.LowerCamelCase)]
    public partial class ComposeService
    {
        public string? Image { get; set; }

        [JN("container_name"), SN("container_name"), YDM(Alias = "container_name", ApplyNamingConventions = false), VM("container_name")]
        public string? ContainerName { get; set; }

        public string? Restart { get; set; }
        public List<string>? Command { get; set; }
        public List<string>? Ports { get; set; }
        public Dictionary<string, string>? Environment { get; set; }
        public List<string>? Volumes { get; set; }

        [JN("depends_on"), SN("depends_on"), YDM(Alias = "depends_on", ApplyNamingConventions = false), VM("depends_on")]
        public List<string>? DependsOn { get; set; }

        public List<string>? Networks { get; set; }
        public Dictionary<string, string>? Labels { get; set; }
        public Healthcheck? Healthcheck { get; set; }
    }

    [YamlObject(NamingConvention.LowerCamelCase)]
    public partial class Healthcheck
    {
        public List<string>? Test { get; set; }
        public string? Interval { get; set; }
        public string? Timeout { get; set; }
        public int? Retries { get; set; }

        [JN("start_period"), SN("start_period"), YDM(Alias = "start_period", ApplyNamingConventions = false), VM("start_period")]
        public string? StartPeriod { get; set; }
    }

    [YamlObject(NamingConvention.LowerCamelCase)]
    public partial class ComposeNetwork
    {
        public string? Driver { get; set; }
    }

    [YamlObject(NamingConvention.LowerCamelCase)]
    public partial class ComposeVolume
    {
        public string? Driver { get; set; }
    }

    // ---------------------------------------------------------------------------------------------------------------
    // GitHub Actions workflow (kebab-case keys where the syntax uses them).

    [YamlObject(NamingConvention.LowerCamelCase)]
    public partial class Workflow
    {
        public string? Name { get; set; }
        public WorkflowTriggers? On { get; set; }
        public Dictionary<string, string>? Permissions { get; set; }
        public Dictionary<string, string>? Env { get; set; }
        public Concurrency? Concurrency { get; set; }
        public Dictionary<string, WorkflowJob>? Jobs { get; set; }
    }

    [YamlObject(NamingConvention.LowerCamelCase)]
    public partial class WorkflowTriggers
    {
        public BranchFilter? Push { get; set; }

        [JN("pull_request"), SN("pull_request"), YDM(Alias = "pull_request", ApplyNamingConventions = false), VM("pull_request")]
        public BranchFilter? PullRequest { get; set; }
    }

    [YamlObject(NamingConvention.LowerCamelCase)]
    public partial class BranchFilter
    {
        public List<string>? Branches { get; set; }
        public List<string>? Paths { get; set; }
    }

    [YamlObject(NamingConvention.LowerCamelCase)]
    public partial class Concurrency
    {
        public string? Group { get; set; }

        [JN("cancel-in-progress"), SN("cancel-in-progress"), YDM(Alias = "cancel-in-progress", ApplyNamingConventions = false), VM("cancel-in-progress")]
        public bool? CancelInProgress { get; set; }
    }

    [YamlObject(NamingConvention.LowerCamelCase)]
    public partial class WorkflowJob
    {
        public string? Name { get; set; }

        [JN("runs-on"), SN("runs-on"), YDM(Alias = "runs-on", ApplyNamingConventions = false), VM("runs-on")]
        public string? RunsOn { get; set; }

        public List<string>? Needs { get; set; }

        [JN("timeout-minutes"), SN("timeout-minutes"), YDM(Alias = "timeout-minutes", ApplyNamingConventions = false), VM("timeout-minutes")]
        public int? TimeoutMinutes { get; set; }

        public JobStrategy? Strategy { get; set; }
        public Dictionary<string, string>? Env { get; set; }
        public List<WorkflowStep>? Steps { get; set; }
    }

    [YamlObject(NamingConvention.LowerCamelCase)]
    public partial class JobStrategy
    {
        [JN("fail-fast"), SN("fail-fast"), YDM(Alias = "fail-fast", ApplyNamingConventions = false), VM("fail-fast")]
        public bool? FailFast { get; set; }

        public Dictionary<string, List<string>>? Matrix { get; set; }
    }

    [YamlObject(NamingConvention.LowerCamelCase)]
    public partial class WorkflowStep
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public string? If { get; set; }
        public string? Uses { get; set; }
        public Dictionary<string, string>? With { get; set; }
        public string? Run { get; set; }
        public Dictionary<string, string>? Env { get; set; }

        [JN("working-directory"), SN("working-directory"), YDM(Alias = "working-directory", ApplyNamingConventions = false), VM("working-directory")]
        public string? WorkingDirectory { get; set; }

        [JN("continue-on-error"), SN("continue-on-error"), YDM(Alias = "continue-on-error", ApplyNamingConventions = false), VM("continue-on-error")]
        public bool? ContinueOnError { get; set; }
    }

    // ---------------------------------------------------------------------------------------------------------------
    // OpenAPI 3.0.

    [YamlObject(NamingConvention.LowerCamelCase)]
    public partial class OpenApiDocument
    {
        public string? Openapi { get; set; }
        public OpenApiInfo? Info { get; set; }
        public List<OpenApiServer>? Servers { get; set; }
        public Dictionary<string, OpenApiPathItem>? Paths { get; set; }
        public OpenApiComponents? Components { get; set; }
    }

    [YamlObject(NamingConvention.LowerCamelCase)]
    public partial class OpenApiInfo
    {
        public string? Title { get; set; }
        public string? Version { get; set; }
        public string? Description { get; set; }
    }

    [YamlObject(NamingConvention.LowerCamelCase)]
    public partial class OpenApiServer
    {
        public string? Url { get; set; }
        public string? Description { get; set; }
    }

    [YamlObject(NamingConvention.LowerCamelCase)]
    public partial class OpenApiPathItem
    {
        public OpenApiOperation? Get { get; set; }
        public OpenApiOperation? Post { get; set; }
        public OpenApiOperation? Put { get; set; }
        public OpenApiOperation? Delete { get; set; }
    }

    [YamlObject(NamingConvention.LowerCamelCase)]
    public partial class OpenApiOperation
    {
        public string? OperationId { get; set; }
        public string? Summary { get; set; }
        public List<string>? Tags { get; set; }
        public List<OpenApiParameter>? Parameters { get; set; }
        public OpenApiRequestBody? RequestBody { get; set; }
        public Dictionary<string, OpenApiResponse>? Responses { get; set; }
    }

    [YamlObject(NamingConvention.LowerCamelCase)]
    public partial class OpenApiParameter
    {
        public string? Name { get; set; }
        public string? In { get; set; }
        public string? Description { get; set; }
        public bool? Required { get; set; }
        public OpenApiSchema? Schema { get; set; }
    }

    [YamlObject(NamingConvention.LowerCamelCase)]
    public partial class OpenApiRequestBody
    {
        public bool? Required { get; set; }
        public Dictionary<string, OpenApiMediaType>? Content { get; set; }
    }

    [YamlObject(NamingConvention.LowerCamelCase)]
    public partial class OpenApiResponse
    {
        public string? Description { get; set; }
        public Dictionary<string, OpenApiMediaType>? Content { get; set; }
    }

    [YamlObject(NamingConvention.LowerCamelCase)]
    public partial class OpenApiMediaType
    {
        public OpenApiSchema? Schema { get; set; }
    }

    [YamlObject(NamingConvention.LowerCamelCase)]
    public partial class OpenApiComponents
    {
        public Dictionary<string, OpenApiSchema>? Schemas { get; set; }
    }

    [YamlObject(NamingConvention.LowerCamelCase)]
    public partial class OpenApiSchema
    {
        [JN("$ref"), SN("$ref"), YDM(Alias = "$ref", ApplyNamingConventions = false), VM("$ref")]
        public string? Ref { get; set; }

        public string? Type { get; set; }
        public string? Format { get; set; }
        public string? Description { get; set; }
        public bool? Nullable { get; set; }
        public List<string>? Required { get; set; }
        public List<string>? Enum { get; set; }
        public Dictionary<string, OpenApiSchema>? Properties { get; set; }
        public OpenApiSchema? Items { get; set; }
    }

    // ---------------------------------------------------------------------------------------------------------------
    // A list of records (the root is a sequence).

    [YamlObject(NamingConvention.LowerCamelCase)]
    public partial class Record
    {
        public long? Id { get; set; }
        public string? Name { get; set; }
        public string? Email { get; set; }
        public bool? Active { get; set; }
        public double? Score { get; set; }
        public int? Visits { get; set; }
        public List<string>? Tags { get; set; }
        public Address? Address { get; set; }
    }

    [YamlObject(NamingConvention.LowerCamelCase)]
    public partial class Address
    {
        public string? Street { get; set; }
        public string? City { get; set; }
        public string? Zip { get; set; }
        public string? Country { get; set; }
    }
}
