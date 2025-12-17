using System.Threading.Tasks;

namespace SaleDetail.Domain.Interfaces
{
    public interface IEventPublisher
    {
        Task PublishAsync(string routingKey, object @event);
    }
}
