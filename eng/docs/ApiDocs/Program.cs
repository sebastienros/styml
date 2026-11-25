// Writes the API reference of the documentation site: one Markdown page per public type of Styml, an index of the
// namespaces, and the folder data (api.yml) that gives the pages their layout and their navigation. See ApiDocs.csproj.

using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Yaml;
using Styml.ApiDocs;

if (args.Length != 1)
{
    Console.Error.WriteLine("usage: ApiDocs <output folder>   (docs/_content/styml/api)");
    return 1;
}

const string BaseUrl = "/styml/api/";
string output = Path.GetFullPath(args[0]);
Assembly assembly = typeof(YamlSerializer).Assembly;
string xmlPath = Path.ChangeExtension(assembly.Location, ".xml");

Type[] types = assembly.GetExportedTypes()
    .Where(t => !Signatures.IsHidden(t) && !t.IsDefined(typeof(CompilerGeneratedAttribute)))
    .OrderBy(t => t.Namespace, StringComparer.Ordinal)
    .ThenBy(t => Signatures.DisplayName(t), StringComparer.Ordinal)
    .ToArray();
var typesById = types.ToDictionary(DocIds.Of);

// The members of every type, by the anchor of their section, so that a cref links to a section that exists.
var members = types.ToDictionary(t => t, Members);
var anchors = types.ToDictionary(t => t, t => new HashSet<string>(members[t].Select(g => g.Anchor)));

var docs = new XmlDocs(xmlPath, Resolve);
var undocumented = new List<string>();

// The folder is replaced, so it must be one this program wrote (with its api.yml), or empty: a mistyped path must not
// delete another folder.
if (Directory.Exists(output))
{
    if (!File.Exists(Path.Combine(output, "api.yml")) && Directory.EnumerateFileSystemEntries(output).Any())
    {
        Console.Error.WriteLine($"{output} is not empty and was not written by ApiDocs (no api.yml): refusing to replace it.");
        return 1;
    }

    Directory.Delete(output, recursive: true);
}

Directory.CreateDirectory(output);
foreach (Type type in types)
{
    File.WriteAllText(Path.Combine(output, Slug(type) + ".md"), Page(type));
}

File.WriteAllText(Path.Combine(output, "index.md"), Index());
File.WriteAllText(Path.Combine(output, "api.yml"), FolderData());
// A member without documentation is usually a documentation ID the generator computed differently from the compiler.
Console.WriteLine($"Wrote {types.Length} type pages to {output}; {undocumented.Count} members without documentation");
foreach (string member in undocumented.Take(40))
{
    Console.WriteLine("  " + member);
}

return 0;

static string Slug(Type type) => type.FullName!.Replace('+', '.').Replace('`', '-').ToLowerInvariant();

static string NamespaceAnchor(string? ns) => (ns ?? "global").Replace('.', '-').ToLowerInvariant();

static string Quote(string text) => "\"" + text.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

