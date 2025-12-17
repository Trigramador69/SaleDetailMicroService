using System.Collections.Generic;
using System.Threading.Tasks;
using SaleDetail.Domain.Entities;

namespace SaleDetail.Domain.Interfaces
{
    public interface IOutboxRepository
    {
        Task AddAsync(OutboxMessage message);
        Task<IEnumerable<OutboxMessage>> GetPendingAsync(int limit = 100);
        Task MarkSentAsync(string id);
        Task IncrementAttemptAsync(string id);
    }
}
