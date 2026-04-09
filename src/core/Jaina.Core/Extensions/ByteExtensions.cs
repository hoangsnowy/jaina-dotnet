using System.Text;
using System.Text.Json;

namespace Jaina.Core.Extensions;

public static class ByteExtensions
{
    public static string ToBase64(this byte[] bytes) =>
        Convert.ToBase64String(bytes);

    public static string GetString(this byte[] bytes) =>
        Encoding.UTF8.GetString(bytes);

    public static string GetString(this byte[] bytes, Encoding encoding) =>
        encoding.GetString(bytes);

    public static T? Deserialize<T>(this byte[] bytes, JsonSerializerOptions? options = null) =>
        JsonSerializer.Deserialize<T>(bytes, options ?? JsonSerializerDefaults.Options);

    public static byte[] Serialize<T>(T value, JsonSerializerOptions? options = null) =>
        JsonSerializer.SerializeToUtf8Bytes(value, options ?? JsonSerializerDefaults.Options);
}
