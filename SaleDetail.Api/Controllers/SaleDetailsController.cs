using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaleDetail.Api.DTOs;
using SaleDetail.Application.Interfaces;
using SaleDetail.Domain.Exceptions;

namespace SaleDetail.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    //[Authorize]
    public class SaleDetailsController : ControllerBase
    {
        private readonly ISaleDetailService _saleDetailService;
        private readonly ILogger<SaleDetailsController> _logger;

        public SaleDetailsController(
            ISaleDetailService saleDetailService,
            ILogger<SaleDetailsController> logger)
        {
            _saleDetailService = saleDetailService;
            _logger = logger;
        }

        // Método auxiliar simplificado
        private static int ParseActorId(string? header) =>
            int.TryParse(header, out var id) ? id : 0;

        [HttpPost]
        [ProducesResponseType(typeof(SaleDetail.Domain.Entities.SaleDetail), (int)HttpStatusCode.Created)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest)]
        public async Task<IActionResult> Register(
            [FromBody] CreateSaleDetailRequest request,
            [FromHeader(Name = "X-Actor-Id")] string? actorHeader)
        {
            try
            {
                // Log profesional en lugar de Console.WriteLine
                _logger.LogInformation("Iniciando registro de detalle. SaleId: {SaleId}, MedicineId: {MedicineId}",
                    request.SaleId, request.MedicineId);

                var actorId = ParseActorId(actorHeader);

                // Mapeo manual (DTO -> Entity)
                var saleDetail = new SaleDetail.Domain.Entities.SaleDetail
                {
                    sale_id = request.SaleId,
                    medicine_id = request.MedicineId,
                    quantity = request.Quantity,
                    unit_price = request.UnitPrice,
                    total_amount = request.TotalAmount,
                    description = request.Description,
                    created_by = request.CreatedBy
                };

                var created = await _saleDetailService.RegisterAsync(saleDetail, actorId);

                return CreatedAtAction(nameof(GetById), new { id = created.id }, created);
            }
            catch (ValidationException ex)
            {
                _logger.LogWarning(ex, "Error de validación al registrar detalle");
                return BadRequest(new { message = ex.Message, errors = ex.Errors });
            }
            catch (DomainException ex)
            {
                _logger.LogError(ex, "Error de dominio al registrar detalle");
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet("{id}")]
        [ProducesResponseType(typeof(SaleDetail.Domain.Entities.SaleDetail), (int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        public async Task<IActionResult> GetById(int id)
        {
            try
            {
                var result = await _saleDetailService.GetByIdAsync(id);
                if (result == null)
                {
                    _logger.LogWarning("Detalle de venta con ID {Id} no encontrado", id);
                    return NotFound(new { message = $"Detalle de venta con ID {id} no encontrado." });
                }

                return Ok(result);
            }
            catch (DomainException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<SaleDetail.Domain.Entities.SaleDetail>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> GetAll()
        {
            try
            {
                var results = await _saleDetailService.GetAllAsync();
                return Ok(results);
            }
            catch (DomainException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet("sale/{saleId}")]
        [ProducesResponseType(typeof(IEnumerable<SaleDetail.Domain.Entities.SaleDetail>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> GetBySaleId(string saleId)
        {
            try
            {
                var results = await _saleDetailService.GetBySaleIdAsync(saleId);
                return Ok(results);
            }
            catch (DomainException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPut("{id}")]
        [ProducesResponseType((int)HttpStatusCode.NoContent)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest)]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        public async Task<IActionResult> Update(
            int id,
            [FromBody] UpdateSaleDetailRequest request,
            [FromHeader(Name = "X-Actor-Id")] string? actorHeader)
        {
            try
            {
                var actorId = ParseActorId(actorHeader);

                var existing = await _saleDetailService.GetByIdAsync(id);
                if (existing == null)
                    return NotFound(new { message = $"Detalle de venta con ID {id} no encontrado." });

                // Mapeo de actualización
                existing.quantity = request.Quantity;
                existing.unit_price = request.UnitPrice;
                existing.total_amount = request.TotalAmount;

                if (!string.IsNullOrEmpty(request.Description))
                    existing.description = request.Description;

                existing.updated_by = request.UpdatedBy;

                await _saleDetailService.UpdateAsync(existing, actorId);
                return NoContent();
            }
            catch (NotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (ValidationException ex)
            {
                return BadRequest(new { message = ex.Message, errors = ex.Errors });
            }
            catch (DomainException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpDelete("{id}")]
        [ProducesResponseType((int)HttpStatusCode.NoContent)]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        public async Task<IActionResult> Delete(
            int id,
            [FromHeader(Name = "X-Actor-Id")] string? actorHeader)
        {
            try
            {
                var actorId = ParseActorId(actorHeader);
                await _saleDetailService.DeleteAsync(id, actorId);
                return NoContent();
            }
            catch (NotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (DomainException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}