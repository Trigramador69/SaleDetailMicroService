using FluentResults;
using SaleDetail.Domain.Interfaces;

namespace SaleDetail.Application.Validators
{
    public class SaleDetailValidator : IValidator<SaleDetail.Domain.Entities.SaleDetail>
    {
        public Result Validate(SaleDetail.Domain.Entities.SaleDetail sd)
        {
            var r = Result.Ok();

            // 1. Validar Sale ID
            if (sd.sale_id <= 0)
            {
                r = r.WithFieldError("sale_id", "Debe seleccionar una venta válida.");
            }

            // 2. Validar Medicine ID
            if (sd.medicine_id <= 0)
            {
                r = r.WithFieldError("medicine_id", "Debe seleccionar un medicamento válido.");
            }

            // 3. Validar Cantidad
            if (sd.quantity <= 0)
            {
                r = r.WithFieldError("quantity", "La cantidad debe ser mayor a cero.");
            }

            // 4. Validar Precio Unitario
            if (sd.unit_price <= 0)
            {
                r = r.WithFieldError("unit_price", "El precio unitario debe ser mayor a cero.");
            }

            // 5. Validar Descripción (opcional, pero si viene debe tener longitud válida)
            if (!string.IsNullOrWhiteSpace(sd.description))
            {
                var desc = sd.description.Trim();
                if (desc.Length > 200)
                {
                    r = r.WithFieldError("description", "La descripción no puede superar los 200 caracteres.");
                }
            }

            return r;
        }
    }
}
