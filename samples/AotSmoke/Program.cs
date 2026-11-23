// Copyright (c) Sébastien Ros. Licensed under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.Yaml;
using System.Text.Yaml.Nodes;
using System.Text.Yaml.Serialization;

int failures = 0;
void Check(bool condition, string name)
{
    Console.WriteLine($"{(condition ? "ok  " : "FAIL")} {name}");
    if (!condition)
    {
        failures++;
    }
}

var order = new Order(42, "Ada", [new Line("engine", 2, 9.5m), new Line("gear", 10, 0.25m)],
    new Dictionary<string, string> { ["priority"] = "high" }, Status.Shipped);

string yaml = YamlSerializer.Serialize(order, SmokeContext.Default.Order);
Order back = YamlSerializer.Deserialize(yaml, SmokeContext.Default.Order)!;
Check(back.Id == 42 && back.Customer == "Ada" && back.Status == Status.Shipped, "source-generated round trip: scalars and enum");
Check(back.Lines.Select(l => (l.Sku, l.Quantity, l.Price)).SequenceEqual(order.Lines.Select(l => (l.Sku, l.Quantity, l.Price))), "source-generated round trip: list");
Check(back.Tags["priority"] == "high", "source-generated round trip: dictionary");

YamlNode node = YamlNode.Parse("base: &b {a: 1}\nother: *b\n")!;
Check((int)node["other"]!["a"]! == 1, "DOM: alias");

var reader = new Utf8YamlReader("k: [1, 2]\n"u8);
int tokens = 0;
while (reader.Read())
{
    tokens++;
}
Check(tokens == 7, "reader: token count");

Check(!YamlSerializer.IsReflectionEnabledByDefault, "reflection off by the feature switch");
try
{
    Reflection.Serialize(order);
    Check(false, "reflection refused");
}
catch (InvalidOperationException)
{
    Check(true, "reflection refused");
}

Console.WriteLine(failures == 0 ? "all checks passed" : $"{failures} check(s) failed");
return failures == 0 ? 0 : 1;

internal static class Reflection
{
    // The call the feature switch must refuse; suppressed because refusing it is the check.
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "The smoke check expects the refusal.")]
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "The smoke check expects the refusal.")]
    public static string Serialize<T>(T value) => YamlSerializer.Serialize(value);
}

public enum Status { Pending, Shipped }

public record Line(string Sku, int Quantity, decimal Price);

public record Order(int Id, string Customer, List<Line> Lines, Dictionary<string, string> Tags, Status Status);

[YamlSerializable(typeof(Order))]
internal partial class SmokeContext : YamlSerializerContext;
