using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Styml.ApiDocs;

/// <summary>C# declarations of reflected types and members, with their nullable annotations.</summary>
internal static class Signatures
{
    private static readonly NullabilityInfoContext Nullability = new();

    private static readonly Dictionary<Type, string> Keywords = new()
    {
        [typeof(void)] = "void", [typeof(object)] = "object", [typeof(string)] = "string", [typeof(bool)] = "bool",
        [typeof(char)] = "char", [typeof(byte)] = "byte", [typeof(sbyte)] = "sbyte", [typeof(short)] = "short",
        [typeof(ushort)] = "ushort", [typeof(int)] = "int", [typeof(uint)] = "uint", [typeof(long)] = "long",
        [typeof(ulong)] = "ulong", [typeof(float)] = "float", [typeof(double)] = "double", [typeof(decimal)] = "decimal",
        [typeof(nint)] = "nint", [typeof(nuint)] = "nuint",
    };

    private static readonly Dictionary<string, string> Operators = new()
    {
        ["op_Equality"] = "==", ["op_Inequality"] = "!=", ["op_LessThan"] = "<", ["op_GreaterThan"] = ">",
        ["op_LessThanOrEqual"] = "<=", ["op_GreaterThanOrEqual"] = ">=", ["op_Addition"] = "+", ["op_Subtraction"] = "-",
        ["op_Multiply"] = "*", ["op_Division"] = "/", ["op_Modulus"] = "%", ["op_UnaryNegation"] = "-",
        ["op_UnaryPlus"] = "+", ["op_LogicalNot"] = "!", ["op_BitwiseAnd"] = "&", ["op_BitwiseOr"] = "|",
        ["op_ExclusiveOr"] = "^", ["op_OnesComplement"] = "~", ["op_True"] = "true", ["op_False"] = "false",
    };

    /// <summary>The name of a type as C# code shows it: keywords, generic arguments, nested types after their parent.</summary>
    public static string TypeName(Type type, NullabilityInfo? nullability = null)
    {
        if (type.IsByRef)
        {
            return TypeName(type.GetElementType()!, nullability);
        }

        if (Nullable.GetUnderlyingType(type) is { } underlying)
        {
            return TypeName(underlying, nullability?.GenericTypeArguments.FirstOrDefault()) + "?";
        }

        string suffix = !type.IsValueType && nullability is { ReadState: NullabilityState.Nullable } ? "?" : "";
        if (type.IsArray)
        {
            int rank = type.GetArrayRank();
            return TypeName(type.GetElementType()!, nullability?.ElementType) + "[" + new string(',', rank - 1) + "]" + suffix;
        }

        if (type.IsPointer)
        {
            return TypeName(type.GetElementType()!) + "*";
        }

        if (Keywords.TryGetValue(type, out string? keyword))
        {
            return keyword + suffix;
        }

        if (type.IsGenericParameter)
        {
            return type.Name + suffix;
        }

        string name = ShortName(type);
        if (type.IsGenericType)
        {
            Type[] arguments = type.GetGenericArguments();
            // The arguments of the declaring types are written with them: only this type's own arguments go here.
            int inherited = type.DeclaringType?.IsGenericType == true ? type.DeclaringType.GetGenericArguments().Length : 0;
            Type[] own = arguments[inherited..];
            if (own.Length > 0)
            {
                NullabilityInfo[] infos = nullability?.GenericTypeArguments ?? [];
                name += "<" + string.Join(", ", own.Select((a, i) => TypeName(a, i + inherited < infos.Length ? infos[i + inherited] : null))) + ">";
            }
        }

        return (type.IsNested && !type.IsGenericParameter ? TypeName(type.DeclaringType!) + "." : "") + name + suffix;
    }

    /// <summary>The name of a type without its arity (<c>YamlConverter</c> for <c>YamlConverter`1</c>).</summary>
    public static string ShortName(Type type) => type.Name.Split('`')[0];

