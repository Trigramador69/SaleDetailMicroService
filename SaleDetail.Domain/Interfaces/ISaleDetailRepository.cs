using System.Collections.Generic;
using System.Threading.Tasks;
using SaleDetail.Domain.Entities;

namespace SaleDetail.Domain.Interfaces
{
    public interface ISaleDetailRepository
    {
        Task<SaleDetail.Domain.Entities.SaleDetail> Create(SaleDetail.Domain.Entities.SaleDetail entity);
        Task<SaleDetail.Domain.Entities.SaleDetail?> GetById(SaleDetail.Domain.Entities.SaleDetail entity);
        Task<IEnumerable<SaleDetail.Domain.Entities.SaleDetail>> GetAll();
        Task<IEnumerable<SaleDetail.Domain.Entities.SaleDetail>> GetBySaleId(int saleId);
        Task Update(SaleDetail.Domain.Entities.SaleDetail entity);
        Task Delete(SaleDetail.Domain.Entities.SaleDetail entity);
    }
}
