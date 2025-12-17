using System;

namespace SaleDetail.Domain.Entities
{
    public class OutboxMessage
    {
        public string Id { get; set; } = string.Empty;
        public string AggregateId { get; set; } = string.Empty;
        public string RoutingKey { get; set; } = string.Empty;
        public string Payload { get; set; } = string.Empty;
        public string Status { get; set; } = "PENDING"; // PENDING | PUBLISHED | FAILED
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? PublishedAt { get; set; }
        public int AttemptCount { get; set; } = 0;
        public string? ErrorLog { get; set; }
    }
}
