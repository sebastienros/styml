// Copyright (c) Sébastien Ros. Licensed under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Styml.Benchmarks
{
    public enum Family
    {
        K8s,
        Compose,
        Workflow,
        OpenApi,
        Records,
    }

    /// <summary>One corpus document in its forms (bench/corpus).</summary>
    public sealed class Doc
    {
        public required string Name { get; init; }
        public required Family Family { get; init; }
        public required byte[] Yaml { get; init; }
        public required string YamlText { get; init; }
        public required byte[] Json { get; init; }
        public required string JsonText { get; init; }
        public byte[]? MinJson { get; init; }

        public override string ToString() => Name;
    }

    public static class Corpus
    {
        /// <summary>The documents, in the order of the report's columns.</summary>
        public static readonly string[] Names = Filter(new[]
        {
            "workflow-s", "compose-s", "k8s-s", "records-s",
            "workflow-m", "compose-m", "k8s-m", "openapi-m", "records-m",
            "openapi-l", "k8s-l", "records-l",
        });

        /// <summary>M6b's anchors documents: a list of their own, so that no
        /// class over <see cref="Names"/> and no identity output changes.</summary>
        public static readonly string[] AnchorNames = Filter(new[] { "anchors-s", "anchors-m", "merge-s", "merge-m" });

        /// <summary>The anchors documents with shared instances (no merge keys), for AnchorsSerialize.</summary>
        public static readonly string[] SharedNames = Array.FindAll(AnchorNames, n => n.StartsWith("anchors-", StringComparison.Ordinal));

        public static bool IsMerge(string name) => name.StartsWith("merge-", StringComparison.Ordinal);

        // STYML_BENCH_DOCS (M6b): a comma-separated list of document names. Unset, every list is as before.
        internal static string[] Filter(string[] names)
        {
            string? docs = Environment.GetEnvironmentVariable("STYML_BENCH_DOCS");
            if (string.IsNullOrWhiteSpace(docs))
            {
                return names;
            }
            var keep = new HashSet<string>(docs.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
            return Array.FindAll(names, keep.Contains);
        }

        private static readonly Dictionary<string, Doc> s_docs = new();

        public static string Directory { get; } = FindCorpus();

        private static string FindCorpus()
        {
            string? dir = AppContext.BaseDirectory;
            while (dir != null)
            {
                string candidate = Path.Combine(dir, "bench", "corpus");
                if (File.Exists(Path.Combine(candidate, "generate.cs")))
                {
                    return candidate;
                }
                dir = Path.GetDirectoryName(dir);
            }
            throw new InvalidOperationException("bench/corpus not found above " + AppContext.BaseDirectory);
        }

        public static Doc Get(string name)
        {
            lock (s_docs)
            {
                if (!s_docs.TryGetValue(name, out Doc? doc))
                {
                    byte[] yaml = File.ReadAllBytes(Path.Combine(Directory, name + ".yaml"));
                    byte[] json = File.ReadAllBytes(Path.Combine(Directory, name + ".json"));
                    string min = Path.Combine(Directory, name + ".min.json");
                    doc = new Doc
                    {
                        Name = name,
                        Family = name[..name.IndexOf('-')] switch
                        {
                            "k8s" => Family.K8s,
                            "compose" => Family.Compose,
                            "workflow" => Family.Workflow,
                            "openapi" => Family.OpenApi,
                            "anchors" or "merge" => Family.Compose,
                            _ => Family.Records,
                        },
                        Yaml = yaml,
                        YamlText = Encoding.UTF8.GetString(yaml),
                        Json = json,
                        JsonText = Encoding.UTF8.GetString(json),
                        MinJson = File.Exists(min) ? File.ReadAllBytes(min) : null,
                    };
                    s_docs[name] = doc;
                }
                return doc;
            }
        }
    }
}
