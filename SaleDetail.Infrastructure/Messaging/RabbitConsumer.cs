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
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using SaleDetail.Domain.Interfaces;

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

            try
            {
                _conn = factory.CreateConnection();
                _channel = _conn.CreateModel();
                _channel.ExchangeDeclare(_exchange, ExchangeType.Topic, durable: true);
                _channel.QueueDeclare(_queueName, durable: true, exclusive: false, autoDelete: false);

                // Bindings esenciales
                _channel.QueueBind(_queueName, _exchange, "sale.header.created");
                _channel.QueueBind(_queueName, _exchange, "sale.completed");
                _channel.QueueBind(_queueName, _exchange, "sale.failed");
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Error fatal inicializando RabbitMQ en SaleDetail");
                throw;
            }
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var consumer = new AsyncEventingBasicConsumer(_channel);
            consumer.Received += OnReceived;
            _channel.BasicConsume(_queueName, autoAck: false, consumer: consumer);
            _log.LogInformation("RabbitConsumer de SaleDetail escuchando...");
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

                // Idempotencia básica (Hash del mensaje si no viene ID)
                string messageId = root.TryGetProperty("MessageId", out var midProp) && midProp.GetString() is { } midStr
                    ? midStr
                    : ComputeHash(routingKey, json);

                // Crear Scope para inyectar repositorios
                using var scope = _scopeFactory.CreateScope();

                if (routingKey == "sale.header.created")
                {
                    _log.LogInformation("Procesando nueva venta: {json}", json);
                    await ProcessSaleCreatedAndSaveDetails(root, scope);
                }
                else if (routingKey == "sale.failed")
                {
                    var saleId = root.GetProperty("sale_id").GetString();
                    _log.LogWarning("Venta falló en SAGA: {saleId}. Se podrían revertir detalles aquí.", saleId);
                }

                _channel.BasicAck(ea.DeliveryTag, multiple: false);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Error procesando mensaje RK={rk}", routingKey);
                // Nack con requeue=false para no bloquear la cola infinitamente con mensajes erróneos
                _channel.BasicNack(ea.DeliveryTag, multiple: false, requeue: false);
            }
        }

        private async Task ProcessSaleCreatedAndSaveDetails(JsonElement root, IServiceScope scope)
        {
            var saleId = root.GetProperty("sale_id").GetString();
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            // INICIAR TRANSACCIÓN
            await uow.BeginTransactionAsync();

            try
            {
                if (root.TryGetProperty("items", out var itemsElement) && itemsElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in itemsElement.EnumerateArray())
                    {
                        var medId = GetIntProperty(item, "MedId", "medId");
                        var quantity = GetIntProperty(item, "Quantity", "quantity");
                        var price = GetDecimalProperty(item, "Price", "price");

                        var detail = new SaleDetail.Domain.Entities.SaleDetail
                        {
                            sale_id = saleId,
                            medicine_id = medId,
                            quantity = quantity,
                            unit_price = price,
                            total_amount = quantity * price,
                            description = "Venta registrada via RabbitMQ",
                            created_at = DateTime.UtcNow,
                            is_deleted = false
                        };

                        // Guardar detalle usando el repositorio del UoW
                        await uow.SaleDetailRepository.Create(detail);

                        var integrationEvent = new
                        {
                            Event = "SaleDetailCreated",
                            SaleId = saleId,
                            Timestamp = DateTime.UtcNow
                        };

                        var outboxMsg = new SaleDetail.Domain.Entities.OutboxMessage
                        {
                            Id = Guid.NewGuid().ToString(),
                            AggregateId = saleId,
                            RoutingKey = "sale.detail.created",
                            Payload = JsonSerializer.Serialize(integrationEvent),
                            Status = "PENDING",
                            CreatedAt = DateTime.UtcNow
                        };

                        // Guardar outbox usando el repositorio del UoW
                        await uow.OutboxRepository.AddAsync(outboxMsg);
                    }

                    // CONFIRMAR TODO
                    await uow.CommitAsync();
                    _log.LogInformation("Detalles y Outbox guardados para venta {id}", saleId);
                }
            }
            catch (Exception ex)
            {
                // SI ALGO FALLA, REVERTIR TODO
                await uow.RollbackAsync();
                _log.LogError(ex, "Error guardando detalles. Transacción revertida.");
                throw;
            }
        }

        // --- MÉTODOS AUXILIARES ROBUSTOS (Para evitar errores de tipo String vs Number) ---

        private int GetIntProperty(JsonElement element, string prop1, string prop2)
        {
            if (element.TryGetProperty(prop1, out var p1))
            {
                if (p1.ValueKind == JsonValueKind.Number) return p1.GetInt32();
                if (p1.ValueKind == JsonValueKind.String && int.TryParse(p1.GetString(), out int val)) return val;
            }

            if (element.TryGetProperty(prop2, out var p2))
            {
                if (p2.ValueKind == JsonValueKind.Number) return p2.GetInt32();
                if (p2.ValueKind == JsonValueKind.String && int.TryParse(p2.GetString(), out int val)) return val;
            }
            return 0;
        }

        private decimal GetDecimalProperty(JsonElement element, string prop1, string prop2)
        {
            if (element.TryGetProperty(prop1, out var p1))
            {
                if (p1.ValueKind == JsonValueKind.Number) return p1.GetDecimal();
                if (p1.ValueKind == JsonValueKind.String && decimal.TryParse(p1.GetString(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal val)) return val;
            }

            if (element.TryGetProperty(prop2, out var p2))
            {
                if (p2.ValueKind == JsonValueKind.Number) return p2.GetDecimal();
                if (p2.ValueKind == JsonValueKind.String && decimal.TryParse(p2.GetString(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal val)) return val;
            }
            return 0;
        }

        private static string ComputeHash(string routingKey, string payload)
        {
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(routingKey + "|" + payload));
            return Convert.ToHexString(bytes);
        }

        public override void Dispose()
        {
            _channel?.Close();
            _conn?.Close();
            base.Dispose();
        }
    }
}