// The members of a type that the page shows, grouped by section and name: overloads share a section.
static List<MemberGroup> Members(Type type)
{
    const BindingFlags flags = BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    var list = new List<(string Kind, string Name, string Anchor, MemberInfo Member)>();
    bool visibleProtected = !type.IsSealed;

    foreach (ConstructorInfo ctor in type.GetConstructors(flags))
    {
        // A delegate's constructor is the compiler's (object, IntPtr), which no code calls.
        if (!ctor.IsStatic && Visible(ctor) && !Signatures.IsHidden(ctor) && !(type.IsValueType && ctor.GetParameters().Length == 0)
            && !typeof(Delegate).IsAssignableFrom(type))
        {
            list.Add(("Constructors", "Constructors", "constructors", ctor));
        }
    }

    foreach (PropertyInfo property in type.GetProperties(flags))
    {
        MethodInfo? accessor = property.GetMethod is { } g && Visible(g) ? g : property.SetMethod is { } s && Visible(s) ? s : null;
        if (accessor is not null && !Signatures.IsHidden(property) && !property.Name.Contains('.') && !property.IsDefined(typeof(CompilerGeneratedAttribute)))
        {
            bool indexer = property.GetIndexParameters().Length > 0;
            list.Add(("Properties", indexer ? "this[]" : property.Name, property.Name.ToLowerInvariant(), property));
        }
    }

    foreach (MethodInfo method in type.GetMethods(flags))
    {
        if (!Visible(method) || Signatures.IsHidden(method) || method.Name.Contains('.') || method.Name.Contains('<')
            || method.IsDefined(typeof(CompilerGeneratedAttribute)))
        {
            continue;
        }

        if (method.IsSpecialName && method.Name.StartsWith("op_", StringComparison.Ordinal))
        {
            list.Add(("Operators", OperatorName(method.Name), method.Name.ToLowerInvariant(), method));
        }
        else if (!method.IsSpecialName && !(typeof(Delegate).IsAssignableFrom(type)))
        {
            list.Add(("Methods", method.Name, method.Name.ToLowerInvariant(), method));
        }
    }

    foreach (FieldInfo field in type.GetFields(flags))
    {
        if (Signatures.IsVisible(field) && !field.IsSpecialName && !Signatures.IsHidden(field) && !field.IsDefined(typeof(CompilerGeneratedAttribute)))
        {
            list.Add(("Fields", field.Name, field.Name.ToLowerInvariant(), field));
        }
    }

    foreach (EventInfo @event in type.GetEvents(flags))
    {
        if (@event.AddMethod is { } add && Visible(add) && !Signatures.IsHidden(@event))
        {
            list.Add(("Events", @event.Name, @event.Name.ToLowerInvariant(), @event));
        }
    }

    string[] order = ["Constructors", "Properties", "Methods", "Operators", "Events", "Fields"];
    return list
        .GroupBy(m => (m.Kind, m.Name, m.Anchor))
        .OrderBy(g => Array.IndexOf(order, g.Key.Kind))
        .ThenBy(g => type.IsEnum && g.Key.Kind == "Fields" ? 0 : 1)
        .ThenBy(g => type.IsEnum ? "" : g.Key.Name, StringComparer.Ordinal)
        .Select(g => new MemberGroup(g.Key.Kind, g.Key.Name, g.Key.Anchor, g.Select(m => m.Member).ToList()))
        .ToList();

    bool Visible(MethodBase method) => method.IsPublic || (visibleProtected && (method.IsFamily || method.IsFamilyOrAssembly));
}

static string OperatorName(string name) => name switch
{
    "op_Implicit" => "Implicit operator",
    "op_Explicit" => "Explicit operator",
    "op_Equality" => "Equality operator (==)",
    "op_Inequality" => "Inequality operator (!=)",
    _ => name[3..] + " operator",
};

// The text and the URL of a cref: a page of this reference, a page of Microsoft Learn for .NET types, or no link.
(string Text, string? Url) Resolve(string cref)
{
    if (cref.Length < 2 || cref[1] != ':')
    {
        return (cref, null);
    }

    char kind = cref[0];
    string name = cref[2..];
    int paren = name.IndexOf('(');
    if (paren >= 0)
    {
        name = name[..paren];
    }

    int tilde = name.IndexOf('~');
    if (tilde >= 0)
    {
        name = name[..tilde];
    }

    if (kind == 'T')
    {
        if (typesById.TryGetValue(cref, out Type? type))
        {
            return (Signatures.DisplayName(type), Slug(type) + ".md");
        }

        return (ShortTypeName(name), name.StartsWith("System.", StringComparison.Ordinal) || name.StartsWith("Microsoft.", StringComparison.Ordinal) ? Learn(name) : null);
    }

    int dot = name.LastIndexOf('.');
    if (dot < 0)
    {
        return (name, null);
    }

    string typeName = name[..dot];
    string member = name[(dot + 1)..];
    int arity = member.IndexOf("``", StringComparison.Ordinal);
    if (arity >= 0)
    {
        member = member[..arity];
    }

    string anchor = member switch
    {
        "#ctor" => "constructors",
        _ => member.ToLowerInvariant(),
    };
    string memberText = member == "#ctor" ? "constructor" : member;
    if (typesById.TryGetValue("T:" + typeName, out Type? owner))
    {
        string text = Signatures.DisplayName(owner) + "." + memberText;
        return (text, Slug(owner) + ".md" + (anchors[owner].Contains(anchor) ? "#" + anchor : ""));
    }

    return (ShortTypeName(typeName) + "." + memberText,
        typeName.StartsWith("System.", StringComparison.Ordinal) && !typeName.StartsWith("System.Text.Yaml", StringComparison.Ordinal)
            ? Learn(typeName + "." + member.Replace("#ctor", "-ctor"))
            : null);
}

