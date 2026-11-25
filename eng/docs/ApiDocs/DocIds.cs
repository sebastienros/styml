using System.Reflection;

namespace Styml.ApiDocs;

/// <summary>The documentation comment IDs of reflected members, as the compiler writes them in the XML file.</summary>
internal static class DocIds
{
    public static string Of(MemberInfo member) => member switch
    {
        Type type => "T:" + TypeName(type),
        ConstructorInfo ctor => "M:" + TypeName(ctor.DeclaringType!) + (ctor.IsStatic ? ".#cctor" : ".#ctor") + Parameters(ctor.GetParameters()),
        MethodInfo method => "M:" + TypeName(method.DeclaringType!) + "." + method.Name.Replace('.', '#')
            + (method.IsGenericMethodDefinition ? "``" + method.GetGenericArguments().Length : "")
            + Parameters(method.GetParameters())
            + (method.Name is "op_Implicit" or "op_Explicit" ? "~" + ParameterType(method.ReturnType) : ""),
        PropertyInfo property => "P:" + TypeName(property.DeclaringType!) + "." + property.Name.Replace('.', '#') + Parameters(property.GetIndexParameters()),
        FieldInfo field => "F:" + TypeName(field.DeclaringType!) + "." + field.Name,
        EventInfo @event => "E:" + TypeName(@event.DeclaringType!) + "." + @event.Name,
        _ => throw new NotSupportedException(member.GetType().Name),
    };

    /// <summary>The name of a type definition in an ID: its full name, with nested types separated by dots.</summary>
    public static string TypeName(Type type) => (type.IsGenericType ? type.GetGenericTypeDefinition() : type).FullName!.Replace('+', '.');

    private static string Parameters(ParameterInfo[] parameters) =>
        parameters.Length == 0 ? "" : "(" + string.Join(",", parameters.Select(p => ParameterType(p.ParameterType))) + ")";

    private static string ParameterType(Type type)
    {
        if (type.IsByRef)
        {
            return ParameterType(type.GetElementType()!) + "@";
        }

        if (type.IsPointer)
        {
            return ParameterType(type.GetElementType()!) + "*";
        }

        if (type.IsArray)
        {
            int rank = type.GetArrayRank();
            return ParameterType(type.GetElementType()!) + (rank == 1 ? "[]" : "[" + string.Join(",", Enumerable.Repeat("0:", rank)) + "]");
        }

        if (type.IsGenericParameter)
        {
            return (type.DeclaringMethod is null ? "`" : "``") + type.GenericParameterPosition;
        }

        if (type.IsGenericType)
        {
            string name = TypeName(type);
            // Remove the arity of every segment: Outer`1.Inner`1 becomes Outer.Inner.
            name = string.Join(".", name.Split('.').Select(segment => segment.Split('`')[0]));
            return name + "{" + string.Join(",", type.GetGenericArguments().Select(ParameterType)) + "}";
        }

        return type.FullName!.Replace('+', '.');
    }
}
