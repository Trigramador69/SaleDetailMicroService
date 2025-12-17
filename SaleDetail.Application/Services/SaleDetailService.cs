using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using FluentResults;
using SaleDetail.Application.Interfaces;
using SaleDetail.Application.Validators;
using SaleDetail.Domain.Entities;
using SaleDetail.Domain.Exceptions;
using SaleDetail.Domain.Interfaces;

namespace SaleDetail.Application.Services
{
    public class SaleDetailService : ISaleDetailService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IValidator<SaleDetail.Domain.Entities.SaleDetail> _validator;
        private readonly IMedicineGateway _medicineGateway;
        private readonly ISaleGateway _saleGateway;

        public SaleDetailService(
            IUnitOfWork unitOfWork,
            IValidator<SaleDetail.Domain.Entities.SaleDetail> validator,
            IMedicineGateway medicineGateway,
            ISaleGateway saleGateway)
        {
            _unitOfWork = unitOfWork;
            _validator = validator;
            _medicineGateway = medicineGateway;
            _saleGateway = saleGateway;
        }

        private static readonly Regex MultiSpace = new(@"\s+", RegexOptions.Compiled);

        private static string NormalizeText(string? s)
            => string.IsNullOrWhiteSpace(s) ? string.Empty : MultiSpace.Replace(s.Trim(), " ");

        public async Task<SaleDetail.Domain.Entities.SaleDetail> RegisterAsync(SaleDetail.Domain.Entities.SaleDetail entity, int actorId)
        {
            if (entity is null) throw new ArgumentNullException(nameof(entity));

            await _unitOfWork.BeginTransactionAsync();
            try
            {
                // 1. Normalización
                entity.description = NormalizeText(entity.description);

                // 2. Validación de dominio
                var validationResult = _validator.Validate(entity);
                if (validationResult.IsFailed)
                {
                    var errorsDict = new Dictionary<string, string>();
                    foreach (var err in validationResult.Errors)
                    {
                        if (!errorsDict.ContainsKey(err.Message))
                            errorsDict.Add("General", err.Message);
                    }
                    ErrorMapping.ThrowIfFailed(validationResult);
                }

                // 3. Verificar existencia de la venta (Sale) - TEMPORALMENTE DESACTIVADO
                // bool saleExists = await _saleGateway.ExistsSale(entity.sale_id);
                // if (!saleExists)
                //     throw new ValidationException("La venta especificada no existe.",
                //         new Dictionary<string, string> { { "sale_id", "La venta no existe o fue eliminada." } });

                // 4. Verificar existencia del medicamento - TEMPORALMENTE DESACTIVADO
                // bool medicineExists = await _medicineGateway.ExistsMedicine(entity.medicine_id);
                // if (!medicineExists)
                //     throw new ValidationException("El medicamento especificado no existe.",
                //         new Dictionary<string, string> { { "medicine_id", "El medicamento no existe o fue eliminado." } });

                // 5. Calcular total_amount
                entity.total_amount = entity.quantity * entity.unit_price;

                // 6. Auditoría
                entity.created_at = DateTime.Now;
                entity.created_by = actorId;
                entity.is_deleted = false;

                // 7. Persistir
                var created = await _unitOfWork.SaleDetailRepository.Create(entity);

                // 8. Publicar evento vía Outbox (saga)
                var outboxMsg = new OutboxMessage
                {
                    Id = Guid.NewGuid().ToString(),
                    AggregateId = created.sale_id.ToString(),
                    RoutingKey = "saledetail.created",
                    Payload = JsonSerializer.Serialize(new
                    {
                        MessageId = Guid.NewGuid().ToString(),
                        sale_detail_id = created.id,
                        sale_id = created.sale_id,
                        medicine_id = created.medicine_id,
                        quantity = created.quantity,
                        unit_price = created.unit_price,
                        total_amount = created.total_amount,
                        created_at = created.created_at
                    }),
                    Status = "PENDING",
                    CreatedAt = DateTime.UtcNow
                };
                await _unitOfWork.OutboxRepository.AddAsync(outboxMsg);

                await _unitOfWork.CommitAsync();
                return created;
            }
            catch
            {
                await _unitOfWork.RollbackAsync();
                throw;
            }
        }

        public async Task<SaleDetail.Domain.Entities.SaleDetail?> GetByIdAsync(int id)
        {
            await _unitOfWork.EnsureConnectionOpenAsync();
            var entity = new SaleDetail.Domain.Entities.SaleDetail { id = id };
            return await _unitOfWork.SaleDetailRepository.GetById(entity);
        }

