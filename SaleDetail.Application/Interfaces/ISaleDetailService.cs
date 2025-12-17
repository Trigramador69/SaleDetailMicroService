using System.Collections.Generic;
using System.Threading.Tasks;

namespace SaleDetail.Application.Interfaces
{
    public interface ISaleDetailService
    {
        Task<SaleDetail.Domain.Entities.SaleDetail> RegisterAsync(SaleDetail.Domain.Entities.SaleDetail entity, int actorId);
        Task<SaleDetail.Domain.Entities.SaleDetail?> GetByIdAsync(int id);
        Task<IEnumerable<SaleDetail.Domain.Entities.SaleDetail>> GetAllAsync();
        Task<IEnumerable<SaleDetail.Domain.Entities.SaleDetail>> GetBySaleIdAsync(int saleId);
        Task UpdateAsync(SaleDetail.Domain.Entities.SaleDetail entity, int actorId);
        Task DeleteAsync(int id, int actorId);
    }
}
