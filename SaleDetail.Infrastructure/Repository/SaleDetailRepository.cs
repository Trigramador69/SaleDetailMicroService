using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Threading.Tasks;
using MySql.Data.MySqlClient;
using SaleDetail.Domain.Interfaces;
// Asegúrate de que este namespace coincida con tu estructura
// using SaleDetail.Infrastructure.Persistences; 

namespace SaleDetail.Infrastructure.Repository
{
    public class SaleDetailRepository : ISaleDetailRepository
    {
        private readonly MySqlConnection _connection;
        private readonly MySqlTransaction? _transaction;

        public SaleDetailRepository(MySqlConnection connection, MySqlTransaction? transaction)
        {
            _connection = connection;
            _transaction = transaction;
        }

        //  CORRECCIÓN CRÍTICA AQUÍ: Método de mapeo robusto
        private SaleDetail.Domain.Entities.SaleDetail MapSaleDetail(DbDataReader reader)
        {
            return new SaleDetail.Domain.Entities.SaleDetail
            {
                id = reader.GetInt32("id"),

                // 1. Usar .ToString() para leer el UUID de forma segura (igual que en Sales)
                sale_id = reader["sale_id"].ToString(),

                medicine_id = reader.GetInt32("medicine_id"),
                quantity = reader.GetInt32("quantity"),
                unit_price = reader.GetDecimal("unit_price"),
                total_amount = reader.GetDecimal("total_amount"),

                // 2. Manejo seguro de nulos en descripción
                description = reader["description"] == DBNull.Value ? "" : reader["description"].ToString(),

                is_deleted = reader.GetBoolean("is_deleted"),
                created_at = reader.GetDateTime("created_at"),

                updated_at = reader["updated_at"] == DBNull.Value ? null : reader.GetDateTime("updated_at"),

                // 3. Convert.ToInt32 es más flexible que GetInt32 para tipos numéricos MySQL
                created_by = reader["created_by"] == DBNull.Value ? null : Convert.ToInt32(reader["created_by"]),
                updated_by = reader["updated_by"] == DBNull.Value ? null : Convert.ToInt32(reader["updated_by"])
            };
        }

        public async Task<SaleDetail.Domain.Entities.SaleDetail> Create(SaleDetail.Domain.Entities.SaleDetail entity)
        {
            const string query = @"
                INSERT INTO sale_details 
                (sale_id, medicine_id, quantity, unit_price, total_amount, description, created_at, created_by, updated_at, updated_by, is_deleted)
                VALUES (@sale_id, @medicine_id, @quantity, @unit_price, @total_amount, @description, @created_at, @created_by, @updated_at, @updated_by, 0);
                SELECT LAST_INSERT_ID();
            ";

            var opened = _transaction == null && _connection.State != ConnectionState.Open;
            if (opened) await _connection.OpenAsync();
            try
            {
                using var cmd = new MySqlCommand(query, _connection, _transaction);

                cmd.Parameters.AddWithValue("@sale_id", entity.sale_id);
                cmd.Parameters.AddWithValue("@medicine_id", entity.medicine_id);
                cmd.Parameters.AddWithValue("@quantity", entity.quantity);
                cmd.Parameters.AddWithValue("@unit_price", entity.unit_price);
                cmd.Parameters.AddWithValue("@total_amount", entity.total_amount);
                cmd.Parameters.AddWithValue("@description", entity.description ?? ""); // Evitar null
                cmd.Parameters.AddWithValue("@created_at", entity.created_at);
                cmd.Parameters.AddWithValue("@created_by", entity.created_by.HasValue ? (object)entity.created_by.Value : DBNull.Value);
                cmd.Parameters.AddWithValue("@updated_at", entity.updated_at.HasValue ? (object)entity.updated_at.Value : DBNull.Value);
                cmd.Parameters.AddWithValue("@updated_by", entity.updated_by.HasValue ? (object)entity.updated_by.Value : DBNull.Value);

                var newId = await cmd.ExecuteScalarAsync();
                entity.id = Convert.ToInt32(newId);

                return entity;
            }
            finally
            {
                if (opened) _connection.Close();
            }
        }

        public async Task<SaleDetail.Domain.Entities.SaleDetail?> GetById(SaleDetail.Domain.Entities.SaleDetail entity)
        {
            const string query = @"
                SELECT id, sale_id, medicine_id, quantity, unit_price, total_amount, description,
                       is_deleted, created_at, updated_at, created_by, updated_by
                FROM sale_details
                WHERE id = @id AND is_deleted = 0;
            ";

            var opened = _transaction == null && _connection.State != ConnectionState.Open;
            if (opened) await _connection.OpenAsync();
            try
            {
                using var cmd = new MySqlCommand(query, _connection, _transaction);
                cmd.Parameters.AddWithValue("@id", entity.id);

                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    return MapSaleDetail(reader);
                }

                return null;
            }
            finally
            {
                if (opened) _connection.Close();
            }
        }

