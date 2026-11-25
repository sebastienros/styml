---
title: The reader and the writer
description: Read YAML token by token with Utf8YamlReader, and write it with Utf8YamlWriter, anchors, tags, styles, comments and directives included.
---

`Utf8YamlReader` and `Utf8YamlWriter` are the layer under the serializer and the DOM: a forward-only reader of tokens
and a writer of tokens, over UTF-8 bytes, that allocate nothing. They are System.Text.Json's `Utf8JsonReader` and
`Utf8JsonWriter`, with YAML's information on each token.

## Read tokens

The reader returns System.Text.Json's token types (`StartObject`, `PropertyName`, `String`, `Number`...) and YAML's
own `Alias`, with properties for what YAML adds: `Anchor`, `Tag` and `GetExpandedTag()` (which applies `%TAG`
directives), `ScalarStyle`, `IsFlow` and `KeyKind`:

<!-- sample:reader -->
```csharp
byte[] utf8 = Encoding.UTF8.GetBytes("""
    base: &b !point {x: 1, y: 2}
    copy: *b
    note: >
      folded
      text
    """);
var reader = new Utf8YamlReader(utf8);
var lines = new List<string>();
while (reader.Read())
{
    string indent = new(' ', reader.CurrentDepth * 2);
    string anchor = reader.Anchor.IsEmpty ? "" : $" &{Encoding.UTF8.GetString(reader.Anchor.ToArray())}";
    string tag = reader.Tag.IsEmpty ? "" : $" {reader.GetExpandedTag()}";
    string value = reader.TokenType switch
    {
        YamlTokenType.PropertyName or YamlTokenType.String => $" {reader.GetString()!.Replace("\n", "\\n")} ({reader.ScalarStyle})",
        YamlTokenType.Number => $" {reader.GetInt32()}",
        YamlTokenType.Alias => $" *{Encoding.UTF8.GetString(reader.ValueSpan.ToArray())}",
        YamlTokenType.StartObject or YamlTokenType.StartArray => reader.IsFlow ? " (flow)" : " (block)",
        _ => "",
    };
    lines.Add($"{indent}{reader.TokenType}{anchor}{tag}{value}");
}
```

```text
StartObject (block)
  PropertyName base (Plain)
  StartObject &b !point (flow)
    PropertyName x (Plain)
    Number 1
    PropertyName y (Plain)
    Number 2
  EndObject
  PropertyName copy (Plain)
  Alias *b
  PropertyName note (Plain)
  String folded text\n (Folded)
EndObject
```
<!-- /sample:reader -->

The reader is a `ref struct` over a `ReadOnlySpan<byte>` or a `ReadOnlySequence<byte>`, for a document that arrives
in segments. As with `Utf8JsonReader`, a partial buffer is read with `isFinalBlock: false` and resumed from the
reader's `CurrentState`.

`YamlReaderOptions` sets the [schema](schemas.md), several documents (`AllowMultipleValues`), JSON comments
(`CommentHandling`), the maximum depth, lenient typed reads and collection keys.

## Write tokens

The writer writes block YAML by default. Before a value, `WriteAnchor`, `WriteTag`, `WriteScalarStyle` and
`WriteCollectionStyle` set what the value gets, and `WriteAliasValue` writes an alias in its place.
`WriteCommentValue` writes a comment, `WriteDirective`, `WriteDocumentStart` and `WriteDocumentEnd` the document's
frame, and `WriteStartPropertyKey`... `WriteEndPropertyKey` a collection key:

<!-- sample:writer -->
```csharp
using var buffer = new MemoryStream();
using (var writer = new Utf8YamlWriter(buffer, new YamlWriterOptions { NewLine = "\n" }))
{
    writer.WriteDirective("%YAML 1.2");
    writer.WriteDocumentStart();
    writer.WriteCommentValue("generated, do not edit", false);
    writer.WriteStartObject();

    writer.WritePropertyName("defaults");
    writer.WriteAnchor("defaults");
    writer.WriteStartObject();
    writer.WriteString("image", "nginx:1.27");
    writer.WriteEndObject();

    writer.WritePropertyName("web");
    writer.WriteTag("!service");
    writer.WriteStartObject();
    writer.WritePropertyName("base");
    writer.WriteAliasValue("defaults");
    writer.WritePropertyName("ports");
    writer.WriteCollectionStyle(YamlCollectionStyle.Flow);
    writer.WriteStartArray();
    writer.WriteNumberValue(80);
    writer.WriteNumberValue(443);
    writer.WriteEndArray();
    writer.WriteScalarStyle(YamlScalarStyle.Literal);
    writer.WriteString("script", "nginx -t\nnginx -s reload\n");
    writer.WriteEndObject();

    writer.WriteStartPropertyKey();
    writer.WriteCollectionStyle(YamlCollectionStyle.Flow);
    writer.WriteStartArray();
    writer.WriteStringValue("eu");
    writer.WriteStringValue("us");
    writer.WriteEndArray();
    writer.WriteEndPropertyKey();
    writer.WriteStringValue("two regions");

    writer.WriteEndObject();
    writer.WriteDocumentEnd();
}

string yaml = Encoding.UTF8.GetString(buffer.ToArray());
```

```yaml
%YAML 1.2
---
# generated, do not edit
defaults: &defaults
  image: nginx:1.27
web: !service
  base: *defaults
  ports: [80, 443]
  script: |
    nginx -t
    nginx -s reload
? [eu, us]
: two regions
...
```
<!-- /sample:writer -->

`YamlWriterOptions.Style = JsonCompatible` writes JSON with the same calls. Scalar and collection styles are then
ignored, comments are written as `/* */`, and a tag, an anchor, an alias or a collection key, which JSON can't hold,
throws an `InvalidOperationException`. See [Writing YAML](writing-yaml.md) for the layout options, which the writer
shares with the serializer.

## In converters

A [custom converter](converters.md) receives the reader and the writer, so it can read the tag and the style of a
value and choose those it writes.