    /// <summary>The name of a type definition with its type parameters (<c>YamlConverter&lt;T&gt;</c>).</summary>
    public static string DisplayName(Type type)
    {
        string name = ShortName(type);
        if (type.IsGenericTypeDefinition)
        {
            int inherited = type.DeclaringType?.IsGenericType == true ? type.DeclaringType.GetGenericArguments().Length : 0;
            Type[] own = type.GetGenericArguments()[inherited..];
            if (own.Length > 0)
            {
                name += "<" + string.Join(", ", own.Select(a => a.Name)) + ">";
            }
        }

        return type.IsNested ? DisplayName(type.DeclaringType!) + "." + name : name;
    }

    public static string Kind(Type type) =>
        type.IsEnum ? "enum"
        : type.IsInterface ? "interface"
        : typeof(Delegate).IsAssignableFrom(type) ? "delegate"
        : type.IsValueType ? "struct"
        : "class";

    /// <summary>The declaration of a type.</summary>
    public static string Declaration(Type type)
    {
        var lines = new List<string>();
        if (type.GetCustomAttribute<ObsoleteAttribute>() is { } obsolete)
        {
            lines.Add(obsolete.Message is null ? "[Obsolete]" : $"[Obsolete(\"{obsolete.Message}\")]");
        }

        if (type.IsEnum && type.GetCustomAttribute<FlagsAttribute>() is not null)
        {
            lines.Add("[Flags]");
        }

        string visibility = type.IsPublic || type.IsNestedPublic ? "public" : "protected";
        if (typeof(Delegate).IsAssignableFrom(type) && type != typeof(Delegate) && type.GetMethod("Invoke") is { } invoke)
        {
            lines.Add($"{visibility} delegate {TypeName(invoke.ReturnType, Nullability.Create(invoke.ReturnParameter))} {DisplayName(type).Split('.')[^1]}({Parameters(invoke)});");
            return string.Join("\n", lines);
        }

        string modifiers = Kind(type) switch
        {
            "class" when type.IsAbstract && type.IsSealed => " static",
            "class" when type.IsAbstract => " abstract",
            "class" when type.IsSealed => " sealed",
            "struct" => (type.GetCustomAttribute<IsReadOnlyAttribute>() is not null ? " readonly" : "") + (type.IsByRefLike ? " ref" : ""),
            _ => "",
        };

        var bases = new List<string>();
        if (type.IsEnum)
        {
            Type underlying = Enum.GetUnderlyingType(type);
            if (underlying != typeof(int))
            {
                bases.Add(TypeName(underlying));
            }
        }
        else
        {
            if (type.BaseType is { } baseType && baseType != typeof(object) && baseType != typeof(ValueType))
            {
                bases.Add(TypeName(baseType));
            }

            bases.AddRange(DeclaredInterfaces(type).Select(i => TypeName(i)));
        }

        string name = DisplayName(type).Split('.')[^1];
        lines.Add($"{visibility}{modifiers} {Kind(type)} {name}{(bases.Count > 0 ? " : " + string.Join(", ", bases) : "")}");
        lines.AddRange(Constraints(type.IsGenericTypeDefinition ? type.GetGenericArguments() : []));
        return string.Join("\n", lines);
    }

    /// <summary>The public interfaces a type implements that its base type and its other interfaces don't bring.</summary>
    public static IEnumerable<Type> DeclaredInterfaces(Type type)
    {
        Type[] all = type.GetInterfaces().Where(i => i.IsPublic || i.IsNestedPublic).ToArray();
        var inherited = new HashSet<Type>(type.BaseType?.GetInterfaces() ?? []);
        foreach (Type i in all)
        {
            inherited.UnionWith(i.GetInterfaces());
        }

        return all.Where(i => !inherited.Contains(i)).OrderBy(i => i.Name, StringComparer.Ordinal);
    }