        public async Task<IEnumerable<SaleDetail.Domain.Entities.SaleDetail>> GetAllAsync()
        {
            await _unitOfWork.EnsureConnectionOpenAsync();
            return await _unitOfWork.SaleDetailRepository.GetAll();
        }

        public async Task<IEnumerable<SaleDetail.Domain.Entities.SaleDetail>> GetBySaleIdAsync(int saleId)
        {
            await _unitOfWork.EnsureConnectionOpenAsync();
            return await _unitOfWork.SaleDetailRepository.GetBySaleId(saleId);
        }

        public async Task UpdateAsync(SaleDetail.Domain.Entities.SaleDetail entity, int actorId)
        {
            if (entity is null) throw new ArgumentNullException(nameof(entity));

            await _unitOfWork.BeginTransactionAsync();
            try
            {
                // Verificar que exista
                var existing = await _unitOfWork.SaleDetailRepository.GetById(entity);
                if (existing is null || existing.is_deleted)
                    throw new NotFoundException($"El detalle de venta con ID {entity.id} no fue encontrado.");

                // Normalizar y validar
                entity.description = NormalizeText(entity.description);
                var validationResult = _validator.Validate(entity);
                ErrorMapping.ThrowIfFailed(validationResult);

                // Verificar existencia de la venta (Sale)
                bool saleExists = await _saleGateway.ExistsSale(entity.sale_id);
                if (!saleExists)
                    throw new ValidationException("La venta especificada no existe.",
                        new Dictionary<string, string> { { "sale_id", "La venta no existe o fue eliminada." } });

                // Verificar existencia del medicamento
                bool medicineExists = await _medicineGateway.ExistsMedicine(entity.medicine_id);
                if (!medicineExists)
                    throw new ValidationException("El medicamento especificado no existe.",
                        new Dictionary<string, string> { { "medicine_id", "El medicamento no existe o fue eliminado." } });

                // Recalcular total_amount
                entity.total_amount = entity.quantity * entity.unit_price;

                // Auditoría
                entity.updated_at = DateTime.Now;
                entity.updated_by = actorId;

                await _unitOfWork.SaleDetailRepository.Update(entity);

                // Publicar evento de actualización
                var outboxMsg = new OutboxMessage
                {
                    Id = Guid.NewGuid().ToString(),
                    AggregateId = entity.sale_id.ToString(),
                    RoutingKey = "saledetail.updated",
                    Payload = JsonSerializer.Serialize(new
                    {
                        MessageId = Guid.NewGuid().ToString(),
                        sale_detail_id = entity.id,
                        sale_id = entity.sale_id,
                        medicine_id = entity.medicine_id,
                        quantity = entity.quantity,
                        unit_price = entity.unit_price,
                        total_amount = entity.total_amount,
                        updated_at = entity.updated_at
                    }),
                    Status = "PENDING",
                    CreatedAt = DateTime.UtcNow
                };
                await _unitOfWork.OutboxRepository.AddAsync(outboxMsg);

                await _unitOfWork.CommitAsync();
            }
            catch
            {
                await _unitOfWork.RollbackAsync();
                throw;
            }
        }

        public async Task DeleteAsync(int id, int actorId)
        {
            await _unitOfWork.BeginTransactionAsync();
            try
            {
                var entity = new SaleDetail.Domain.Entities.SaleDetail { id = id };
                var existing = await _unitOfWork.SaleDetailRepository.GetById(entity);

                if (existing is null || existing.is_deleted)
                    throw new NotFoundException($"El detalle de venta con ID {id} no fue encontrado.");

                existing.is_deleted = true;
                existing.updated_at = DateTime.Now;
                existing.updated_by = actorId;

                await _unitOfWork.SaleDetailRepository.Delete(existing);

                // Publicar evento de eliminación
                var outboxMsg = new OutboxMessage
                {
                    Id = Guid.NewGuid().ToString(),
                    AggregateId = existing.sale_id.ToString(),
                    RoutingKey = "saledetail.deleted",
                    Payload = JsonSerializer.Serialize(new
                    {
                        MessageId = Guid.NewGuid().ToString(),
                        sale_detail_id = existing.id,
                        sale_id = existing.sale_id,
                        deleted_at = existing.updated_at
                    }),
                    Status = "PENDING",
                    CreatedAt = DateTime.UtcNow
                };
                await _unitOfWork.OutboxRepository.AddAsync(outboxMsg);

                await _unitOfWork.CommitAsync();
            }
            catch
            {
                await _unitOfWork.RollbackAsync();
                throw;
            }
        }
    }
}
