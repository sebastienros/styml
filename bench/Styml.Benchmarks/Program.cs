// Copyright (c) Sébastien Ros. Licensed under the MIT license.

using System;
using System.IO;
using System.Linq;
using BenchmarkDotNet.Running;

namespace Styml.Benchmarks
{
    public static class Program
    {
        // 'hash' prints the output hash of every Styml write row (gate G2).
        // 'check' runs the equal-work check on every document and exits; anything else goes to BenchmarkDotNet after the check.
        // The job is BenchmarkDotNet's ShortRun. A first argument 'dry' runs every benchmark once (the Dry job) instead, to
        // catch failures before a long run.
        public static int Main(string[] args)
        {
            // Every mode: the JSON-compatible rows and identity commands keep the default style JSON-compatible, checked.
            WriteLibraries.SetSwitch();
            if (args.Length == 4 && args[0] == "profile")
            {
                return Profile(args[1], Corpus.Get(args[2]), TimeSpan.FromSeconds(int.Parse(args[3])));
            }
            if (args.Length >= 1 && args[0] == "sequence-check")
            {
                return SequenceCheck.Run(args);
            }
            if (args.Length >= 1 && args[0] == "hash")
            {
                return WriteCheck.Hash(Console.Out);
            }
            if (args.Length >= 1 && args[0] == "hash-block-mixed")
            {
                return BlockCheck.HashBlockMixed(Console.Out);
            }
            if (args.Length >= 1 && args[0] == "hash-block")
            {
                return BlockCheck.HashBlock(Console.Out);
            }
            if (args.Length >= 1 && args[0] == "tree-dump")
            {
                return TreeDump.Run(args, Console.Out);
            }
            if (args.Length >= 2 && args[0] == "quote-dump")
            {
                return BlockCheck.QuoteDump(args[1], Console.Out);
            }
            if (args.Length >= 1 && args[0] == "key-pool-check")
            {
                return KeyPoolCheck.Run(Console.Out);
            }
            if (args.Length >= 1 && args[0] == "resolver-census")
            {
                return ResolverCensus.Run(Console.Out);
            }
            if (args.Length >= 1 && args[0] == "key-census")
            {
                return KeyCensus.Run(Console.Out);
            }
            if (args.Length >= 4 && args[0] == "gate3-row")
            {
                return Gate3Row.Run(args);
            }
            WriteLibraries.SetSwitch();
            // Relative to the working directory (the repository root for the identity outputs), so the output names no machine path.
            Console.WriteLine($"Corpus: {Path.GetRelativePath(Environment.CurrentDirectory, Corpus.Directory).Replace('\\', '/')}");
            Console.WriteLine($"Switch {WriteLibraries.JsonCompatibleSwitch}: {WriteLibraries.SwitchValue}");
            foreach (string name in Corpus.Names)
            {
                Check.Document(name, Console.Out);
            }
            // M6b's equal-work checks, after M6a's lines, which stay as they are.
            M6bCheck.Run(Console.Out);
            if (args.Length > 0 && args[0] == "check")
            {
                return 0;
            }
            bool dry = args.Length > 0 && args[0] == "dry";
            BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(dry ? args[1..] : args, Config.Create(dry));
            return 0;
        }

        // 'profile <scenario> <document> <seconds>' loops one scenario, for a profiler attached from outside
        // (dotnet-trace collect ... -- dotnet Styml.Benchmarks.dll profile ...). Not used by the benchmarks.
        private static Func<long> SerializeGenerated(Doc doc, StymlWriteContext context)
        {
            object graph = Libraries.Json(doc.Json, doc.Family)!;
            return () => WriteLibraries.StymlGeneratedWrite(graph, doc.Family, context) is null ? 0 : 1;
        }

        private static Func<long> SerializeReflection(Doc doc)
        {
            object graph = Libraries.Json(doc.Json, doc.Family)!;
            return () => WriteLibraries.Styml(graph, doc.Family, BlockLibraries.BlockOptions) is null ? 0 : 1;
        }

        private static int Profile(string scenario, Doc doc, TimeSpan duration)
        {
            Func<long> work = scenario switch
            {
                "tokenize-yaml" => () => Libraries.StymlTokens(doc.Yaml, Libraries.StymlReader, text: false),
                "tokenize-json" => () => Libraries.StymlTokens(doc.Json, Libraries.StymlReader, text: false),
                "tokenize-stj" => () => Libraries.JsonTokens(doc.Json, text: false),
                "deserialize-yaml" => () => Libraries.StymlGeneratedRead(doc.Yaml, doc.Family, Libraries.StymlGenerated) is null ? 0 : 1,
                "deserialize-stj" => () => Libraries.JsonGeneratedRead(doc.Json, doc.Family) is null ? 0 : 1,
                // M7d (item 2): DomParse's two Styml rows.
                "domparse-yaml" => () => Dom.StymlDocument(doc.Yaml),
                "domparse-json" => () => Dom.StymlDocument(doc.Json),
                // M7d (item 3): SerializeYamlSourceGen's Styml_Block row, and its Styml_Json row (the same handlers and
                // writer in JSON-compatible style, level with STJ since M6b) to set the block side's own cost apart.
                "serialize-block" => SerializeGenerated(doc, BlockLibraries.BlockGenerated),
                "serialize-json" => SerializeGenerated(doc, YamlWriteLibraries.JsonGenerated),
                // M7d (item 4): SerializeReflectionBlock's Styml_Block row, whose workflow documents write literal blocks.
                "serialize-reflection-block" => SerializeReflection(doc),
                _ => throw new ArgumentException("unknown scenario " + scenario),
            };
            var clock = System.Diagnostics.Stopwatch.StartNew();
            long n = 0, sum = 0;
            while (clock.Elapsed < duration)
            {
                sum += work();
                n++;
            }
            Console.WriteLine($"{scenario} {doc.Name}: {n} operations in {clock.Elapsed.TotalSeconds:F1} s ({sum})");
            return 0;
        }
    }
}