static string ShortTypeName(string name)
{
    string last = name[(name.LastIndexOf('.') + 1)..];
    int arity = last.IndexOf('`');
    return arity < 0 ? last : last[..arity] + "<" + string.Join(", ", Enumerable.Range(0, int.Parse(last[(arity + 1)..])).Select(i => i == 0 ? "T" : "T" + (i + 1))) + ">";
}

static string Learn(string name) => "https://learn.microsoft.com/dotnet/api/" + name.Replace('`', '-').ToLowerInvariant();

string Page(Type type)
{
    var doc = docs.Get(type);
    string kind = Signatures.Kind(type);
    string title = Signatures.DisplayName(type) + " " + kind;
    string summary = docs.Markdown(doc?.Element("summary"));
    string description = docs.Plain(doc?.Element("summary"));
    if (description.Length == 0)
    {
        description = $"The {Signatures.DisplayName(type)} {kind} of {type.Namespace}.";
    }

    var page = new StringBuilder();
    page.Append("---\n")
        .Append("title: ").Append(Quote(title)).Append('\n')
        .Append("description: ").Append(Quote(description)).Append('\n')
        .Append("---\n\n");

    page.Append("**Namespace:** [").Append(type.Namespace).Append("](index.md#").Append(NamespaceAnchor(type.Namespace)).Append(") · **Assembly:** Styml.dll\n\n");
    page.Append("```csharp\n").Append(Signatures.Declaration(type)).Append("\n```\n\n");

    // The inheritance chain and the interfaces, linked to their pages.
    if (!type.IsInterface && !type.IsEnum && !type.IsValueType)
    {
        var chain = new List<Type>();
        for (Type? t = type.BaseType; t is not null; t = t.BaseType)
        {
            chain.Insert(0, t);
        }

        if (chain.Count > 1 || (chain.Count == 1 && chain[0] != typeof(object)))
        {
            page.Append("**Inheritance:** ").Append(string.Join(" → ", chain.Select(Link).Append(Signatures.DisplayName(type)))).Append("  \n");
        }
    }

    Type[] derived = types.Where(t => t.BaseType is { } b && (b == type || (b.IsGenericType && b.GetGenericTypeDefinition() == type))).ToArray();
    if (derived.Length > 0)
    {
        page.Append("**Derived:** ").Append(string.Join(", ", derived.Select(Link))).Append("  \n");
    }

    Type[] interfaces = Signatures.DeclaredInterfaces(type).ToArray();
    if (interfaces.Length > 0)
    {
        page.Append("**Implements:** ").Append(string.Join(", ", interfaces.Select(Link))).Append("  \n");
    }

    page.Append('\n');
    if (summary.Length > 0)
    {
        page.Append(summary).Append("\n\n");
    }

    if (doc is not null)
    {
        AppendTypeParameters(page, doc);
        if (typeof(Delegate).IsAssignableFrom(type))
        {
            AppendParameters(page, doc, "####");
        }

        if (docs.Markdown(doc.Element("remarks")) is { Length: > 0 } remarks)
        {
            page.Append("## Remarks\n\n").Append(remarks).Append("\n\n");
        }
    }

    if (type.IsEnum)
    {
        AppendEnum(page, type);
    }
    else
    {
        AppendMembers(page, type);
    }

    Type[] nested = types.Where(t => t.DeclaringType == type).ToArray();
    if (nested.Length > 0)
    {
        page.Append("## Nested types\n\n| Type | Description |\n| --- | --- |\n");
        foreach (Type t in nested)
        {
            page.Append("| ").Append(Link(t)).Append(" | ").Append(XmlDocs.Cell(docs.Plain(docs.Get(t)?.Element("summary")))).Append(" |\n");
        }

        page.Append('\n');
    }

    return page.ToString().TrimEnd() + "\n";
}

