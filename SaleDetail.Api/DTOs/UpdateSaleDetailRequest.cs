namespace SaleDetail.Api.DTOs
{
    public class UpdateSaleDetailRequest
    {
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalAmount { get; set; }
        public string? Description { get; set; }
        public int? UpdatedBy { get; set; }
    }
}
