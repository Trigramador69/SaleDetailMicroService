using System;
using System.Data;
using System.Threading.Tasks;
using MySql.Data.MySqlClient;
using SaleDetail.Domain.Interfaces;
using SaleDetail.Infrastructure.Persistences;

namespace SaleDetail.Infrastructure.Repository
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly MySqlConnection _connection;
        private MySqlTransaction? _transaction;

        private SaleDetailRepository? _saleDetailRepository;
        private OutboxRepository? _outboxRepository;

        public UnitOfWork()
        {
            _connection = DatabaseConnection.Instance.GetConnection();
        }

        public ISaleDetailRepository SaleDetailRepository
        {
            get
            {
                return _saleDetailRepository ??= new SaleDetailRepository(_connection, _transaction);
            }
        }

        public IOutboxRepository OutboxRepository
        {
            get
            {
                return _outboxRepository ??= new OutboxRepository(_connection, _transaction);
            }
        }

        public async Task BeginTransactionAsync()
        {
            if (_connection.State != ConnectionState.Open)
                await _connection.OpenAsync();

            _transaction = await _connection.BeginTransactionAsync();

            _saleDetailRepository = null;
            _outboxRepository = null;
        }

        public async Task EnsureConnectionOpenAsync()
        {
            if (_connection.State != ConnectionState.Open)
            {
                await _connection.OpenAsync();
            }
        }

        public async Task CommitAsync()
        {
            try
            {
                await _transaction!.CommitAsync();
            }
            catch
            {
                await RollbackAsync();
                throw;
            }
            finally
            {
                await DisposeTransaction();
            }
        }

        public async Task RollbackAsync()
        {
            if (_transaction != null)
                await _transaction.RollbackAsync();

            await DisposeTransaction();
        }

        private Task DisposeTransaction()
        {
            if (_transaction != null)
            {
                _transaction.Dispose();
                _transaction = null;
            }

            _saleDetailRepository = null;
            _outboxRepository = null;

            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _transaction?.Dispose();
            if (_connection.State == ConnectionState.Open)
                _connection.Close();
            _connection.Dispose();
        }
    }
}
