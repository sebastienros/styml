// Copyright (c) Sébastien Ros. Licensed under the MIT license.

#:package Microsoft.Diagnostics.Tracing.TraceEvent@3.1.23
#:property ManagePackageVersionsCentrally=false
// M3c gate 3: prints the JIT's inlining decisions for each compilation of one
// method, from an EventPipe trace with the runtime's JIT tracing events. The release JIT has no
// DOTNET_JitPrintInlinedMethods; these events carry the same decisions. Events are keyed by the method being compiled,
// since tier-1 compilations run on a background thread.
//   dotnet-trace collect --providers Microsoft-Windows-DotNETRuntime:0x1010:5 -o t.nettrace -- \
//       dotnet artifacts/m3c-swap/head/Styml.Benchmarks.dll gate3-row Tokenize Styml_Yaml records-l
//   dotnet run bench/m3c-inlining.cs -- t.nettrace StymlTokens
using System;
using System.Collections.Generic;
using Microsoft.Diagnostics.Tracing;

string root = args[1];
using var source = new EventPipeEventSource(args[0]);
var clr = source.Clr;
var pending = new List<string>();
void Add(string compiling, string line)
{
    if (compiling == root)
    {
        pending.Add(line);
    }
}
clr.MethodInliningSucceeded += e => Add(e.MethodBeingCompiledName, $"OK   {e.InlinerName} <- {e.InlineeName}");
clr.MethodInliningFailed += e => Add(e.MethodBeingCompiledName, $"FAIL {e.InlinerName} <- {e.InlineeName} : {e.FailReason}");
clr.MethodLoadVerbose += e =>
{
    if (e.MethodName != root)
    {
        return;
    }
    Console.WriteLine($"=== {e.MethodName} opt={e.OptimizationTier} size={e.MethodSize}");
    foreach (string line in pending)
    {
        Console.WriteLine(line);
    }
    pending.Clear();
};
source.Process();
