using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Sh.Autofit.New.Interfaces.UnitOfWork;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sh.Autofit.New.Dal.UnitOfWork
{
    public class EfUnitOfWork<TContext> : IUnitOfWork where TContext : DbContext
    {
        private readonly TContext _db;
        private IDbContextTransaction? _currentTx;

        public EfUnitOfWork(TContext db) => _db = db;

        public Task<int> SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);

        public async Task<IDisposable> BeginTransactionAsync(CancellationToken ct = default)
        {
            if (_currentTx is not null) return _currentTx;
            _currentTx = await _db.Database.BeginTransactionAsync(ct);
            return new TxScope(_currentTx, () => _currentTx = null);
        }

        public async ValueTask DisposeAsync()
        {
            if (_currentTx is not null) await _currentTx.DisposeAsync();
            await _db.DisposeAsync();
        }

        private sealed class TxScope : IDisposable
        {
            private readonly IDbContextTransaction _tx;
            private readonly Action _onDispose;
            private bool _completed;

            public TxScope(IDbContextTransaction tx, Action onDispose)
            {
                _tx = tx; _onDispose = onDispose;
            }

            public void Dispose()
            {
                if (!_completed)
                {
                    // If user didn’t commit, roll back on dispose.
                    _tx.Rollback();
                }
                _tx.Dispose();
                _onDispose();
            }

            public void Commit()
            {
                _tx.Commit();
                _completed = true;
            }
        }
    }
}
