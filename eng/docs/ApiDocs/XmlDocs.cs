using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Styml.ApiDocs;

/// <summary>The XML documentation of the assembly, with <c>inheritdoc</c> resolved, rendered as Markdown.</summary>
internal sealed partial class XmlDocs
{
    private readonly Dictionary<string, XElement> _members;
    private readonly Func<string, (string Text, string? Url)> _resolve;

    /// <param name="path">The XML documentation file.</param>
    /// <param name="resolve">The text and the URL of a <c>cref</c> (a documentation comment ID).</param>
    public XmlDocs(string path, Func<string, (string Text, string? Url)> resolve)
    {
        _members = XDocument.Load(path).Root!.Element("members")!.Elements("member")
            .GroupBy(m => (string)m.Attribute("name")!)
            .ToDictionary(g => g.Key, g => g.First());
        _resolve = resolve;
    }

    /// <summary>The documentation of a member, its own or the one it inherits; null when it has none.</summary>
    public XElement? Get(MemberInfo member) => Get(DocIds.Of(member), member, 0);

    private XElement? Get(string id, MemberInfo? member, int depth)
    {
        if (depth > 8 || !_members.TryGetValue(id, out XElement? element))
        {
            return member is null ? null : Inherited(member, depth);
        }

        if (element.Element("inheritdoc") is not { } inheritdoc)
        {
            return element;
        }

        XElement? source = inheritdoc.Attribute("cref") is { } cref
            ? Get(cref.Value, null, depth + 1)
            : member is null ? null : Inherited(member, depth);

        // The member's own elements win over the inherited ones.
        var merged = new XElement("member");
        foreach (XElement child in source?.Elements() ?? [])
        {
            merged.Add(child);
        }

        foreach (XElement child in element.Elements().Where(e => e.Name != "inheritdoc"))
        {
            merged.Elements(child.Name).Where(e => SameTarget(e, child)).Remove();
            merged.Add(child);
        }

        return merged;
    }

    private static bool SameTarget(XElement a, XElement b) =>
        (string?)a.Attribute("name") == (string?)b.Attribute("name") && (string?)a.Attribute("cref") == (string?)b.Attribute("cref");

