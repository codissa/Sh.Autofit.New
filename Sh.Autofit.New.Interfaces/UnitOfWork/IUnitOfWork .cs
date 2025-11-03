using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sh.Autofit.New.Interfaces.UnitOfWork
{
    public interface IUnitOfWork : IAsyncDisposable
    {
        Task<int> SaveChangesAsync(CancellationToken ct = default);

        // Optional lightweight transaction helpers
        Task<IDisposable> BeginTransactionAsync(CancellationToken ct = default);
    }
}
