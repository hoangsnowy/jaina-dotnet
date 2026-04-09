using Microsoft.EntityFrameworkCore;

namespace Jaina.Data;

public class EfUnitOfWork<TContext> : IUnitOfWork where TContext : DbContext
{
    private readonly TContext _context;

    public EfUnitOfWork(TContext context) => _context = context;

    public int SaveChanges() => _context.SaveChanges();

    public async Task<int> SaveChangesAsync(CancellationToken ct = default) =>
        await _context.SaveChangesAsync(ct).ConfigureAwait(false);

    public void Dispose() => GC.SuppressFinalize(this);
}