    // The documentation a member inherits: from the member it overrides, from the interface member it implements, or for a
    // type from its base type.
    private XElement? Inherited(MemberInfo member, int depth)
    {
        foreach (MemberInfo source in InheritedFrom(member))
        {
            if (source.Module.Assembly == member.Module.Assembly && Get(DocIds.Of(source), source, depth + 1) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    private static IEnumerable<MemberInfo> InheritedFrom(MemberInfo member)
    {
        switch (member)
        {
            case MethodInfo method:
                if (method.GetBaseDefinition() is { } baseMethod && baseMethod != method)
                {
                    yield return baseMethod;
                }

                foreach (MethodInfo implemented in Implemented(method))
                {
                    yield return implemented;
                }

                break;
            case PropertyInfo property when (property.GetMethod ?? property.SetMethod) is { } first:
                var candidates = new List<MethodInfo>();
                if (first.GetBaseDefinition() is { } baseAccessor && baseAccessor != first)
                {
                    candidates.Add(baseAccessor);
                }

                candidates.AddRange(Implemented(first));
                foreach (MethodInfo candidate in candidates)
                {
                    if (candidate.DeclaringType!.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
                        .FirstOrDefault(p => p.GetMethod == candidate || p.SetMethod == candidate) is { } inheritedProperty)
                    {
                        yield return inheritedProperty;
                    }
                }

                break;
            case Type type:
                if (type.BaseType is { } baseType)
                {
                    yield return baseType;
                }

                foreach (Type i in type.GetInterfaces())
                {
                    yield return i;
                }

                break;
        }
    }

    private static IEnumerable<MethodInfo> Implemented(MethodInfo method)
    {
        Type type = method.DeclaringType!;
        if (type.IsInterface)
        {
            yield break;
        }

        foreach (Type i in type.GetInterfaces())
        {
            InterfaceMapping map;
            try
            {
                map = type.GetInterfaceMap(i);
            }
            catch (ArgumentException)
            {
                continue;
            }

            for (int k = 0; k < map.TargetMethods.Length; k++)
            {
                if (map.TargetMethods[k] == method)
                {
                    yield return map.InterfaceMethods[k];
                }
            }
        }
    }

    /// <summary>The Markdown of the content of an element (a summary, a remarks section, a parameter).</summary>
    public string Markdown(XElement? element)
    {
        if (element is null)
        {
            return "";
        }

        var text = new StringBuilder();
        foreach (XNode node in element.Nodes())
        {
            Render(node, text);
        }

        return Tidy(text.ToString());
    }

    /// <summary>The first sentence of a summary, as plain text, for descriptions and tables.</summary>
    public string Plain(XElement? element)
    {
        string text = Markdown(element);
        text = MarkdownLink().Replace(text, "$1");
        text = text.Replace("`", "").Replace("**", "");
        // Undo the escapes of Escape (a backslash before punctuation), keeping other backslashes.
        text = Regex.Replace(text, @"\\([\\`*_\[\]<>|])", "$1");
        text = WebUtility(text);
        text = Regex.Replace(text, @"\s+", " ").Trim();
        return text;
    }

    private static string WebUtility(string text) => System.Net.WebUtility.HtmlDecode(text);

    private void Render(XNode node, StringBuilder text)
    {
        switch (node)
        {
            case XText t:
                text.Append(Escape(Regex.Replace(t.Value, @"\s+", " ")));
                break;
            case XElement e:
                RenderElement(e, text);
                break;
        }
    }

    private void RenderElement(XElement e, StringBuilder text)
    {
        switch (e.Name.LocalName)
        {
            case "c":
                text.Append(Code(e.Value));
                break;
            case "code":
                text.Append("\n\n```csharp\n").Append(Dedent(e.Value)).Append("\n```\n\n");
                break;
            case "see" or "seealso" when e.Attribute("langword") is { } langword:
                text.Append(Code(langword.Value));
                break;
            case "see" or "seealso" when e.Attribute("href") is { } href:
                string label = e.Value.Trim();
                text.Append('[').Append(label.Length > 0 ? Escape(label) : href.Value).Append("](").Append(href.Value).Append(')');
                break;
            case "see" or "seealso" when e.Attribute("cref") is { } cref:
                (string name, string? url) = _resolve(cref.Value);
                string shown = e.Value.Trim().Length > 0 ? Escape(e.Value.Trim()) : Code(name);
                text.Append(url is null ? shown : $"[{shown}]({url})");
                break;
            case "paramref" or "typeparamref":
                text.Append(Code((string?)e.Attribute("name") ?? ""));
                break;
            case "para":
                text.Append("\n\n");
                foreach (XNode child in e.Nodes())
                {
                    Render(child, text);
                }

                text.Append("\n\n");
                break;
            case "br":
                text.Append("<br>");
                break;
            case "b" or "strong":
                text.Append("**").Append(Markdown(e)).Append("**");
                break;
            case "i" or "em":
                text.Append('*').Append(Markdown(e)).Append('*');
                break;
            case "list":
                RenderList(e, text);
                break;
            default:
                foreach (XNode child in e.Nodes())
                {
                    Render(child, text);
                }

                break;
        }
    }

    private void RenderList(XElement list, StringBuilder text)
    {
        string type = (string?)list.Attribute("type") ?? "bullet";
        text.Append("\n\n");
        if (type == "table")
        {
            XElement? header = list.Element("listheader");
            string[] heads = header is null ? ["", ""] : [Markdown(header.Element("term")), Markdown(header.Element("description"))];
            text.Append("| ").Append(heads[0]).Append(" | ").Append(heads[1]).Append(" |\n| --- | --- |\n");
            foreach (XElement item in list.Elements("item"))
            {
                text.Append("| ").Append(Cell(Markdown(item.Element("term")))).Append(" | ").Append(Cell(Markdown(item.Element("description")))).Append(" |\n");
            }
        }
        else
        {
            int n = 1;
            foreach (XElement item in list.Elements("item"))
            {
                string term = Markdown(item.Element("term"));
                // An item without a description is its own content, without the term already written.
                string description = item.Element("description") is { } d
                    ? Markdown(d)
                    : Markdown(new XElement("item", item.Nodes().Where(n => n is not XElement { Name.LocalName: "term" })));
                string content = term.Length > 0 ? $"**{term}**: {description}" : description;
                text.Append(type == "number" ? $"{n++}. " : "- ").Append(Regex.Replace(content.Trim(), @"\n+", " ")).Append('\n');
            }
        }

        text.Append('\n');
    }

    /// <summary>Text for a table cell: one line, pipes escaped.</summary>
    public static string Cell(string markdown) => Regex.Replace(markdown, @"\s*\n\s*", " ").Replace("|", "\\|").Trim();

    private static string Code(string value)
    {
        value = Regex.Replace(value, @"\s+", " ").Trim();
        return value.Contains('`') ? "`` " + value + " ``" : "`" + value + "`";
    }

    // Escapes what Markdown would read as markup in plain text: HTML, emphasis and links.
    private static string Escape(string text) =>
        text.Replace("\\", "\\\\").Replace("<", "&lt;").Replace(">", "&gt;").Replace("*", "\\*").Replace("_", "\\_")
            .Replace("[", "\\[").Replace("]", "\\]").Replace("`", "\\`").Replace("|", "\\|");

    private static string Dedent(string code)
    {
        string[] lines = code.Replace("\r\n", "\n").Trim('\n').Split('\n');
        int cut = lines.Where(l => l.Trim().Length > 0).Select(l => l.Length - l.TrimStart().Length).DefaultIfEmpty(0).Min();
        return string.Join("\n", lines.Select(l => l.Length >= cut ? l[cut..].TrimEnd() : l.Trim()));
    }

    // Joins the text into paragraphs: no spaces at the ends of lines, at most one blank line, fences kept as they are.
    private static string Tidy(string text)
    {
        var output = new StringBuilder();
        bool fence = false;
        int blank = 0;
        foreach (string raw in text.Split('\n'))
        {
            if (raw.StartsWith("```", StringComparison.Ordinal))
            {
                fence = !fence;
            }

            string line = fence || raw.StartsWith("```", StringComparison.Ordinal) ? raw : raw.Trim();
            if (line.Length == 0 && !fence)
            {
                blank++;
                continue;
            }

            if (output.Length > 0)
            {
                output.Append(blank > 0 ? "\n\n" : "\n");
            }

            output.Append(line);
            blank = 0;
        }

        return output.ToString().Trim();
    }

    [GeneratedRegex(@"\[([^\]]*)\]\([^)]*\)")]
    private static partial Regex MarkdownLink();
}
