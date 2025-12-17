using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MySql.Data.MySqlClient;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using SaleDetail.Domain.Interfaces;
using SaleDetail.Infrastructure.Persistences;

namespace SaleDetail.Infrastructure.Messaging
{
    public class RabbitConsumer : BackgroundService
    {
        private readonly IConnection _conn;
        private readonly IModel _channel;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<RabbitConsumer> _log;
        private readonly string _exchange;
        private readonly string _queueName = "saledetail.queue";

        public RabbitConsumer(IConfiguration cfg, IServiceScopeFactory scopeFactory, ILogger<RabbitConsumer> log)
        {
            _scopeFactory = scopeFactory;
            _log = log;

            var factory = new ConnectionFactory
            {
                HostName = cfg["RabbitMQ:Host"] ?? "localhost",
                UserName = cfg["RabbitMQ:User"] ?? "guest",
                Password = cfg["RabbitMQ:Password"] ?? "guest",
                DispatchConsumersAsync = true
            };

            _exchange = cfg["RabbitMQ:Exchange"] ?? "saga.exchange";
            _conn = factory.CreateConnection();
            _channel = _conn.CreateModel();
            _channel.ExchangeDeclare(_exchange, ExchangeType.Topic, durable: true);

            _channel.QueueDeclare(_queueName, durable: true, exclusive: false, autoDelete: false);

            // Bind routing keys que este servicio necesita escuchar
            _channel.QueueBind(_queueName, _exchange, "sale.created");
            _channel.QueueBind(_queueName, _exchange, "sale.completed");
            _channel.QueueBind(_queueName, _exchange, "sale.failed");
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var consumer = new AsyncEventingBasicConsumer(_channel);
            consumer.Received += OnReceived;
            _channel.BasicConsume(_queueName, autoAck: false, consumer: consumer);
            _log.LogInformation("RabbitConsumer de SaleDetail iniciado");
            return Task.CompletedTask;
        }

        private async Task OnReceived(object sender, BasicDeliverEventArgs ea)
        {
            var json = Encoding.UTF8.GetString(ea.Body.ToArray());
            var routingKey = ea.RoutingKey;

            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                // MessageId for idempotency
                string messageId = root.TryGetProperty("MessageId", out var midProp) && midProp.GetString() is { } midStr && !string.IsNullOrEmpty(midStr)
                    ? midStr
                    : ComputeHash(routingKey, json);

                using var scope = _scopeFactory.CreateScope();
                var publisher = scope.ServiceProvider.GetService<IEventPublisher>();

                // Procesamiento por routing key
                if (routingKey == "sale.created")
                {
                    // Cuando se crea una venta, podríamos calcular el total de los detalles y publicar un evento
                    var saleId = root.GetProperty("sale_id").GetString();
                    
                    _log.LogInformation("Venta creada recibida: {sale}", saleId);
                    
                    // Aquí podríamos calcular el total de los detalles de venta y publicar
                    var totalCalculated = await CalculateSaleTotalAsync(saleId);
                    
                    if (publisher != null)
                    {
                        var evt = new
                        {
                            MessageId = Guid.NewGuid().ToString(),
                            sale_id = saleId,
                            total_calculated = totalCalculated,
                            calculated_at = DateTime.UtcNow
                        };
                        await publisher.PublishAsync("sale.details.persisted", evt);
                        _log.LogInformation("Evento sale.details.persisted publicado para venta {sale}", saleId);
                    }
                }
                else if (routingKey == "sale.completed")
                {
                    var saleId = root.GetProperty("sale_id").GetString();
                    _log.LogInformation("Venta completada: {sale}", saleId);
                    // Aquí podrías marcar los detalles como finalizados, etc.
                }
                else if (routingKey == "sale.failed")
                {
                    var saleId = root.GetProperty("sale_id").GetString();
                    var reason = root.TryGetProperty("reason", out var r) ? r.GetString() : "unknown";
                    _log.LogWarning("Venta fallida: {sale}, razón: {reason}", saleId, reason);
                    // Aquí podrías revertir operaciones, etc.
                }
                else
                {
                    _log.LogWarning("Routing key no manejada: {rk}", routingKey);
                }

                // ACK para eliminar de la cola
                _channel.BasicAck(ea.DeliveryTag, multiple: false);
            }
            catch (JsonException jex)
            {
                _log.LogError(jex, "Mensaje JSON inválido routingKey={rk} body={b}", routingKey, json);
                _channel.BasicAck(ea.DeliveryTag, multiple: false);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Error procesando mensaje routingKey={rk} body={b}", routingKey, json);
                _channel.BasicNack(ea.DeliveryTag, multiple: false, requeue: true);
            }
        }

        private static string ComputeHash(string routingKey, string payload)
        {
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(routingKey + "|" + payload));
            return Convert.ToHexString(bytes);
        }

        private async Task<decimal> CalculateSaleTotalAsync(string? saleId)
        {
            if (string.IsNullOrEmpty(saleId)) return 0;
            
            // Convertir el sale_id string a int (ajusta según tu BD)
            if (!int.TryParse(saleId, out var saleIdInt))
                return 0;

            const string sql = @"SELECT COALESCE(SUM(total_amount), 0) as total FROM sale_details WHERE sale_id = @sale_id AND is_deleted = 0;";
            
            using var con = DatabaseConnection.Instance.GetConnection();
            await con.OpenAsync();
            using var cmd = new MySqlCommand(sql, con);
            cmd.Parameters.AddWithValue("@sale_id", saleIdInt);
            
            var result = await cmd.ExecuteScalarAsync();
            return Convert.ToDecimal(result);
        }

        public override void Dispose()
        {
            _channel?.Close();
            _conn?.Close();
            base.Dispose();
        }
    }
}
