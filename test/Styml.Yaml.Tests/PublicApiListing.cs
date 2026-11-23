// Copyright (c) Sébastien Ros. Licensed under the MIT license.

using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Versioning;

// Shared with eng/test/public-api, which lists the M2b baseline; keep it free of test-framework dependencies.
namespace System.Text.Yaml.Tests
{
    /// <summary>One line per public or protected type and member, with full type names.</summary>
    internal static class PublicApiListing
    {
        internal static string TargetMoniker(Assembly assembly)
        {
            string name = assembly.GetCustomAttribute<TargetFrameworkAttribute>()?.FrameworkName ?? "";
            // ".NETCoreApp,Version=v11.0" -> "net11.0"; ".NETStandard,Version=v2.0" -> "netstandard2.0".
            string version = name.Substring(name.IndexOf("=v", StringComparison.Ordinal) + 2);
            return name.StartsWith(".NETStandard", StringComparison.Ordinal) ? "netstandard" + version : "net" + version;
        }

        internal static IEnumerable<string> List(Assembly assembly)
        {
            const BindingFlags Declared = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
            var lines = new List<string>();
            foreach (Type type in assembly.GetTypes().Where(IsVisible))
            {
                string t = Name(type);
                string kind = type.IsEnum ? "enum" : type.IsInterface ? "interface" : type.IsValueType ? "struct" : typeof(Delegate).IsAssignableFrom(type) ? "delegate" : "class";
                string modifiers = type.IsAbstract && type.IsSealed ? "static " : type.IsAbstract && !type.IsInterface ? "abstract " : type.IsSealed && !type.IsValueType && !type.IsEnum ? "sealed " : "";
                string bases = string.Join(", ", new[] { type.BaseType }.Where(b => b is not null && b != typeof(object) && b != typeof(ValueType) && b != typeof(Enum)).Concat(OwnInterfaces(type)).Select(b => Name(b!)));
                lines.Add($"{modifiers}{kind} {t}{(bases.Length > 0 ? " : " + bases : "")}");

                foreach (MemberInfo m in type.GetMembers(Declared))
                {
                    string? line = m switch
                    {
                        FieldInfo f when f.IsPublic || f.IsFamily || f.IsFamilyOrAssembly =>
                            type.IsEnum && f.IsSpecialName ? null : $"{t}.{f.Name} field {(f.IsLiteral ? "const " : f.IsStatic ? "static " : "")}{(f.IsInitOnly ? "readonly " : "")}{Name(f.FieldType)}{(type.IsEnum ? " = " + Convert.ToInt64(f.GetRawConstantValue()) : "")}",
                        ConstructorInfo c when IsVisible(c) => $"{t}.ctor({Parameters(c)})",
                        MethodInfo mi when IsVisible(mi) && !mi.IsSpecialName => $"{t}.{mi.Name}{Generic(mi)}({Parameters(mi)}) {(mi.IsStatic ? "static " : mi.IsAbstract ? "abstract " : mi.IsVirtual && !mi.IsFinal ? "virtual " : "")}{Name(mi.ReturnType)}",
                        PropertyInfo p => Accessors(p) is { Length: > 0 } a ? $"{t}.{p.Name}{(p.GetIndexParameters().Length > 0 ? "[" + string.Join(", ", p.GetIndexParameters().Select(ip => Name(ip.ParameterType))) + "]" : "")} property {Name(p.PropertyType)} {{ {a} }}" : null,
                        EventInfo e when e.AddMethod is { } add && IsVisible(add) => $"{t}.{e.Name} event {Name(e.EventHandlerType!)}",
                        _ => null,
                    };
                    if (line is not null)
                    {
                        lines.Add(line);
                    }
                }
            }
            lines.Sort(StringComparer.Ordinal);
            return lines;
        }

        // Interfaces the type adds over its base type: those of System.Enum or Exception depend on the runtime.
        private static IEnumerable<Type> OwnInterfaces(Type type) =>
            type.GetInterfaces().Except(type.BaseType?.GetInterfaces() ?? Type.EmptyTypes).OrderBy(Name, StringComparer.Ordinal);

        private static bool IsVisible(Type type) =>
            type.IsPublic || (type.IsNestedPublic || type.IsNestedFamily || type.IsNestedFamORAssem) && IsVisible(type.DeclaringType!);

        private static bool IsVisible(MethodBase m) => m.IsPublic || m.IsFamily || m.IsFamilyOrAssembly;

        private static string Accessors(PropertyInfo p) => string.Join(" ",
            new[] { (p.GetMethod, "get;"), (p.SetMethod, IsInitOnly(p.SetMethod) ? "init;" : "set;") }
                .Where(x => x.Item1 is not null && IsVisible(x.Item1)).Select(x => x.Item2));

        private static bool IsInitOnly(MethodInfo? setter) =>
            setter is not null && setter.ReturnParameter.GetRequiredCustomModifiers().Any(t => t.FullName == "System.Runtime.CompilerServices.IsExternalInit");

        private static string Generic(MethodInfo m) => m.IsGenericMethodDefinition ? "<" + string.Join(", ", m.GetGenericArguments().Select(a => a.Name)) + ">" : "";

        private static string Parameters(MethodBase m) => string.Join(", ", m.GetParameters().Select(p =>
            (p.IsOut ? "out " : p.ParameterType.IsByRef ? (p.IsIn ? "in " : "ref ") : "") + Name(p.ParameterType.IsByRef ? p.ParameterType.GetElementType()! : p.ParameterType)));

        internal static string Name(Type type)
        {
            if (type.IsGenericParameter)
            {
                return type.Name;
            }
            if (type.IsByRef)
            {
                return Name(type.GetElementType()!) + "&";
            }
            if (type.IsArray)
            {
                return Name(type.GetElementType()!) + "[" + new string(',', type.GetArrayRank() - 1) + "]";
            }
            if (type.IsPointer)
            {
                return Name(type.GetElementType()!) + "*";
            }

            string name = type.IsNested ? Name(type.DeclaringType!) + "+" + type.Name : (type.Namespace is null ? "" : type.Namespace + ".") + type.Name;
            if (type.IsGenericType)
            {
                Type[] args = type.GetGenericArguments();
                // Arguments that belong to the declaring type are already shown on it.
                int inherited = type.IsNested ? type.DeclaringType!.GetGenericArguments().Length : 0;
                if (args.Length > inherited)
                {
                    name += "[" + string.Join(", ", args.Skip(inherited).Select(Name)) + "]";
                }
            }
            return name;
        }
    }
}
