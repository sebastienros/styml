// Copyright (c) Sébastien Ros. Licensed under the MIT license.

#:project ../Styml.Benchmarks.Model/Styml.Benchmarks.Model.csproj
#:property PublishAot=false
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// Generates the M6a benchmark corpus from a fixed seed:
//   dotnet run bench/corpus/generate.cs        (from the repository root)
// Each document is built as model objects, written as JSON by System.Text.Json (pretty-printed, and minified for the
// large record list), and as block YAML by the emitter below, which walks that JSON. The two forms are therefore the
// same data. The emitter writes no anchors, aliases, merge keys, collection keys, tags or second documents, and the
// generator checks the output for them.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Styml.Benchmarks.Model;

var random = new Random(20261118);
string outDir = Path.Combine(Directory.GetCurrentDirectory(), "bench", "corpus");
var json = new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = true,
    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
};
var jsonMin = new JsonSerializerOptions(json) { WriteIndented = false };

string[] words = { "alpha", "bravo", "charlie", "delta", "echo", "foxtrot", "golf", "hotel", "india", "juliet", "kilo", "lima", "mike", "november", "oscar", "papa", "quebec", "romeo", "sierra", "tango", "uniform", "victor", "whiskey", "xray", "yankee", "zulu" };
string[] cities = { "Lisbon", "Kraków", "Montréal", "Zürich", "Osaka", "Seattle", "Nairobi", "São Paulo", "Reykjavík", "Auckland" };
string Word() => words[random.Next(words.Length)];
string Name(int i) => $"{Word()}-{Word()}-{i}";

// ---------------------------------------------------------------------------------------------------------------------
// Documents.

Deployment MakeDeployment(int i)
{
    string app = Name(i);
    var labels = new Dictionary<string, string> { ["app.kubernetes.io/name"] = app, ["app.kubernetes.io/part-of"] = Word(), ["tier"] = random.Next(2) == 0 ? "backend" : "frontend" };
    return new Deployment
    {
        ApiVersion = "apps/v1",
        Kind = "Deployment",
        Metadata = new ObjectMeta
        {
            Name = app,
            Namespace = "team-" + Word(),
            Labels = labels,
            Annotations = new Dictionary<string, string> { ["deployment.kubernetes.io/revision"] = random.Next(1, 40).ToString(), ["example.com/owner"] = Word() + "@example.com", ["example.com/docs"] = "https://docs.example.com/services/" + app },
        },
        Spec = new DeploymentSpec
        {
            Replicas = random.Next(1, 6),
            Selector = new LabelSelector { MatchLabels = new Dictionary<string, string> { ["app.kubernetes.io/name"] = app } },
            Strategy = new DeploymentStrategy { Type = "RollingUpdate", RollingUpdate = new RollingUpdate { MaxSurge = "25%", MaxUnavailable = random.Next(2) == 0 ? "0" : "1" } },
            Template = new PodTemplate
            {
                Metadata = new ObjectMeta { Labels = new Dictionary<string, string>(labels) },
                Spec = new PodSpec
                {
                    ServiceAccountName = app,
                    TerminationGracePeriodSeconds = 30,
                    NodeSelector = new Dictionary<string, string> { ["kubernetes.io/os"] = "linux" },
                    Containers = Enumerable.Range(0, random.Next(1, 3)).Select(c => new Container
                    {
                        Name = c == 0 ? app : "sidecar-" + Word(),
                        Image = $"registry.example.com/{Word()}/{app}:{random.Next(1, 4)}.{random.Next(0, 20)}.{random.Next(0, 9)}",
                        ImagePullPolicy = "IfNotPresent",
                        Args = new List<string> { "--port=8080", "--log-level=" + (random.Next(2) == 0 ? "info" : "debug") },
                        Ports = new List<ContainerPortSpec> { new() { Name = "http", ContainerPort = 8080, Protocol = "TCP" }, new() { Name = "metrics", ContainerPort = 9090, Protocol = "TCP" } },
                        Env = new List<EnvVar>
                        {
                            new() { Name = "ASPNETCORE_URLS", Value = "http://+:8080" },
                            new() { Name = "FEATURE_FLAGS", Value = "true" },
                            new() { Name = "CACHE_SIZE", Value = random.Next(64, 1024).ToString() },
                            new() { Name = "REGION", Value = Word() },
                        },
                        Resources = new ResourceRequirements
                        {
                            Limits = new Dictionary<string, string> { ["cpu"] = random.Next(1, 4) * 250 + "m", ["memory"] = random.Next(1, 8) * 128 + "Mi" },
                            Requests = new Dictionary<string, string> { ["cpu"] = "100m", ["memory"] = "64Mi" },
                        },
                        LivenessProbe = new Probe { HttpGet = new HttpGetAction { Path = "/healthz", Port = 8080 }, InitialDelaySeconds = 10, PeriodSeconds = 15 },
                        ReadinessProbe = new Probe { HttpGet = new HttpGetAction { Path = "/ready", Port = 8080 }, PeriodSeconds = 5, FailureThreshold = 3 },
                    }).ToList(),
                },
            },
        },
    };
}