    /// <summary>The declaration of a member.</summary>
    public static string Declaration(MemberInfo member)
    {
        string obsolete = member.GetCustomAttribute<ObsoleteAttribute>() is { } o
            ? (o.Message is null ? "[Obsolete]\n" : $"[Obsolete(\"{o.Message}\")]\n")
            : "";
        return obsolete + member switch
        {
            ConstructorInfo ctor => $"{Visibility(ctor)} {ShortName(ctor.DeclaringType!)}({Parameters(ctor)});",
            MethodInfo method => Method(method),
            PropertyInfo property => Property(property),
            FieldInfo field => Field(field),
            EventInfo @event => $"{Visibility(@event.AddMethod!)}{(@event.AddMethod!.IsStatic ? " static" : "")} event {TypeName(@event.EventHandlerType!, Nullability.Create(@event))} {@event.Name};",
            _ => throw new NotSupportedException(member.GetType().Name),
        };
    }

    private static string Method(MethodInfo method)
    {
        string returns = TypeName(method.ReturnType, Nullability.Create(method.ReturnParameter));
        if (method.ReturnType.IsByRef)
        {
            returns = (method.ReturnParameter.GetCustomAttribute<IsReadOnlyAttribute>() is not null ? "ref readonly " : "ref ") + returns;
        }

        if (method.Name is "op_Implicit" or "op_Explicit")
        {
            return $"public static {(method.Name == "op_Implicit" ? "implicit" : "explicit")} operator {returns}({Parameters(method)});";
        }

        if (Operators.TryGetValue(method.Name, out string? op))
        {
            return $"public static {returns} operator {op}({Parameters(method)});";
        }

        string generics = method.IsGenericMethodDefinition ? "<" + string.Join(", ", method.GetGenericArguments().Select(a => a.Name)) + ">" : "";
        string constraints = string.Concat(Constraints(method.IsGenericMethodDefinition ? method.GetGenericArguments() : []).Select(c => "\n" + c));
        return $"{Visibility(method)}{Modifiers(method)} {returns} {method.Name}{generics}({Parameters(method)}){constraints};";
    }

    private static string Property(PropertyInfo property)
    {
        MethodInfo accessor = (property.GetMethod is { } g && IsVisible(g) ? g : property.SetMethod)!;
        string name = property.GetIndexParameters() is { Length: > 0 } index
            ? "this[" + string.Join(", ", index.Select(Parameter)) + "]"
            : property.Name;
        var accessors = new List<string>();
        if (property.GetMethod is { } getter && IsVisible(getter))
        {
            accessors.Add((IsVisible(accessor) && Visibility(getter) != Visibility(accessor) ? Visibility(getter) + " " : "") + "get;");
        }

        if (property.SetMethod is { } setter && IsVisible(setter))
        {
            bool init = setter.ReturnParameter.GetRequiredCustomModifiers().Contains(typeof(IsExternalInit));
            accessors.Add((Visibility(setter) != Visibility(accessor) ? Visibility(setter) + " " : "") + (init ? "init;" : "set;"));
        }

        string type = TypeName(property.PropertyType, Nullability.Create(property));
        if (property.PropertyType.IsByRef)
        {
            type = "ref " + type;
        }

        return $"{Visibility(accessor)}{Modifiers(accessor)} {type} {name} {{ {string.Join(" ", accessors)} }}";
    }

    private static string Field(FieldInfo field)
    {
        string type = TypeName(field.FieldType, Nullability.Create(field));
        if (field.IsLiteral)
        {
            return $"{Visibility(field)} const {type} {field.Name} = {Literal(field.GetRawConstantValue(), field.FieldType)};";
        }

        return $"{Visibility(field)}{(field.IsStatic ? " static" : "")}{(field.IsInitOnly ? " readonly" : "")} {type} {field.Name};";
    }

    private static string Parameters(MethodBase method) => string.Join(", ", method.GetParameters().Select((p, i) =>
        (i == 0 && method.IsDefined(typeof(ExtensionAttribute)) ? "this " : "") + Parameter(p)));

