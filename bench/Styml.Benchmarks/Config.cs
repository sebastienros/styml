// Copyright (c) Sébastien Ros. Licensed under the MIT license.

using System;
using System.IO;
using System.Linq;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Exporters.Csv;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;

namespace Styml.Benchmarks
{
    public enum Form
    {
        Yaml,
        Json,
        MinJson,
    }

    /// <summary>The input a benchmark method reads, for the MB/s column. Methods without it read the YAML form.</summary>
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class InputAttribute(Form form) : Attribute
    {
        public Form Form { get; } = form;
    }

    internal static class Config
    {
        public static IConfig Create(bool dry)
        {
            string tfm = "net" + Environment.Version.ToString(2);
            // STYML_BENCH_RESULTS names the results folder under bench/results (default M6a), e.g. M3c-baseline.
            string set = Environment.GetEnvironmentVariable("STYML_BENCH_RESULTS") is { Length: > 0 } named ? named : "M6a";
            string artifacts = Path.GetFullPath(Path.Combine(Corpus.Directory, "..", "results", set, tfm));
            return DefaultConfig.Instance
                .AddJob(Job(dry ? BenchmarkDotNet.Jobs.Job.Dry : BenchmarkDotNet.Jobs.Job.ShortRun))
                .AddDiagnoser(MemoryDiagnoser.Default)
                .AddColumn(new ThroughputColumn(), new InputSizeColumn())
                .AddExporter(MarkdownExporter.GitHub, new CsvExporter(CsvSeparator.Comma, SummaryStyle.Default.WithTimeUnit(Perfolizer.Horology.TimeUnit.Nanosecond).WithSizeUnit(Perfolizer.Metrology.SizeUnit.B).WithCultureInfo(System.Globalization.CultureInfo.InvariantCulture)))
                .AddFilter(new BenchmarkDotNet.Filters.SimpleFilter(RunsOn))
                .WithArtifactsPath(artifacts)
                .WithOption(ConfigOptions.JoinSummary, false)
                .WithOrderer(new BenchmarkDotNet.Order.DefaultOrderer(BenchmarkDotNet.Order.SummaryOrderPolicy.Declared, BenchmarkDotNet.Order.MethodOrderPolicy.Declared));
        }

        // M6b: a [NotOnMerge] row does not run on the merge files.
        private static bool RunsOn(BenchmarkCase benchmark) =>
            !(benchmark.Descriptor.WorkloadMethod.IsDefined(typeof(NotOnMergeAttribute), false)
              && benchmark.Parameters.Items.FirstOrDefault(p => p.Name == "Document")?.Value is string document
              && Corpus.IsMerge(document));

        // The host's runtime, out of process with the stock toolchain (BenchmarkDotNet 0.16 knows .NET 11).
        // STYML_BENCH_LAUNCHES (M6b's confirmation run): that many launches of the job, for rows whose spread over one
        // launch's iterations is wide. Unset, the job is as before.
        private static BenchmarkDotNet.Jobs.Job Job(BenchmarkDotNet.Jobs.Job baseJob)
        {
            BenchmarkDotNet.Jobs.Job job = baseJob.WithRuntime(Environment.Version.Major >= 11 ? BenchmarkDotNet.Environments.CoreRuntime.Core11_0 : BenchmarkDotNet.Environments.CoreRuntime.Core10_0);
            if (int.TryParse(Environment.GetEnvironmentVariable("STYML_BENCH_LAUNCHES"), out int launches) && launches > 1)
            {
                job = job.WithLaunchCount(launches);
            }
            return job.AsDefault();
        }

        /// <summary>The bytes of an input named document.form (form: yaml, json, min.json).</summary>
        public static byte[] InputData(string input)
        {
            int dot = input.IndexOf('.');
            Doc doc = Corpus.Get(input[..dot]);
            return input[(dot + 1)..] switch
            {
                "json" => doc.Json,
                "min.json" => doc.MinJson ?? throw new InvalidOperationException("no minified form of " + input),
                _ => doc.Yaml,
            };
        }

        public static long InputBytes(BenchmarkCase benchmark)
        {
            if (benchmark.Parameters.Items.Any(p => p.Name == "Input"))
            {
                return InputData((string)benchmark.Parameters["Input"]).Length;
            }

            // A class whose Document is not a corpus file (KeyPoolRows' synthetic inputs) has no size: the columns say NA.
            if (benchmark.Parameters.Items.FirstOrDefault(p => p.Name == "Document")?.Value is not string name
                || !File.Exists(Path.Combine(Corpus.Directory, name + ".yaml")))
            {
                return -1;
            }

            Doc doc = Corpus.Get(name);
            Form form = benchmark.Descriptor.WorkloadMethod.GetCustomAttributes(typeof(InputAttribute), false).OfType<InputAttribute>().FirstOrDefault()?.Form ?? Form.Yaml;
            return form switch
            {
                Form.Json => doc.Json.Length,
                Form.MinJson => doc.MinJson!.Length,
                _ => doc.Yaml.Length,
            };
        }
    }

    /// <summary>MB/s (10^6 bytes per second) from the input size and the mean.</summary>
    internal sealed class ThroughputColumn : IColumn
    {
        public string Id => nameof(ThroughputColumn);
        public string ColumnName => "MB/s";
        public bool AlwaysShow => true;
        public ColumnCategory Category => ColumnCategory.Custom;
        public int PriorityInCategory => 0;
        public bool IsNumeric => true;
        public UnitType UnitType => UnitType.Dimensionless;
        public string Legend => "Throughput: input bytes (UTF-8) / mean, in 10^6 bytes per second";

        public string GetValue(Summary summary, BenchmarkCase benchmarkCase)
        {
            BenchmarkReport? report = summary[benchmarkCase];
            if (report?.ResultStatistics is not { } stats)
            {
                return "NA";
            }
            long bytes = Config.InputBytes(benchmarkCase);
            if (bytes < 0)
            {
                return "NA";
            }

            double mbPerSecond = bytes * 1e3 / stats.Mean;
            return mbPerSecond.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
        }

        public string GetValue(Summary summary, BenchmarkCase benchmarkCase, SummaryStyle style) => GetValue(summary, benchmarkCase);
        public bool IsDefault(Summary summary, BenchmarkCase benchmarkCase) => false;
        public bool IsAvailable(Summary summary) => true;
    }

    /// <summary>The input size in bytes, so the CSV carries it.</summary>
    internal sealed class InputSizeColumn : IColumn
    {
        public string Id => nameof(InputSizeColumn);
        public string ColumnName => "Input";
        public bool AlwaysShow => true;
        public ColumnCategory Category => ColumnCategory.Custom;
        public int PriorityInCategory => 1;
        public bool IsNumeric => true;
        public UnitType UnitType => UnitType.Dimensionless;
        public string Legend => "Input size in bytes (UTF-8)";

        public string GetValue(Summary summary, BenchmarkCase benchmarkCase) => Config.InputBytes(benchmarkCase) is >= 0 and long bytes ? bytes.ToString(System.Globalization.CultureInfo.InvariantCulture) : "NA";
        public string GetValue(Summary summary, BenchmarkCase benchmarkCase, SummaryStyle style) => GetValue(summary, benchmarkCase);
        public bool IsDefault(Summary summary, BenchmarkCase benchmarkCase) => false;
        public bool IsAvailable(Summary summary) => true;
    }
}
