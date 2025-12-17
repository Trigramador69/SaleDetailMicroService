namespace SaleDetail.Api.DTOs
{
    public class CreateSaleDetailRequest
    {
        public int SaleId { get; set; }
        public int MedicineId { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalAmount { get; set; }
        public string Description { get; set; } = string.Empty;
        public int? CreatedBy { get; set; }
    }
}
