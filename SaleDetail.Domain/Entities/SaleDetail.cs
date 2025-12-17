using System;

namespace SaleDetail.Domain.Entities
{
    public class SaleDetail
    {
        public int id { get; set; }
        
        // FK al microservicio de Ventas (Sale)
        public int sale_id { get; set; }
        
        // FK al microservicio de Medicinas
        public int medicine_id { get; set; }
        
        // Datos del detalle de venta
        public int quantity { get; set; }
        public decimal unit_price { get; set; }
        public decimal total_amount { get; set; }
        
        // Descripción del producto (por si acaso)
        public string description { get; set; } = string.Empty;
        
        // Auditoría
        public bool is_deleted { get; set; }
        public DateTime created_at { get; set; }
        public DateTime? updated_at { get; set; }
        public int? created_by { get; set; }
        public int? updated_by { get; set; }
    }
}