string Link(Type type)
{
    Type definition = type.IsGenericType ? type.GetGenericTypeDefinition() : type;
    if (typesById.ContainsKey(DocIds.Of(definition)))
    {
        return $"[{Signatures.TypeName(type).Replace("<", "&lt;").Replace(">", "&gt;")}]({Slug(definition)}.md)";
    }

    string name = Signatures.TypeName(type).Replace("<", "&lt;").Replace(">", "&gt;");
    return definition.Namespace?.StartsWith("System", StringComparison.Ordinal) == true
        ? $"[{name}]({Learn(DocIds.TypeName(definition))})"
        : name;
}

void AppendEnum(StringBuilder page, Type type)
{
    page.Append("## Fields\n\n| Name | Value | Description |\n| --- | ---: | --- |\n");
    foreach (FieldInfo field in type.GetFields(BindingFlags.Public | BindingFlags.Static).OrderBy(f => Convert.ToInt64(f.GetRawConstantValue())))
    {
        if (Signatures.IsHidden(field))
        {
            continue;
        }

        // The id is the anchor that crefs to the value link to.
        page.Append("| <code id=\"").Append(field.Name.ToLowerInvariant()).Append("\">").Append(field.Name).Append("</code> | ").Append(field.GetRawConstantValue()).Append(" | ")
            .Append(XmlDocs.Cell(docs.Markdown(docs.Get(field)?.Element("summary")))).Append(" |\n");
    }

    page.Append('\n');
}

void AppendMembers(StringBuilder page, Type type)
{
    string? section = null;
    foreach (MemberGroup group in members[type])
    {
        if (group.Kind != section)
        {
            section = group.Kind;
            page.Append("## ").Append(section).Append("\n\n");
        }

        if (group.Kind != "Constructors")
        {
            page.Append("### ").Append(group.Name.Replace("<", "&lt;")).Append(" {#").Append(group.Anchor).Append("}\n\n");
        }

        bool overloads = group.Members.Count > 1;
        foreach (MemberInfo member in group.Members)
        {
            var doc = docs.Get(member);
            if (docs.Markdown(doc?.Element("summary")) is { Length: > 0 } summary)
            {
                page.Append(summary).Append("\n\n");
            }
            else
            {
                undocumented.Add(DocIds.Of(member));
            }

            page.Append("```csharp\n").Append(Signatures.Declaration(member)).Append("\n```\n\n");
            if (doc is null)
            {
                continue;
            }

            string level = overloads || group.Kind == "Constructors" ? "#####" : "####";
            AppendTypeParameters(page, doc, level);
            AppendParameters(page, doc, level);
            if (member is PropertyInfo && docs.Markdown(doc.Element("value")) is { Length: > 0 } value)
            {
                page.Append("**Value:** ").Append(value).Append("\n\n");
            }

            if (member is MethodInfo method && method.ReturnType != typeof(void) && docs.Markdown(doc.Element("returns")) is { Length: > 0 } returns)
            {
                page.Append("**Returns:** ").Append(returns).Append("\n\n");
            }

            var exceptions = doc.Elements("exception").ToList();
            if (exceptions.Count > 0)
            {
                page.Append("| Exception | Condition |\n| --- | --- |\n");
                foreach (var exception in exceptions)
                {
                    (string text, string? url) = Resolve((string?)exception.Attribute("cref") ?? "");
                    string name = url is null ? $"`{text}`" : $"[{text}]({url})";
                    page.Append("| ").Append(name).Append(" | ").Append(XmlDocs.Cell(docs.Markdown(exception))).Append(" |\n");
                }

                page.Append('\n');
            }

            if (docs.Markdown(doc.Element("remarks")) is { Length: > 0 } remarks)
            {
                page.Append(remarks).Append("\n\n");
            }
        }
    }
}