        public async Task<IEnumerable<SaleDetail.Domain.Entities.SaleDetail>> GetAll()
        {
            const string query = @"
                SELECT id, sale_id, medicine_id, quantity, unit_price, total_amount, description,
                       is_deleted, created_at, updated_at, created_by, updated_by
                FROM sale_details
                WHERE is_deleted = 0
                ORDER BY created_at DESC;
            ";

            var list = new List<SaleDetail.Domain.Entities.SaleDetail>();

            var opened = _transaction == null && _connection.State != ConnectionState.Open;
            if (opened) await _connection.OpenAsync();
            try
            {
                using var cmd = new MySqlCommand(query, _connection, _transaction);
                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    list.Add(MapSaleDetail(reader));
                }

                return list;
            }
            finally
            {
                if (opened) _connection.Close();
            }
        }

        public async Task<IEnumerable<SaleDetail.Domain.Entities.SaleDetail>> GetBySaleId(string saleId)
        {
            const string query = @"
                SELECT id, sale_id, medicine_id, quantity, unit_price, total_amount, description,
                       is_deleted, created_at, updated_at, created_by, updated_by
                FROM sale_details
                WHERE sale_id = @sale_id AND is_deleted = 0
                ORDER BY created_at DESC;
            ";

            var list = new List<SaleDetail.Domain.Entities.SaleDetail>();

            var opened = _transaction == null && _connection.State != ConnectionState.Open;
            if (opened) await _connection.OpenAsync();
            try
            {
                using var cmd = new MySqlCommand(query, _connection, _transaction);
                cmd.Parameters.AddWithValue("@sale_id", saleId);

                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    list.Add(MapSaleDetail(reader));
                }

                return list;
            }
            finally
            {
                if (opened) _connection.Close();
            }
        }

        public async Task Update(SaleDetail.Domain.Entities.SaleDetail entity)
        {
            const string query = @"
                UPDATE sale_details
                SET sale_id = @sale_id,
                    medicine_id = @medicine_id,
                    quantity = @quantity,
                    unit_price = @unit_price,
                    total_amount = @total_amount,
                    description = @description,
                    updated_at = @updated_at,
                    updated_by = @updated_by
                WHERE id = @id;
            ";

            var opened = _transaction == null && _connection.State != ConnectionState.Open;
            if (opened) await _connection.OpenAsync();
            try
            {
                using var cmd = new MySqlCommand(query, _connection, _transaction);

                cmd.Parameters.AddWithValue("@id", entity.id);
                cmd.Parameters.AddWithValue("@sale_id", entity.sale_id);
                cmd.Parameters.AddWithValue("@medicine_id", entity.medicine_id);
                cmd.Parameters.AddWithValue("@quantity", entity.quantity);
                cmd.Parameters.AddWithValue("@unit_price", entity.unit_price);
                cmd.Parameters.AddWithValue("@total_amount", entity.total_amount);
                cmd.Parameters.AddWithValue("@description", entity.description ?? "");
                cmd.Parameters.AddWithValue("@updated_at", entity.updated_at.HasValue ? (object)entity.updated_at.Value : DBNull.Value);
                cmd.Parameters.AddWithValue("@updated_by", entity.updated_by.HasValue ? (object)entity.updated_by.Value : DBNull.Value);

                await cmd.ExecuteNonQueryAsync();
            }
            finally
            {
                if (opened) _connection.Close();
            }
        }

        public async Task Delete(SaleDetail.Domain.Entities.SaleDetail entity)
        {
            const string query = @"
                UPDATE sale_details
                SET is_deleted = 1,
                    updated_at = @updated_at,
                    updated_by = @updated_by
                WHERE id = @id;
            ";

            var opened = _transaction == null && _connection.State != ConnectionState.Open;
            if (opened) await _connection.OpenAsync();
            try
            {
                using var cmd = new MySqlCommand(query, _connection, _transaction);

                cmd.Parameters.AddWithValue("@id", entity.id);
                cmd.Parameters.AddWithValue("@updated_at", entity.updated_at.HasValue ? (object)entity.updated_at.Value : DBNull.Value);
                cmd.Parameters.AddWithValue("@updated_by", entity.updated_by.HasValue ? (object)entity.updated_by.Value : DBNull.Value);

                await cmd.ExecuteNonQueryAsync();
            }
            finally
            {
                if (opened) _connection.Close();
            }
        }
    }
}