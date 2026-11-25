namespace Styml.Playground;

/// <summary>A sample of the playground: a name and the state it loads.</summary>
public sealed record PlaygroundSample(string Id, string Name, PlaygroundState State);

/// <summary>The samples of the sample menu, each showing a feature of Styml.</summary>
public static class PlaygroundSamples
{
    /// <summary>The sample shown when the URL has no state.</summary>
    public static PlaygroundSample Default => All[0];

    /// <summary>Every sample, in the order of the menu.</summary>
    public static IReadOnlyList<PlaygroundSample> All { get; } =
    [
        new("compose", "Compose file", new PlaygroundState
        {
            Yaml = """
                # A Compose file: block mappings, sequences and a flow sequence.
                services:
                  web:
                    image: nginx:1.27
                    ports: ["80:80", "443:443"]
                    environment:
                      TIER: front
                      REPLICAS: "3"
                    depends_on:
                    - api
                  api:
                    image: example/api:2.4
                    command: [dotnet, Api.dll, --urls, "http://+:8080"]
                    healthcheck:
                      test: curl -f http://localhost:8080/health
                      interval: 30s
                """,
        }),
        new("anchors", "Anchors and merge keys", new PlaygroundState
        {
            Yaml = """
                # An anchor (&) names a node, an alias (*) refers to it. With "Merge keys" on, << merges mappings.
                defaults: &defaults
                  image: nginx:1.27
                  restart: always
                  labels: &labels [web, public]
                web:
                  <<: *defaults
                  restart: never
                admin:
                  <<: *defaults
                  labels: *labels
                """,
            View = PlaygroundView.Tree,
            Options = new() { ResolveMergeKeys = true },
        }),
        new("styles", "Scalar styles", new PlaygroundState
        {
            Yaml = """
                plain: text without quotes
                single: 'single-quoted, with '' escaped'
                double: "double-quoted, with \t escapes"
                literal: |
                  Line breaks
                  are kept.
                folded: >
                  Lines are folded
                  into one.
                number-as-text: "3"
                yaml11-boolean: yes
                """,
            View = PlaygroundView.Tree,
        }),
        new("schemas", "Schemas", new PlaygroundState
        {
            Yaml = """
                # Switch the schema in the options: Core (YAML 1.2), YAML 1.1, JSON or Failsafe.
                decimal: 12
                hex: 0x1F
                octal-1.1: 012
                octal-1.2: 0o14
                separated: 1_000
                binary: 0b101
                boolean-1.1: yes
                boolean: true
                tilde: ~
                """,
            View = PlaygroundView.Json,
            Options = new() { Schema = System.Text.Yaml.YamlSchemaKind.Yaml11 },
        }),
        new("documents", "Several documents", new PlaygroundState
        {
            Yaml = """
                # A stream of documents, separated by ---.
                apiVersion: v1
                kind: Service
                metadata:
                  name: web
                spec:
                  ports:
                  - port: 80
                ---
                apiVersion: apps/v1
                kind: Deployment
                metadata:
                  name: web
                spec:
                  replicas: 3
                """,
        }),
        new("tags", "Tags and directives", new PlaygroundState
        {
            Yaml = """
                %TAG !e! tag:example.com,2000:app/
                ---
                # Tags say what a node is. %TAG declares a handle for a prefix.
                shape: !e!circle
                  radius: 1.5
                local: !point {x: 1, y: 2}
                standard: !!str 123
                binary: !!binary aGVsbG8=
                """,
            View = PlaygroundView.Tree,
        }),
        new("collection-keys", "Collection keys", new PlaygroundState
        {
            Yaml = """
                # A key can be a collection, explicitly (? ...) or implicitly in flow style.
                ? [north, east]
                : quadrant 1
                [south, west]: quadrant 3
                ? {x: 0, y: 0}
                : origin
                """,
            View = PlaygroundView.Tree,
        }),
        new("json", "JSON input", new PlaygroundState
        {
            Yaml = """
                {
                  "name": "web", // JSON comments, read with the option on
                  "ports": [80, 443,],
                  "tls": { "enabled": true, "cert": null }
                }
                """,
            Options = new() { SkipJsonComments = true, IgnoreSourceStyles = true },
        }),
        new("errors", "An error", new PlaygroundState
        {
            Yaml = """
                # Duplicate keys are an error, as YAML 1.2 requires. Change "Duplicate keys" in the options.
                name: web
                port: 80
                name: api
                """,
        }),
    ];
}
