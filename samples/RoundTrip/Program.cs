// Copyright (c) Sébastien Ros. Licensed under the MIT license.

// The consumer sample: the README's examples, run against the project or, with -p:StymlPackageVersion=<version>,
// against the packed Styml package (see RoundTrip.csproj).

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Yaml;
using System.Text.Yaml.Nodes;
using System.Text.Yaml.Serialization;

var service = new Service("web", "nginx:1.27", [80, 443], new() { ["TIER"] = "front", ["REPLICAS"] = "3" });

// Reflection, as YamlSerializer.Serialize<T> and Deserialize<T>.
string yaml = YamlSerializer.Serialize(service);
Console.WriteLine(yaml);
Service back = YamlSerializer.Deserialize<Service>(yaml)!;

// Source generation, through a YamlSerializerContext.
string generated = YamlSerializer.Serialize(service, SampleContext.Default.Service);
Service backGenerated = YamlSerializer.Deserialize(generated, SampleContext.Default.Service)!;

// The DOM, with an anchor, an alias and a merge key.
YamlNode root = YamlNode.Parse(
    """
    defaults: &defaults
      image: nginx:1.27
      restart: always
    web:
      <<: *defaults
      ports: [80, 443]
    """,
    documentOptions: new YamlDocumentOptions { ResolveMergeKeys = true })!;
Console.WriteLine($"web.image = {root["web"]!["image"]}, web.restart = {root["web"]!["restart"]}");

// The reader, token by token.
var reader = new Utf8YamlReader("name: web\nports: [80, 443]\n"u8);
var tokens = new List<string>();
while (reader.Read())
{
    tokens.Add(reader.TokenType is YamlTokenType.PropertyName or YamlTokenType.String ? $"{reader.TokenType}:{reader.GetString()}" : reader.TokenType.ToString());
}
Console.WriteLine(string.Join(" ", tokens));

bool same = Same(back, service) && Same(backGenerated, service) && generated == yaml;
Console.WriteLine($"round trip equal: {same}");
#if NET
Console.WriteLine($"in-box System.Text.Json alongside: {System.Text.Json.JsonSerializer.Serialize(service)}");
#endif
return same ? 0 : 1;

static bool Same(Service a, Service b) =>
    a.Name == b.Name && a.Image == b.Image && a.Ports.SequenceEqual(b.Ports)
    && a.Environment.OrderBy(p => p.Key).SequenceEqual(b.Environment.OrderBy(p => p.Key));

public record Service(string Name, string Image, int[] Ports, Dictionary<string, string> Environment);

[YamlSerializable(typeof(Service))]
internal partial class SampleContext : YamlSerializerContext;
