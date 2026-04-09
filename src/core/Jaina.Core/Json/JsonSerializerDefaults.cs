using System.Text.Json;
using System.Text.Json.Serialization;

namespace Jaina.Core;

public static class JsonSerializerDefaults
{
    private static JsonSerializerOptions? _options;

    public static JsonSerializerOptions Options => _options ??= new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };
}
