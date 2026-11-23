// Copyright (c) Sébastien Ros. Licensed under the MIT license.

#:project ../../src/Styml/Styml.csproj
#:property TargetFramework=net11.0
#:property PublishAot=false

// Writes the strings of the cross-read (eng/crossread/run.sh) in block style with QuoteForLenientReaders off and on, as
// block sequence items, flow sequence items and block mapping keys, one document per setting and position, and the
// strings themselves as JSON (strings.json), for the Psych and PyYAML readers to compare.
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Yaml;

string dir = args.Length > 0 ? args[0] : "artifacts/crossread";
int maxLength = args.Length > 1 ? int.Parse(args[1]) : 4;
Directory.CreateDirectory(dir);

var strings = new HashSet<string>(StringComparer.Ordinal);

// Every string up to maxLength characters over the characters the type patterns and Psych's scanner use.
const string Alphabet = "0189+-._:,xbeE";
var current = new List<string> { "" };
for (int length = 1; length <= maxLength; length++)
{
    var next = new List<string>();
    foreach (string prefix in current)
    {
        foreach (char c in Alphabet)
        {
            next.Add(prefix + c);
        }
    }

    strings.UnionWith(next);
    current = next;
}

// The keywords in every case, with signs and points.
foreach (string word in new[] { "y", "n", "yes", "no", "on", "off", "true", "false", "null", "inf", "nan", "infinity" })
{
    for (int mask = 0; mask < 1 << word.Length; mask++)
    {
        char[] chars = word.ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            if ((mask & (1 << i)) != 0)
            {
                chars[i] = char.ToUpperInvariant(chars[i]);
            }
        }

        string cased = new string(chars);
        foreach (string prefix in new[] { "", ".", "+.", "-.", "+", "-", ":" })
        {
            strings.Add(prefix + cased);
        }
    }
}

// Dates and timestamps, and their near misses.
foreach (string y in new[] { "2001", "200", "-2001", "-200", "+2001" })
foreach (string m in new[] { "12", "1", "123" })
foreach (string d in new[] { "14", "4", "32" })
{
    string date = y + "-" + m + "-" + d;
    strings.Add(date);
    foreach (string sep in new[] { "T", "t", " ", "  ", "\t" })
    foreach (string time in new[] { "21:59:43", "1:59:43", "21:59:4", "21:5:43" })
    foreach (string fraction in new[] { "", ".", ".10" })
    // Psych's TIME zone is [-+]\d{1,2}:?(?:\d\d)?, after optional white space; the last ones are near misses.
    foreach (string zone in new[] { "", "Z", " Z", "+5", "-05", "+05:00", "+0500", " -5", " +05:00", "-530", "+050", "+5:", "-05:", " +0500", "\t-5:", "+05:0", "+05000", " ", "Z " })
    {
        strings.Add(date + sep + time + fraction + zone);
    }
}

foreach (string s in new[] { "~", "<<", "=", "1,000", "12,5", "0,.", "1,0.", "09:30", "00:15:00", "0:30", "2024-3-1", ".e+0", ".E-1", ".e+32001", "0x_", "0b_", "-0x__", "1._", "0._0", "1_._" })
{
    strings.Add(s);
}

string[] list = strings.Order(StringComparer.Ordinal).ToArray();
File.WriteAllText(Path.Combine(dir, "strings.json"), JsonSerializer.Serialize(list));

foreach (bool lenient in new[] { false, true })
{
    var options = new YamlWriterOptions { Style = YamlWriterStyle.Block, NewLine = "\n", QuoteForLenientReaders = lenient };
    string setting = lenient ? "on" : "off";
    Write("items", w =>
    {
        w.WriteStartArray();
        foreach (string s in list)
        {
            w.WriteStringValue(s);
        }

        w.WriteEndArray();
    });
    Write("flow", w =>
    {
        w.WriteStartArray();
        foreach (string s in list)
        {
            w.WriteCollectionStyle(YamlCollectionStyle.Flow);
            w.WriteStartArray();
            w.WriteStringValue(s);
            w.WriteEndArray();
        }

        w.WriteEndArray();
    });
    Write("keys", w =>
    {
        w.WriteStartObject();
        foreach (string s in list)
        {
            w.WritePropertyName(s);
            w.WriteNumberValue(0);
        }

        w.WriteEndObject();
    });

    void Write(string position, Action<Utf8YamlWriter> calls)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var w = new Utf8YamlWriter(buffer, options))
        {
            calls(w);
        }

        File.WriteAllBytes(Path.Combine(dir, $"{setting}.{position}.yaml"), buffer.WrittenSpan.ToArray());
    }
}

Console.WriteLine($"{list.Length} strings written to {dir}");
