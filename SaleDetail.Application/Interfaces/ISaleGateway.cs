using System.Threading.Tasks;

namespace SaleDetail.Application.Interfaces
{
    public interface ISaleGateway
    {
        Task<bool> ExistsSale(int saleId);
    }
}
