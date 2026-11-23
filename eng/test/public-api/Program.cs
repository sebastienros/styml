// Copyright (c) Sébastien Ros. Licensed under the MIT license.

// Usage: dotnet run --project eng/test/public-api -- <path/to/Styml.dll> [output.txt]
// Writes one line per public or protected type and member (System.Text.Yaml.Tests.PublicApiListing).
// Dependencies of a netstandard2.0 build that are not in the shared framework are loaded from the NuGet cache.
using System.Reflection;
using System.Text.Yaml.Tests;

string path = Path.GetFullPath(args[0]);
string nuget = Environment.GetEnvironmentVariable("NUGET_PACKAGES")
    ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages");

AppDomain.CurrentDomain.AssemblyResolve += (_, e) =>
{
    var name = new AssemblyName(e.Name);
    string local = Path.Combine(Path.GetDirectoryName(path)!, name.Name + ".dll");
    if (File.Exists(local))
        return Assembly.LoadFrom(local);
    string package = Path.Combine(nuget, name.Name!.ToLowerInvariant());
    if (!Directory.Exists(package))
        return null;
    string? match = Directory.GetFiles(package, name.Name + ".dll", SearchOption.AllDirectories)
        .Where(f => f.Contains($"{Path.DirectorySeparatorChar}lib{Path.DirectorySeparatorChar}netstandard2"))
        .OrderByDescending(f => f, StringComparer.Ordinal).FirstOrDefault();
    return match is null ? null : Assembly.LoadFrom(match);
};

Assembly assembly = Assembly.LoadFrom(path);
string[] lines = PublicApiListing.List(assembly).ToArray();
string header = $"# Public API of {Path.GetFileName(path)} ({PublicApiListing.TargetMoniker(assembly)}), listed by eng/test/public-api.";
if (args.Length > 1)
    File.WriteAllLines(args[1], new[] { header }.Concat(lines));
else
    foreach (string line in lines) Console.WriteLine(line);
