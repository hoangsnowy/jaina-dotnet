using System.Collections;

namespace Jaina.Core.Extensions;

public static class TypeExtensions
{
    public static Type? GetAnyElementType(this Type type)
    {
        if (type.IsArray)
            return type.GetElementType();

        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>))
            return type.GetGenericArguments()[0];

        var enumType = type.GetInterfaces()
            .Where(t => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IEnumerable<>))
            .Select(t => t.GetGenericArguments()[0])
            .FirstOrDefault();

        if (enumType is not null)
            return enumType;

        if (typeof(IEnumerable).IsAssignableFrom(type))
            return typeof(object);

        return null;
    }
}
