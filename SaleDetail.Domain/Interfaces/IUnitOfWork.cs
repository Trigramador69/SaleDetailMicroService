using System;
using System.Threading.Tasks;

namespace SaleDetail.Domain.Interfaces
{
    public interface IUnitOfWork : IDisposable
    {
        Task BeginTransactionAsync();
        Task CommitAsync();
        Task RollbackAsync();
        Task EnsureConnectionOpenAsync();
        
        ISaleDetailRepository SaleDetailRepository { get; }
        IOutboxRepository OutboxRepository { get; }
    }
}
