// Copyright (c) Sébastien Ros. Licensed under the MIT license.

// M3c gate 3: the in-process measurement of one flagged row of any
// single-segment class, deserialization rows included (bench/gate3-row.cs tokenizes only). It runs the benchmark
// method itself, on an instance set up as BenchmarkDotNet sets it up, after a timed warm-up of one second; the best
// of 40 timed batches of at least 2 ms, printed as microseconds per call, with four decimals since M4.0 (for the
// sub-microsecond writer rows). The baseline worktree runs a copy of this file against its own src/.
//   dotnet run -c Release -f net11.0 --no-build --project bench/Styml.Benchmarks -- gate3-row <Class> <Method> <Document>
// (for the sequence classes, <Document> is <input>@<segment size>, M7a).
// With a fifth argument, a number of seconds, it loops the row for that long instead of timing it, for a profiler
// attached from outside (dotnet-trace collect -- dotnet Styml.Benchmarks.dll gate3-row <Class> <Method> <Document> 20).
using System;
using System.Diagnostics;
using System.Reflection;
using BenchmarkDotNet.Attributes;

namespace Styml.Benchmarks
{
    internal static class Gate3Row
    {
        public static int Run(string[] args)
        {
            string className = args[1], methodName = args[2], document = args[3];
            Type type = typeof(Gate3Row).Assembly.GetType("Styml.Benchmarks." + className, throwOnError: true)!;
            object instance = Activator.CreateInstance(type)!;
            if (type.GetProperty("Document") is { } documentProperty)
            {
                documentProperty.SetValue(instance, document);
            }
            else
            {
                // M7a: the sequence classes (SequenceScenario), as <input>@<segment size>, e.g. k8s-l.yaml@4096.
                int at = document.IndexOf('@');
                type.GetProperty("Input")!.SetValue(instance, document[..at]);
                type.GetProperty("SegmentSize")!.SetValue(instance, int.Parse(document[(at + 1)..]));
            }
            foreach (MethodInfo setup in type.GetMethods())
            {
                if (setup.GetCustomAttribute<GlobalSetupAttribute>() is not null)
                {
                    setup.Invoke(instance, null);
                }
            }
            MethodInfo method = type.GetMethod(methodName)!;
            Action call = method.ReturnType == typeof(long)
                ? Bind<long>(method, instance)
                : Bind<object?>(method, instance);

            var clock = Stopwatch.StartNew();
            if (args.Length >= 5)
            {
                double seconds = double.Parse(args[4], System.Globalization.CultureInfo.InvariantCulture);
                long calls = 0;
                while (clock.Elapsed.TotalSeconds < seconds)
                {
                    call();
                    calls++;
                }
                Console.WriteLine(FormattableString.Invariant($"{className} {methodName} {document}: {calls} calls in {clock.Elapsed.TotalSeconds:F1} s"));
                return 0;
            }

            while (clock.Elapsed.TotalMilliseconds < 1000)
            {
                call();
            }
            int batch = 1;
            while (true)
            {
                clock.Restart();
                for (int i = 0; i < batch; i++)
                {
                    call();
                }
                if (clock.Elapsed.TotalMilliseconds >= 2)
                {
                    break;
                }
                batch *= 2;
            }
            double best = double.MaxValue;
            for (int round = 0; round < 40; round++)
            {
                clock.Restart();
                for (int i = 0; i < batch; i++)
                {
                    call();
                }
                best = Math.Min(best, clock.Elapsed.TotalMilliseconds * 1000 / batch);
            }
            Console.WriteLine(FormattableString.Invariant($"{className} {methodName} {document} {best:F4}"));
            return 0;
        }

        private static Action Bind<T>(MethodInfo method, object instance)
        {
            var target = (Func<T>)method.CreateDelegate(typeof(Func<T>), instance);
            return () => GC.KeepAlive(target());
        }
    }
}