    private static string Parameter(ParameterInfo parameter)
    {
        string modifier = "";
        if (parameter.ParameterType.IsByRef)
        {
            modifier = parameter.IsOut ? "out " : parameter.IsIn || parameter.IsDefined(typeof(IsReadOnlyAttribute)) ? "in " : "ref ";
        }
        else if (parameter.IsDefined(typeof(ParamArrayAttribute)))
        {
            modifier = "params ";
        }

        string text = modifier + TypeName(parameter.ParameterType, Nullability.Create(parameter)) + " " + parameter.Name;
        if (parameter.HasDefaultValue)
        {
            text += " = " + Literal(parameter.RawDefaultValue, parameter.ParameterType);
        }

        return text;
    }

    private static string Literal(object? value, Type type)
    {
        Type target = Nullable.GetUnderlyingType(type) ?? type;
        return value switch
        {
            null or DBNull when target.IsValueType && Nullable.GetUnderlyingType(type) is null => "default",
            null or DBNull => "null",
            Missing => "default",
            string s => "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r") + "\"",
            char c => "'" + c + "'",
            bool b => b ? "true" : "false",
            _ when target.IsEnum => EnumLiteral(target, value),
            IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? "",
        };
    }

    private static string EnumLiteral(Type type, object value)
    {
        object boxed = Enum.ToObject(type, value);
        string? name = Enum.GetName(type, boxed);
        return name is not null ? ShortName(type) + "." + name : "(" + ShortName(type) + ")" + Convert.ToInt64(value, CultureInfo.InvariantCulture);
    }

    private static IEnumerable<string> Constraints(Type[] parameters)
    {
        foreach (Type parameter in parameters)
        {
            var parts = new List<string>();
            GenericParameterAttributes attributes = parameter.GenericParameterAttributes;
            if ((attributes & GenericParameterAttributes.NotNullableValueTypeConstraint) != 0)
            {
                parts.Add("struct");
            }
            else if ((attributes & GenericParameterAttributes.ReferenceTypeConstraint) != 0)
            {
                parts.Add("class");
            }

            parts.AddRange(parameter.GetGenericParameterConstraints().Where(c => c != typeof(ValueType)).Select(c => TypeName(c)));
            if ((attributes & GenericParameterAttributes.DefaultConstructorConstraint) != 0 && (attributes & GenericParameterAttributes.NotNullableValueTypeConstraint) == 0)
            {
                parts.Add("new()");
            }

            if (parts.Count > 0)
            {
                yield return $"    where {parameter.Name} : {string.Join(", ", parts)}";
            }
        }
    }

    private static string Modifiers(MethodInfo method)
    {
        if (method.IsStatic)
        {
            return method.IsAbstract ? " static abstract" : " static";
        }

        if (method.DeclaringType!.IsInterface)
        {
            return "";
        }

        bool isOverride = method.GetBaseDefinition() != method;
        if (method.IsAbstract)
        {
            return isOverride ? " abstract override" : " abstract";
        }

        if (isOverride)
        {
            return method.IsFinal ? " sealed override" : " override";
        }

        return method.IsVirtual && !method.IsFinal ? " virtual" : "";
    }

    public static bool IsVisible(MethodBase method) => method.IsPublic || method.IsFamily || method.IsFamilyOrAssembly;

    public static bool IsVisible(FieldInfo field) => field.IsPublic || field.IsFamily || field.IsFamilyOrAssembly;

    public static bool IsHidden(MemberInfo member) =>
        member.GetCustomAttribute<EditorBrowsableAttribute>()?.State == EditorBrowsableState.Never;

    private static string Visibility(MethodBase method) => method.IsPublic ? "public" : method.IsFamilyOrAssembly ? "protected internal" : "protected";

    private static string Visibility(FieldInfo field) => field.IsPublic ? "public" : "protected";
}