K8sList MakeK8s(int count) => new() { ApiVersion = "v1", Kind = "List", Items = Enumerable.Range(0, count).Select(MakeDeployment).ToList() };

ComposeFile MakeCompose(int count)
{
    var services = new Dictionary<string, ComposeService>();
    for (int i = 0; i < count; i++)
    {
        string name = Name(i);
        services[name] = new ComposeService
        {
            Image = i % 3 == 0 ? "postgres:16" : $"registry.example.com/{Word()}/{name}:latest",
            ContainerName = name,
            Restart = "unless-stopped",
            Command = i % 2 == 0 ? new List<string> { "dotnet", name + ".dll", "--urls", "http://0.0.0.0:8080" } : null,
            Ports = new List<string> { $"{8000 + i}:8080", $"{9000 + i}:9090" },
            Environment = new Dictionary<string, string> { ["ASPNETCORE_ENVIRONMENT"] = "Production", ["ConnectionStrings__Default"] = $"Host=db-{i};Database={Word()};Username=app", ["LOG_LEVEL"] = "info", ["RETRIES"] = "5" },
            Volumes = new List<string> { $"./data/{name}:/var/lib/{name}", "/etc/localtime:/etc/localtime:ro" },
            DependsOn = i > 0 ? new List<string> { services.Keys.First() } : null,
            Networks = new List<string> { "backend" },
            Labels = new Dictionary<string, string> { ["com.example.team"] = Word(), ["com.example.description"] = $"Service {name}: handles {Word()} requests" },
            Healthcheck = new Healthcheck { Test = new List<string> { "CMD", "curl", "-f", "http://localhost:8080/health" }, Interval = "30s", Timeout = "5s", Retries = 3, StartPeriod = "10s" },
        };
    }
    return new ComposeFile
    {
        Name = "stack-" + Word(),
        Services = services,
        Networks = new Dictionary<string, ComposeNetwork> { ["backend"] = new() { Driver = "bridge" }, ["frontend"] = new() { Driver = "bridge" } },
        Volumes = new Dictionary<string, ComposeVolume> { ["db-data"] = new() { Driver = "local" } },
    };
}

