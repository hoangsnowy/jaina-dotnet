using System.Linq.Expressions;
using AutoMapper;
using Microsoft.EntityFrameworkCore;

namespace Jaina.Data.EfCore;

public class EfRepository<TContext, TEntity> : IRepository<TEntity>
    where TContext : DbContext
    where TEntity : class, IEntity
{
    protected TContext Context { get; }
    protected IMapper Mapper { get; }
    private readonly DbSet<TEntity> _dbSet;

    public EfRepository(TContext context, IMapper mapper)
    {
        Context = context;
        Mapper = mapper;
        _dbSet = context.Set<TEntity>();
    }

    protected async Task<TDto?> GetSingleAsync<TDto>(Expression<Func<TEntity, bool>> predicate)
    {
        var entity = await _dbSet.AsNoTracking().SingleOrDefaultAsync(predicate).ConfigureAwait(false);
        return Mapper.Map<TDto>(entity);
    }

    protected async Task<IEnumerable<TDto>> GetListAsync<TDto>()
    {
        var entities = await _dbSet.AsNoTracking().ToListAsync().ConfigureAwait(false);
        return Mapper.Map<IEnumerable<TDto>>(entities);
    }

    protected async Task<IEnumerable<TDto>> GetListAsync<TDto>(Expression<Func<TEntity, bool>> predicate)
    {
        var entities = await _dbSet.AsNoTracking().Where(predicate).ToListAsync().ConfigureAwait(false);
        return Mapper.Map<IEnumerable<TDto>>(entities);
    }

    protected async Task<IEnumerable<TDto>> GetListAsync<TDto>(Expression<Func<TEntity, bool>> predicate, Expression<Func<TEntity, TDto>> selector)
    {
        return await _dbSet.AsNoTracking().Where(predicate).Select(selector).ToListAsync().ConfigureAwait(false);
    }

    public virtual async Task<TEntity?> GetEntityAsync(params object[] keyValues) =>
        await _dbSet.FindAsync(keyValues).ConfigureAwait(false);

    public virtual Task CreateAsync(TEntity entity)
    {
        _dbSet.Add(entity);
        return Task.CompletedTask;
    }

    public virtual Task UpdateAsync(TEntity entity, params object[] keyValues)
    {
        var existing = _dbSet.Find(keyValues)
            ?? throw new RepositoryException($"Entity '{typeof(TEntity).Name}' not found with keys: '{string.Join(",", keyValues)}'");
        Context.Entry(existing).CurrentValues.SetValues(entity);
        return Task.CompletedTask;
    }

    public virtual Task UpdateOnlyPropertiesAsync(TEntity entity, Expression<Func<TEntity, object>>[] properties, params object[] keyValues)
    {
        var existing = _dbSet.Find(keyValues)
            ?? throw new RepositoryException($"Entity '{typeof(TEntity).Name}' not found with keys: '{string.Join(",", keyValues)}'");

        var entry = Context.Entry(existing);
        var propInfos = entity.GetType().GetProperties();
        foreach (var prop in properties)
        {
            var propertyEntry = entry.Property(prop);
            propertyEntry.CurrentValue = propInfos.First(x => x.Name == propertyEntry.Metadata.Name).GetValue(entity)!;
        }
        return Task.CompletedTask;
    }

    public virtual Task UpdateWithoutPropertiesAsync(TEntity entity, Expression<Func<TEntity, object>>[] excludedProperties, params object[] keyValues)
    {
        var existing = _dbSet.Find(keyValues)
            ?? throw new RepositoryException($"Entity '{typeof(TEntity).Name}' not found with keys: '{string.Join(",", keyValues)}'");

        var entry = Context.Entry(existing);
        entry.CurrentValues.SetValues(entity);
        foreach (var prop in excludedProperties)
            entry.Property(prop).IsModified = false;
        return Task.CompletedTask;
    }

    public virtual Task CreateOrUpdateAsync(TEntity entity, params object[] keyValues)
    {
        var existing = _dbSet.Find(keyValues);
        if (existing is null) return CreateAsync(entity);

        var entry = Context.Entry(existing);
        entry.CurrentValues.SetValues(entity);
        entry.State = EntityState.Modified;
        return Task.CompletedTask;
    }

    public virtual Task DeleteAsync(params object[] keyValues)
    {
        var entity = _dbSet.Find(keyValues);
        if (entity is not null) _dbSet.Remove(entity);
        return Task.CompletedTask;
    }
}