void AppendTypeParameters(StringBuilder page, System.Xml.Linq.XElement doc, string level = "##")
{
    var parameters = doc.Elements("typeparam").ToList();
    if (parameters.Count == 0)
    {
        return;
    }

    page.Append("**Type parameters**\n\n");
    foreach (var parameter in parameters)
    {
        page.Append("- `").Append((string?)parameter.Attribute("name")).Append("`: ").Append(XmlDocs.Cell(docs.Markdown(parameter))).Append('\n');
    }

    page.Append('\n');
}

void AppendParameters(StringBuilder page, System.Xml.Linq.XElement doc, string level)
{
    var parameters = doc.Elements("param").ToList();
    if (parameters.Count == 0)
    {
        return;
    }

    page.Append("**Parameters**\n\n");
    foreach (var parameter in parameters)
    {
        page.Append("- `").Append((string?)parameter.Attribute("name")).Append("`: ").Append(XmlDocs.Cell(docs.Markdown(parameter))).Append('\n');
    }

    page.Append('\n');
}

string Index()
{
    var page = new StringBuilder();
    page.Append("---\ntitle: API reference\ndescription: Every public type of the Styml package, by namespace, with its members and its documentation.\n---\n\n");
    page.Append("The public types of the `Styml` package, generated from its XML documentation. Most of the documentation comes from ")
        .Append("System.Text.Json, renamed for YAML; the [guide](../guide/getting-started.md) shows the types at work.\n\n");
    foreach (var ns in types.GroupBy(t => t.Namespace))
    {
        page.Append("## ").Append(ns.Key).Append(" {#").Append(NamespaceAnchor(ns.Key)).Append("}\n\n");
        page.Append("| Type | Kind | Description |\n| --- | --- | --- |\n");
        foreach (Type type in ns.Where(t => !t.IsNested || true))
        {
            page.Append("| ").Append(Link(type)).Append(" | ").Append(Signatures.Kind(type)).Append(" | ")
                .Append(XmlDocs.Cell(docs.Plain(docs.Get(type)?.Element("summary")))).Append(" |\n");
        }

        page.Append('\n');
    }

    return page.ToString().TrimEnd() + "\n";
}

string FolderData()
{
    var data = new StringBuilder();
    data.Append("# Written by eng/docs/ApiDocs: the layout of the API pages, and their navigation (api_nav), one group per namespace.\n");
    data.Append("layout: doc.html\nliquid: false\napi_nav:\n");
    data.Append("  - label: Overview\n    items:\n      - label: All namespaces\n        link: ").Append(BaseUrl).Append('\n');
    foreach (var ns in types.GroupBy(t => t.Namespace))
    {
        data.Append("  - label: ").Append(Quote(ns.Key ?? "")).Append("\n    collapsed: true\n    items:\n");
        foreach (Type type in ns)
        {
            data.Append("      - label: ").Append(Quote(Signatures.DisplayName(type))).Append("\n        link: ").Append(BaseUrl).Append(Slug(type)).Append("/\n");
        }
    }

    return data.ToString();
}

internal sealed record MemberGroup(string Kind, string Name, string Anchor, List<MemberInfo> Members);
