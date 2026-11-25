---
title: Scalar styles and quoting
description: When the writer quotes a string, the five scalar styles, and how to choose them per member or by default.
---

YAML has five ways to write a scalar: plain (`text`), single-quoted (`'text'`), double-quoted (`"text"`), literal
(`|`) and folded (`>`). The writer picks the one that reads back as the same string, and lets you choose another.

## What gets quoted

A string is written plain unless a YAML reader would read it as something else, or the syntax needs quotes. Those are
double-quoted:

- values of the YAML 1.2 core schema: numbers, booleans, `null` and `~`, and the empty string;
- values of YAML 1.1, which many readers still use: `yes`, `off`, dates, `0x1F`, `1_000`, sexagesimal numbers;
- an indicator at the start (`#`, `&`, `*`, `!`, `|`, `>`, `'`, `"`, `%`, `@`, `` ` ``, `{`, `[`, `,`, `:`, and `-` or
  `?` followed by a space: `-x` stays plain), `: ` and ` #` inside, and spaces at either end.

<!-- sample:quoting -->
```csharp
string[] values =
[
    "plain text", "42", "3.14", "true", "null", "~", "", "yes", "off", "2026-11-24",
    "0x1F", "1_000", "- item", "key: value", "a #comment", "@handle", "'quoted'", " padded ",
];
string yaml = YamlSerializer.Serialize(values);
```

```yaml
- plain text
- "42"
- "3.14"
- "true"
- "null"
- "~"
- ""
- "yes"
- "off"
- "2026-11-24"
- "0x1F"
- "1_000"
- "- item"
- "key: value"
- "a #comment"
- "@handle"
- "'quoted'"
- " padded "
```
<!-- /sample:quoting -->

`QuoteForLenientReaders` also quotes what some readers resolve although no YAML standard does, such as `nan` and
`Infinity` (read as numbers by Ruby's Psych), and case variants such as `NULL`:

<!-- sample:lenient -->
```csharp
string[] values = ["nan", "Infinity", "NULL", "True", "+12", "1e3"];
string strict = YamlSerializer.Serialize(values);
string lenient = YamlSerializer.Serialize(values, new YamlSerializerOptions { QuoteForLenientReaders = true });
```

```yaml
- nan
- Infinity
- "NULL"
- "True"
- "+12"
- "1e3"
---
- "nan"
- "Infinity"
- "NULL"
- "True"
- "+12"
- "1e3"
```
<!-- /sample:lenient -->

## Choose a style

A string with a line break is a literal block scalar by default; `MultiLineStringStyle` chooses `Literal`, `Folded` or
`DoubleQuoted`. `DefaultScalarStyle` sets the style of every other string, and `[YamlScalarStyle]` the style of one
member, which wins over both:

<!-- sample:styles-types -->
```csharp
public class Release
{
    public string Version { get; set; } = "2.1.0";

    [YamlScalarStyle(YamlScalarStyle.Folded)]
    public string Summary { get; set; } = "A release that fixes\nthe merge of anchors.";

    public string Notes { get; set; } = "- Faster reads\n- Smaller writes\n";

    [YamlScalarStyle(YamlScalarStyle.SingleQuoted)]
    public string Codename { get; set; } = "it's alive";
}
```
<!-- /sample:styles-types -->

<!-- sample:styles -->
```csharp
string defaults = YamlSerializer.Serialize(new Release());
string quoted = YamlSerializer.Serialize(new Release(), new YamlSerializerOptions
{
    MultiLineStringStyle = YamlScalarStyle.DoubleQuoted,
    DefaultScalarStyle = YamlScalarStyle.DoubleQuoted,
});
```

```yaml
Version: 2.1.0
Summary: >-
  A release that fixes

  the merge of anchors.
Notes: |
  - Faster reads
  - Smaller writes
Codename: 'it''s alive'
---
Version: "2.1.0"
Summary: >-
  A release that fixes

  the merge of anchors.
Notes: "- Faster reads\n- Smaller writes\n"
Codename: 'it''s alive'
```
<!-- /sample:styles -->

A style is a preference: when it can't hold the text, the writer chooses one that can. A plain style is not used for a
string that would read as another type, a literal or folded style applies to a string with a line break only, and a
single-quoted style can't hold some control characters. Numbers and booleans are always plain.

## Styles of parsed documents

`YamlDocument` and `YamlNode` remember the style of each scalar and collection they read, and write it back:
`'kept quoted'` stays single-quoted. `IgnoreSourceStyles` writes them in the options' styles instead. See
[the DOM](dom.md#styles).

The [playground](/styml/playground/) has every style option, and its tree view shows the style of each node.
