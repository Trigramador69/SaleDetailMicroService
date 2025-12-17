using FluentResults;

namespace SaleDetail.Domain.Interfaces
{
    public interface IValidator<T>
    {
        Result Validate(T entity);
    }
}
