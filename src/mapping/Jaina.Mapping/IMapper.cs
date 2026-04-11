namespace Jaina.Mapping;

/// <summary>Abstracts object-to-object mapping.</summary>
public interface IMapper
{
    /// <summary>Maps <paramref name="source"/> to an instance of <typeparamref name="TDestination"/>.</summary>
    TDestination Map<TDestination>(object source);

    /// <summary>Maps <paramref name="source"/> to an instance of <typeparamref name="TDestination"/>.</summary>
    TDestination Map<TSource, TDestination>(TSource source);
}
