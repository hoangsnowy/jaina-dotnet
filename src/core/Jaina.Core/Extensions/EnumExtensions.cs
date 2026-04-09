namespace Jaina.Core.Extensions;

public static class EnumExtensions
{
    public static T Parse<T>(string value) where T : struct, Enum =>
#if NET6_0_OR_GREATER
        Enum.Parse<T>(value, ignoreCase: true);
#else
        (T)Enum.Parse(typeof(T), value, ignoreCase: true);
#endif

    public static bool TryParse<T>(string value, out T result) where T : struct, Enum =>
        Enum.TryParse(value, ignoreCase: true, out result);

    public static string GetName<T>(this T value) where T : struct, Enum =>
#if NET6_0_OR_GREATER
        Enum.GetName(value) ?? value.ToString();
#else
        Enum.GetName(typeof(T), value) ?? value.ToString();
#endif
}
