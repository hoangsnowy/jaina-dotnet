using System.Globalization;
using System.Text.Json;

namespace Jaina.Core.Extensions;

public static class ObjectExtensions
{
    public static int GetInt(this object? value, int defaultValue = 0) =>
        value is null ? defaultValue : int.TryParse(value.ToString(), out var result) ? result : defaultValue;

    public static long GetLong(this object? value, long defaultValue = 0) =>
        value is null ? defaultValue : long.TryParse(value.ToString(), out var result) ? result : defaultValue;

    public static decimal GetDecimal(this object? value, decimal defaultValue = 0) =>
        value is null ? defaultValue : decimal.TryParse(value.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var result) ? result : defaultValue;

    public static double GetDouble(this object? value, double defaultValue = 0) =>
        value is null ? defaultValue : double.TryParse(value.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var result) ? result : defaultValue;

    public static bool GetBool(this object? value, bool defaultValue = false) =>
        value is null ? defaultValue : bool.TryParse(value.ToString(), out var result) ? result : defaultValue;

    public static string GetString(this object? value, string defaultValue = "") =>
        value?.ToString() ?? defaultValue;

    public static DateTime GetDateTime(this object? value) =>
        value is null ? default : DateTime.TryParse(value.ToString(), out var result) ? result : default;

    public static string ToJson<T>(this T value, JsonSerializerOptions? options = null) =>
        JsonSerializer.Serialize(value, options ?? JsonSerializerDefaults.Options);
}
