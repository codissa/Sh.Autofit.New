// Sh.Autofit.New.Dal.Repos/EfRepository.cs  (SINGLE-GENERIC)
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Sh.Autofit.New.Entities.Models;
using Sh.Autofit.New.Interfaces.Repos;
using System;
using System.Linq.Expressions;

namespace Sh.Autofit.New.Dal.Repos
{
    public class EfRepository<TEntity> : IRepository<TEntity>
        where TEntity : class
    {
        protected readonly ShAutofitContext _db;
        protected readonly DbSet<TEntity> _set;

        public EfRepository(ShAutofitContext db)
        {
            _db = db;
            _set = _db.Set<TEntity>();
        }

        public virtual async Task<TEntity?> GetByIdAsync(CancellationToken ct = default, params object[] keyValues)
            => await _set.FindAsync(keyValues, ct);

        public virtual async Task<TEntity?> FirstOrDefaultAsync(
            Expression<Func<TEntity, bool>> predicate,
            Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object>>? include = null,
            bool asNoTracking = true,
            CancellationToken ct = default)
        {
            IQueryable<TEntity> q = _set;
            if (asNoTracking) q = q.AsNoTracking();
            if (include is not null) q = include(q);
            return await q.FirstOrDefaultAsync(predicate, ct);
        }

        public virtual async Task<List<TEntity>> ListAsync(
            Expression<Func<TEntity, bool>>? predicate = null,
            Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
            Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object>>? include = null,
            bool asNoTracking = true,
            int? skip = null,
            int? take = null,
            CancellationToken ct = default)
        {
            IQueryable<TEntity> q = _set;
            if (asNoTracking) q = q.AsNoTracking();
            if (include is not null) q = include(q);
            if (predicate is not null) q = q.Where(predicate);
            if (orderBy is not null) q = orderBy(q);
            if (skip.HasValue) q = q.Skip(skip.Value);
            if (take.HasValue) q = q.Take(take.Value);
            return await q.ToListAsync(ct);
        }

        public Task<bool> AnyAsync(Expression<Func<TEntity, bool>>? predicate = null, CancellationToken ct = default)
            => predicate is null ? _set.AnyAsync(ct) : _set.AnyAsync(predicate, ct);

        public Task<int> CountAsync(Expression<Func<TEntity, bool>>? predicate = null, CancellationToken ct = default)
            => predicate is null ? _set.CountAsync(ct) : _set.CountAsync(predicate, ct);

        public Task AddAsync(TEntity entity, CancellationToken ct = default) => _set.AddAsync(entity, ct).AsTask();
        public Task AddRangeAsync(IEnumerable<TEntity> entities, CancellationToken ct = default) => _set.AddRangeAsync(entities, ct);
        public void Update(TEntity entity) => _set.Update(entity);
        public void UpdateRange(IEnumerable<TEntity> entities) => _set.UpdateRange(entities);
        public void Remove(TEntity entity) => _set.Remove(entity);
        public void RemoveRange(IEnumerable<TEntity> entities) => _set.RemoveRange(entities);
    }
}
