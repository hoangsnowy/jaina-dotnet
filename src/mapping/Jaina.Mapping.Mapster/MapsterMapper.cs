using Jaina.Mapping;
using Mapster;

namespace Jaina.Mapping.Mapster;

internal sealed class MapsterMapper : IMapper
{
    private readonly TypeAdapterConfig _config;

    public MapsterMapper(TypeAdapterConfig config) => _config = config;

    public TDestination Map<TDestination>(object source)
        => source.Adapt<TDestination>(_config);

    public TDestination Map<TSource, TDestination>(TSource source)
        => source.Adapt<TDestination>(_config);
}