Workflow MakeWorkflow(int jobs)
{
    var all = new Dictionary<string, WorkflowJob>();
    for (int j = 0; j < jobs; j++)
    {
        string id = j == 0 ? "build" : "job-" + Name(j);
        all[id] = new WorkflowJob
        {
            Name = $"Build and test {Word()} ({j})",
            RunsOn = "${{ matrix.os }}",
            Needs = j > 0 ? new List<string> { "build" } : null,
            TimeoutMinutes = 30,
            Strategy = new JobStrategy { FailFast = false, Matrix = new Dictionary<string, List<string>> { ["os"] = new() { "ubuntu-latest", "windows-latest", "macos-latest" }, ["dotnet"] = new() { "8.0.x", "9.0.x" } } },
            Env = new Dictionary<string, string> { ["DOTNET_NOLOGO"] = "true", ["CONFIGURATION"] = "Release" },
            Steps = new List<WorkflowStep>
            {
                new() { Name = "Check out", Uses = "actions/checkout@v4", With = new Dictionary<string, string> { ["fetch-depth"] = "0" } },
                new() { Name = "Set up .NET", Uses = "actions/setup-dotnet@v4", With = new Dictionary<string, string> { ["dotnet-version"] = "${{ matrix.dotnet }}" } },
                new() { Id = "build", Name = "Build", Run = "dotnet restore\ndotnet build --no-restore -c $CONFIGURATION\necho \"built ${{ github.sha }}\"\n" },
                new() { Name = "Test", Run = $"dotnet test --no-build -c $CONFIGURATION --logger trx --results-directory TestResults/{j}\n", WorkingDirectory = "src", ContinueOnError = j % 4 == 3 },
                new() { Name = "Upload results", If = "${{ always() && github.event_name == 'push' }}", Uses = "actions/upload-artifact@v4", With = new Dictionary<string, string> { ["name"] = $"results-{j}", ["path"] = "TestResults/" } },
            },
        };
    }
    return new Workflow
    {
        Name = "CI",
        On = new WorkflowTriggers { Push = new BranchFilter { Branches = new() { "main", "release/*" }, Paths = new() { "src/**", "test/**" } }, PullRequest = new BranchFilter { Branches = new() { "main" } } },
        Permissions = new Dictionary<string, string> { ["contents"] = "read", ["checks"] = "write" },
        Env = new Dictionary<string, string> { ["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1" },
        Concurrency = new Concurrency { Group = "ci-${{ github.ref }}", CancelInProgress = true },
        Jobs = all,
    };
}

OpenApiDocument MakeOpenApi(int resources)
{
    var paths = new Dictionary<string, OpenApiPathItem>();
    var schemas = new Dictionary<string, OpenApiSchema>();
    for (int r = 0; r < resources; r++)
    {
        string resource = Word() + r;
        string schema = char.ToUpperInvariant(resource[0]) + resource[1..];
        schemas[schema] = new OpenApiSchema
        {
            Type = "object",
            Description = $"A {resource} resource.",
            Required = new() { "id", "name" },
            Properties = new Dictionary<string, OpenApiSchema>
            {
                ["id"] = new() { Type = "string", Format = "uuid" },
                ["name"] = new() { Type = "string", Description = "Display name." },
                ["count"] = new() { Type = "integer", Format = "int32" },
                ["status"] = new() { Type = "string", Enum = new() { "active", "suspended", "deleted" } },
                ["tags"] = new() { Type = "array", Items = new() { Type = "string" } },
                ["owner"] = new() { Ref = "#/components/schemas/" + schema + "Owner", Nullable = true },
            },
        };
        schemas[schema + "Owner"] = new OpenApiSchema { Type = "object", Properties = new Dictionary<string, OpenApiSchema> { ["email"] = new() { Type = "string", Format = "email" } } };
        var reference = new OpenApiSchema { Ref = "#/components/schemas/" + schema };
        var json = new Dictionary<string, OpenApiMediaType> { ["application/json"] = new() { Schema = reference } };
        var idParameter = new OpenApiParameter { Name = "id", In = "path", Required = true, Description = "The identifier.", Schema = new() { Type = "string", Format = "uuid" } };
        paths[$"/v1/{resource}"] = new OpenApiPathItem
        {
            Get = new OpenApiOperation
            {
                OperationId = "list" + schema,
                Summary = $"Lists {resource} resources",
                Tags = new() { schema },
                Parameters = new() { new() { Name = "top", In = "query", Schema = new() { Type = "integer" } }, new() { Name = "skip", In = "query", Schema = new() { Type = "integer" } } },
                Responses = new Dictionary<string, OpenApiResponse> { ["200"] = new() { Description = "OK", Content = new Dictionary<string, OpenApiMediaType> { ["application/json"] = new() { Schema = new() { Type = "array", Items = reference } } } } },
            },
            Post = new OpenApiOperation
            {
                OperationId = "create" + schema,
                Summary = $"Creates a {resource}",
                Tags = new() { schema },
                RequestBody = new OpenApiRequestBody { Required = true, Content = json },
                Responses = new Dictionary<string, OpenApiResponse> { ["201"] = new() { Description = "Created", Content = json }, ["400"] = new() { Description = "Bad request" } },
            },
        };
        paths[$"/v1/{resource}/{{id}}"] = new OpenApiPathItem
        {
            Get = new OpenApiOperation { OperationId = "get" + schema, Tags = new() { schema }, Parameters = new() { idParameter }, Responses = new Dictionary<string, OpenApiResponse> { ["200"] = new() { Description = "OK", Content = json }, ["404"] = new() { Description = "Not found" } } },
            Put = new OpenApiOperation { OperationId = "update" + schema, Tags = new() { schema }, Parameters = new() { idParameter }, RequestBody = new OpenApiRequestBody { Required = true, Content = json }, Responses = new Dictionary<string, OpenApiResponse> { ["200"] = new() { Description = "OK", Content = json } } },
            Delete = new OpenApiOperation { OperationId = "delete" + schema, Tags = new() { schema }, Parameters = new() { idParameter }, Responses = new Dictionary<string, OpenApiResponse> { ["204"] = new() { Description = "Deleted" } } },
        };
    }
    return new OpenApiDocument
    {
        Openapi = "3.0.3",
        Info = new OpenApiInfo { Title = "Example API", Version = "1.4.0", Description = "A generated API description: no real service." },
        Servers = new() { new() { Url = "https://api.example.com", Description = "Production" } },
        Paths = paths,
        Components = new OpenApiComponents { Schemas = schemas },
    };
}

List<Record> MakeRecords(int count) => Enumerable.Range(0, count).Select(i => new Record
{
    Id = 1_000_000L + i,
    Name = $"{char.ToUpperInvariant(Word()[0])}{Word()[1..]} {char.ToUpperInvariant(Word()[0])}{Word()[1..]}",
    Email = $"{Word()}.{i}@example.com",
    Active = random.Next(3) != 0,
    Score = Math.Round(random.NextDouble() * 100, 2),
    Visits = random.Next(0, 5000),
    Tags = Enumerable.Range(0, random.Next(0, 4)).Select(_ => Word()).ToList(),
    Address = new Address { Street = $"{random.Next(1, 999)} {Word()} street", City = cities[random.Next(cities.Length)], Zip = random.Next(10000, 99999).ToString(), Country = Word() },
}).ToList();

// ---------------------------------------------------------------------------------------------------------------------
// Writing.

var sizes = new List<(string Name, int Bytes)>();

void Write<T>(string name, Func<int, T> make, int targetBytes, bool indentlessSequences, bool minified = false)
{
    // Size the document from one unit, then build it with the count that reaches the target.
    int unit = Encoding.UTF8.GetByteCount(ToYaml(JsonSerializer.Serialize(make(1), json), indentlessSequences));
    int count = Math.Max(1, targetBytes / Math.Max(1, unit));
    T value = make(count);
    string jsonText = JsonSerializer.Serialize(value, json) + "\n";
    string yamlText = "# Generated by bench/corpus/generate.cs (seed 20261118): original data, MIT licence.\n" + ToYaml(jsonText, indentlessSequences);
    Check(name, yamlText);
    File.WriteAllText(Path.Combine(outDir, name + ".yaml"), yamlText);
    File.WriteAllText(Path.Combine(outDir, name + ".json"), jsonText);
    sizes.Add((name + ".yaml", Encoding.UTF8.GetByteCount(yamlText)));
    sizes.Add((name + ".json", Encoding.UTF8.GetByteCount(jsonText)));
    if (minified)
    {
        string min = JsonSerializer.Serialize(value, jsonMin);
        File.WriteAllText(Path.Combine(outDir, name + ".min.json"), min);
        sizes.Add((name + ".min.json", Encoding.UTF8.GetByteCount(min)));
    }
}

Write("k8s-s", MakeK8s, 1_000, indentlessSequences: true);
Write("k8s-m", MakeK8s, 100_000, indentlessSequences: true);
Write("k8s-l", MakeK8s, 3_000_000, indentlessSequences: true);
Write("compose-s", MakeCompose, 1_000, indentlessSequences: false);
Write("compose-m", MakeCompose, 50_000, indentlessSequences: false);
Write("workflow-s", MakeWorkflow, 1_000, indentlessSequences: false);
Write("workflow-m", MakeWorkflow, 30_000, indentlessSequences: false);
Write("openapi-m", MakeOpenApi, 100_000, indentlessSequences: false);
Write("openapi-l", MakeOpenApi, 1_000_000, indentlessSequences: false);
Write("records-s", MakeRecords, 1_000, indentlessSequences: false);
Write("records-m", MakeRecords, 100_000, indentlessSequences: false);
Write("records-l", MakeRecords, 3_000_000, indentlessSequences: false, minified: true);

// M6b: anchors and merge keys, compose-shaped, after every M6a document so
// that their random draws do not change. Not in Corpus.Names: Corpus.AnchorNames.
WriteAnchors("anchors-s", 1_000);
WriteAnchors("anchors-m", 50_000);
WriteMerge("merge-s", 1_000);
WriteMerge("merge-m", 50_000);

foreach (var (file, bytes) in sizes)
{
    Console.WriteLine($"{file,-24} {bytes,12:N0}");
}

// ---------------------------------------------------------------------------------------------------------------------
// M6b: the anchors and merge documents.

// One service's own fields (no environment, labels or healthcheck).
ComposeService OwnFields(int i, string name) => new ComposeService
{
    ContainerName = name,
    Command = i % 2 == 0 ? new List<string> { "dotnet", name + ".dll", "--urls", "http://0.0.0.0:8080" } : null,
    Ports = new List<string> { $"{8000 + i}:8080", $"{9000 + i}:9090" },
    Volumes = new List<string> { $"./data/{name}:/var/lib/{name}", "/etc/localtime:/etc/localtime:ro" },
    Networks = new List<string> { "backend" },
};

ComposeService Common(int g) => new ComposeService
{
    Image = $"registry.example.com/{Word()}/group-{g}:latest",
    Restart = "unless-stopped",
    Environment = new Dictionary<string, string> { ["ASPNETCORE_ENVIRONMENT"] = "Production", ["ConnectionStrings__Default"] = $"Host=db-{g};Database={Word()};Username=app", ["LOG_LEVEL"] = "info", ["RETRIES"] = "5" },
    Labels = new Dictionary<string, string> { ["com.example.team"] = Word(), ["com.example.group"] = $"group-{g}" },
    Healthcheck = new Healthcheck { Test = new List<string> { "CMD", "curl", "-f", $"http://localhost:{8080 + g}/health" }, Interval = "30s", Timeout = "5s", Retries = 3, StartPeriod = "10s" },
};

// anchors-*: services in groups of 4 among the first 32 share their environment and healthcheck instances, written once
// with an anchor at their first use (a mapped member, review D) and as aliases after: at most 48 aliases a file.
ComposeFile MakeShared(int count)
{
    var services = new Dictionary<string, ComposeService>();
    ComposeService? group = null;
    for (int i = 0; i < count; i++)
    {
        string name = Name(i);
        if (i % 4 == 0 || i >= 32)
        {
            group = Common(i);
        }

        ComposeService service = OwnFields(i, name);
        service.Image = group!.Image;
        service.Restart = group.Restart;
        service.Environment = group.Environment;
        service.Healthcheck = group.Healthcheck;
        service.Labels = new Dictionary<string, string> { ["com.example.service"] = name };
        services[name] = service;
    }

    return new ComposeFile { Name = "stack-" + Word(), Services = services, Networks = new Dictionary<string, ComposeNetwork> { ["backend"] = new() { Driver = "bridge" } } };
}

void WriteAnchors(string name, int targetBytes)
{
    int unit = Encoding.UTF8.GetByteCount(JsonSerializer.Serialize(MakeShared(1), json));
    ComposeFile value = MakeShared(Math.Max(3, targetBytes / Math.Max(1, unit))); // at least one alias
    string jsonText = JsonSerializer.Serialize(value, json) + "\n";
    var plan = new AnchorPlan(new HashSet<string> { "environment", "healthcheck" }, jsonText);
    string yamlText = "# Generated by bench/corpus/generate.cs (seed 20261118): original data, MIT licence.\n" + ToYaml(jsonText, indentlessSequences: false, plan);
    CheckAnchors(name, yamlText, plan.Count);
    File.WriteAllText(Path.Combine(outDir, name + ".yaml"), yamlText);
    File.WriteAllText(Path.Combine(outDir, name + ".json"), jsonText);
    sizes.Add((name + ".yaml", Encoding.UTF8.GetByteCount(yamlText)));
    sizes.Add((name + ".json", Encoding.UTF8.GetByteCount(jsonText)));
}

// merge-*: the real compose shape, an x-common block merged into every service (<<: *common), every fourth service
// overriding restart. The JSON twin is the merged result.
void WriteMerge(string name, int targetBytes)
{
    ComposeService common = Common(0);
    int count = Math.Max(2, targetBytes / Math.Max(1, Encoding.UTF8.GetByteCount(JsonSerializer.Serialize(OwnFields(0, Name(0)), json)) + 12));
    var own = new List<(string Name, ComposeService Service)>();
    var merged = new Dictionary<string, ComposeService>();
    for (int i = 0; i < count; i++)
    {
        string n = Name(i);
        ComposeService o = OwnFields(i, n);
        if (i % 4 == 3)
        {
            o.Restart = "no";
        }

        own.Add((n, o));
        merged[n] = new ComposeService
        {
            Image = common.Image,
            ContainerName = o.ContainerName,
            Restart = o.Restart ?? common.Restart,
            Command = o.Command,
            Ports = o.Ports,
            Environment = common.Environment,
            Volumes = o.Volumes,
            Networks = o.Networks,
            Labels = common.Labels,
            Healthcheck = common.Healthcheck,
        };
    }

    var value = new ComposeFile { Name = "stack-" + Word(), Services = merged, Networks = new Dictionary<string, ComposeNetwork> { ["backend"] = new() { Driver = "bridge" } } };
    string jsonText = JsonSerializer.Serialize(value, json) + "\n";
    var yaml = new StringBuilder("# Generated by bench/corpus/generate.cs (seed 20261118): original data, MIT licence.\n");
    yaml.Append("name: ").Append(Scalar(value.Name)).Append('\n');
    yaml.Append("x-common: &common\n").Append(Indent(ToYaml(JsonSerializer.Serialize(common, json), false), 2));
    yaml.Append("services:\n");
    foreach ((string n, ComposeService o) in own)
    {
        yaml.Append("  ").Append(Scalar(n, isKey: true)).Append(":\n    <<: *common\n").Append(Indent(ToYaml(JsonSerializer.Serialize(o, json), false), 4));
    }

    yaml.Append("networks:\n  backend:\n    driver: bridge\n");
    string yamlText = yaml.ToString();
    CheckAnchors(name, yamlText, count);
    File.WriteAllText(Path.Combine(outDir, name + ".yaml"), yamlText);
    File.WriteAllText(Path.Combine(outDir, name + ".json"), jsonText);
    sizes.Add((name + ".yaml", Encoding.UTF8.GetByteCount(yamlText)));
    sizes.Add((name + ".json", Encoding.UTF8.GetByteCount(jsonText)));
}

static string Indent(string text, int by) => string.Concat(text.Split('\n').Where(l => l.Length > 0).Select(l => new string(' ', by) + l + "\n"));

// The anchors files may have anchors, aliases and merge keys, and nothing else the corpus excludes.
static void CheckAnchors(string name, string yaml, int aliases)
{
    foreach (string line in yaml.Split('\n'))
    {
        string t = line.TrimStart(' ', '-');
        if (t.StartsWith("---") || t.StartsWith("!") || t.StartsWith("? ") || Regex.IsMatch(line, @":\s+![^\s""]"))
        {
            throw new InvalidOperationException($"{name}: a construct the anchors corpus excludes: {line}");
        }
    }

    int written = Regex.Matches(yaml, @"\*[A-Za-z0-9]+\n").Count;
    if (written == 0 || (name.StartsWith("anchors") && written > 48))
    {
        throw new InvalidOperationException($"{name}: {written} aliases (expected 1 to 48)");
    }
}

// ---------------------------------------------------------------------------------------------------------------------
// The YAML emitter: block style, from the JSON text.

static string ToYaml(string jsonText, bool indentlessSequences, AnchorPlan? plan = null)
{
    using JsonDocument document = JsonDocument.Parse(jsonText);
    var sb = new StringBuilder();
    var root = document.RootElement;
    if (root.ValueKind == JsonValueKind.Array)
    {
        Sequence(sb, root, 0, indentlessSequences, plan);
    }
    else
    {
        Mapping(sb, root, 0, indentlessSequences, firstPrefix: null, plan);
    }
    return sb.ToString();
}

static void Mapping(StringBuilder sb, JsonElement obj, int indent, bool indentless, string? firstPrefix, AnchorPlan? plan = null)
{
    bool first = true;
    foreach (JsonProperty property in obj.EnumerateObject())
    {
        if (first && firstPrefix != null)
        {
            sb.Append(firstPrefix);
        }
        else
        {
            sb.Append(' ', indent);
        }
        first = false;
        sb.Append(Scalar(property.Name, isKey: true)).Append(':');
        if (plan?.For(property.Name, property.Value) is var (marker, isAlias))
        {
            // M6b: an alias replaces the value; an anchor goes before it, on the key's line.
            sb.Append(marker);
            if (isAlias)
            {
                sb.Append('\n');
                continue;
            }
        }

        Value(sb, property.Value, indent, indentless, plan);
    }
}

static void Value(StringBuilder sb, JsonElement value, int indent, bool indentless, AnchorPlan? plan = null)
{
    switch (value.ValueKind)
    {
        case JsonValueKind.Object when value.EnumerateObject().Any():
            sb.Append('\n');
            Mapping(sb, value, indent + 2, indentless, firstPrefix: null, plan);
            break;
        case JsonValueKind.Object:
            sb.Append(" {}\n");
            break;
        case JsonValueKind.Array when value.GetArrayLength() > 0:
            sb.Append('\n');
            Sequence(sb, value, indentless ? indent : indent + 2, indentless, plan);
            break;
        case JsonValueKind.Array:
            sb.Append(" []\n");
            break;
        case JsonValueKind.String when value.GetString()!.Contains('\n'):
            string text = value.GetString()!;
            sb.Append(text.EndsWith('\n') ? " |\n" : " |-\n");
            foreach (string line in text.TrimEnd('\n').Split('\n'))
            {
                sb.Append(' ', indent + 2).Append(line).Append('\n');
            }
            break;
        default:
            sb.Append(' ').Append(ScalarValue(value)).Append('\n');
            break;
    }
}

static void Sequence(StringBuilder sb, JsonElement array, int indent, bool indentless, AnchorPlan? plan = null)
{
    foreach (JsonElement item in array.EnumerateArray())
    {
        if (item.ValueKind == JsonValueKind.Object && item.EnumerateObject().Any())
        {
            // A compact mapping: the first key on the entry's line, the others under it.
            Mapping(sb, item, indent + 2, indentless, firstPrefix: new string(' ', indent) + "- ", plan);
        }
        else if (item.ValueKind == JsonValueKind.Array && item.GetArrayLength() > 0)
        {
            sb.Append(' ', indent).Append("-\n");
            Sequence(sb, item, indent + 2, indentless, plan);
        }
        else
        {
            sb.Append(' ', indent).Append("- ");
            sb.Append(item.ValueKind switch
            {
                JsonValueKind.Object => "{}",
                JsonValueKind.Array => "[]",
                _ => ScalarValue(item),
            }).Append('\n');
        }
    }
}

static string ScalarValue(JsonElement value) => value.ValueKind switch
{
    JsonValueKind.String => Scalar(value.GetString()!),
    JsonValueKind.Number => value.GetRawText(),
    JsonValueKind.True => "true",
    JsonValueKind.False => "false",
    _ => throw new InvalidOperationException("No nulls in the corpus."),
};

// Plain when no YAML reader can take the text for anything but a string; otherwise double-quoted with JSON escaping
// (valid in YAML too). Keys are never resolved to a type, so a key like 'on' (GitHub workflows) stays plain.
static string Scalar(string text, bool isKey = false)
{
    bool plain = Regex.IsMatch(text, @"^[A-Za-z][A-Za-z0-9 _./@+:%-]*$")
        && !text.EndsWith(' ') && !text.EndsWith(':')
        && !text.Contains(": ") && !text.Contains(" #")
        && (isKey || !Regex.IsMatch(text, @"^(y|n|yes|no|on|off|true|false|null)$", RegexOptions.IgnoreCase));
    return plain ? text : JsonSerializer.Serialize(text, new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
}

// The corpus must be free of what Styml cannot bind until M5, and of what not every library reads.
static void Check(string name, string yaml)
{
    foreach (string line in yaml.Split('\n'))
    {
        string t = line.TrimStart(' ', '-');
        if (t.StartsWith("---") || t.StartsWith("&") || t.StartsWith("*") || t.StartsWith("!") || t.StartsWith("<<") || t.StartsWith("? ") || Regex.IsMatch(line, @":\s+[&*!][^\s""]"))
        {
            throw new InvalidOperationException($"{name}: a construct the corpus excludes: {line}");
        }
    }
}

// M6b: which object and array values become anchors and aliases: those under the listed keys whose JSON text repeats.
sealed class AnchorPlan
{
    public AnchorPlan(HashSet<string> keys, string jsonText)
    {
        Keys = keys;
        var counts = new Dictionary<string, int>();
        using JsonDocument document = JsonDocument.Parse(jsonText);
        Tally(document.RootElement, counts);
        Repeated = counts.Where(p => p.Value > 1).Select(p => p.Key).ToHashSet();
    }

    public HashSet<string> Keys { get; }

    public HashSet<string> Repeated { get; }

    public Dictionary<string, string> Seen { get; } = new();

    public int Count { get; set; }

    private void Tally(JsonElement element, Dictionary<string, int> counts)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty property in element.EnumerateObject())
            {
                if (Keys.Contains(property.Name) && property.Value.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
                {
                    string raw = property.Value.GetRawText();
                    counts[raw] = counts.TryGetValue(raw, out int n) ? n + 1 : 1;
                }

                Tally(property.Value, counts);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in element.EnumerateArray())
            {
                Tally(item, counts);
            }
        }
    }

    // The anchor or alias for a value under `key`, if any: (" &aN", false) the first time, (" *aN", true) after.
    public (string Text, bool IsAlias)? For(string key, JsonElement value)
    {
        if (!Keys.Contains(key) || value.ValueKind is not (JsonValueKind.Object or JsonValueKind.Array))
        {
            return null;
        }

        string raw = value.GetRawText();
        if (Seen.TryGetValue(raw, out string? anchor))
        {
            Count++;
            return (" *" + anchor, true);
        }

        if (!Repeated.Contains(raw))
        {
            return null;
        }

        anchor = "a" + (Seen.Count + 1);
        Seen[raw] = anchor;
        return (" &" + anchor, false);
    }
}
