using System.Threading.Tasks;

namespace SaleDetail.Application.Interfaces
{
    public interface IMedicineGateway
    {
        Task<bool> ExistsMedicine(int medicineId);
    }
}
