using System.Linq.Expressions;

namespace Jaina.Data;

public interface IRepository { }

public interface IRepository<TEntity> where TEntity : IEntity
{
    Task<TEntity?> GetEntityAsync(params object[] keyValues);
    Task CreateAsync(TEntity entity);
    Task UpdateAsync(TEntity entity, params object[] keyValues);
    Task UpdateOnlyPropertiesAsync(TEntity entity, Expression<Func<TEntity, object>>[] properties, params object[] keyValues);
    Task UpdateWithoutPropertiesAsync(TEntity entity, Expression<Func<TEntity, object>>[] excludedProperties, params object[] keyValues);
    Task CreateOrUpdateAsync(TEntity entity, params object[] keyValues);
    Task DeleteAsync(params object[] keyValues);
}
