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
                entity.description = NormalizeText(entity.description);

                var validationResult = _validator.Validate(entity);
                if (validationResult.IsFailed)
                {
                    // Lógica de error... (simplificada para el ejemplo)
                    ErrorMapping.ThrowIfFailed(validationResult);
                }

                entity.total_amount = entity.quantity * entity.unit_price;
                entity.created_at = DateTime.Now;
                entity.created_by = actorId;
                entity.is_deleted = false;

                var created = await _unitOfWork.SaleDetailRepository.Create(entity);

                // Publicar evento
                var outboxMsg = new OutboxMessage
                {
                    Id = Guid.NewGuid().ToString(),
                    AggregateId = created.sale_id, // sale_id ya es string
                    RoutingKey = "saledetail.created",
                    Payload = JsonSerializer.Serialize(new { sale_detail_id = created.id, sale_id = created.sale_id }),
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

        // ✅ CORREGIDO: GetById usa ID numérico (PK)
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

        // ✅ CORREGIDO: GetBySaleId usa STRING porque viene de Sale.Api (GUID)
        public async Task<IEnumerable<SaleDetail.Domain.Entities.SaleDetail>> GetBySaleIdAsync(string saleId)
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
                // Busca por ID numérico
                var existing = await _unitOfWork.SaleDetailRepository.GetById(entity);
                if (existing is null || existing.is_deleted)
                    throw new NotFoundException($"El detalle {entity.id} no existe.");

                entity.description = NormalizeText(entity.description);
                entity.total_amount = entity.quantity * entity.unit_price;
                entity.updated_at = DateTime.Now;
                entity.updated_by = actorId;

                await _unitOfWork.SaleDetailRepository.Update(entity);

                // Outbox...
                await _unitOfWork.CommitAsync();
            }
            catch
            {
                await _unitOfWork.RollbackAsync();
                throw;
            }
        }

        // ✅ CORREGIDO: DeleteAsync usa INT id (el ID de la fila a borrar)
        public async Task DeleteAsync(int id, int actorId)
        {
            await _unitOfWork.BeginTransactionAsync();
            try
            {
                var entity = new SaleDetail.Domain.Entities.SaleDetail { id = id };
                var existing = await _unitOfWork.SaleDetailRepository.GetById(entity);

                if (existing is null || existing.is_deleted)
                    throw new NotFoundException($"El detalle {id} no existe.");

                existing.is_deleted = true;
                existing.updated_at = DateTime.Now;
                existing.updated_by = actorId;

                await _unitOfWork.SaleDetailRepository.Delete(existing);

                // Outbox...
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