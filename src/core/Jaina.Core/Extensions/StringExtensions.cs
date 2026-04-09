using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Jaina.Core.Extensions;

public static class StringExtensions
{
    public static byte[] GetBytes(this string value) =>
        Encoding.UTF8.GetBytes(value);

    public static byte[] GetBytes(this string value, Encoding encoding) =>
        encoding.GetBytes(value);

    public static T? FromJson<T>(this string json, JsonSerializerOptions? options = null) =>
        JsonSerializer.Deserialize<T>(json, options ?? JsonSerializerDefaults.Options);

    public static string ToBase64(this string value) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(value));

    public static string FromBase64(this string value) =>
        Encoding.UTF8.GetString(Convert.FromBase64String(value));

    public static bool IsNullOrEmpty(this string? value) =>
        string.IsNullOrEmpty(value);

    public static bool IsNullOrWhiteSpace(this string? value) =>
        string.IsNullOrWhiteSpace(value);

    public static string NormalizeUmlaut(this string value)
    {
        if (string.IsNullOrEmpty(value)) return value;

        var normalized = value.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);

        foreach (var c in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(c);
            if (category != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }

        return sb.ToString().Normalize(NormalizationForm.FormC);
    }
}
