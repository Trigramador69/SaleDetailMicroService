using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using System;

namespace SaleDetail.Infrastructure.Messaging
{
    /// <summary>
    /// Configuración inicial de RabbitMQ para el microservicio SaleDetail
    /// Crea la cola y los bindings necesarios al iniciar la aplicación
    /// </summary>
    public class RabbitMQConfiguration
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<RabbitMQConfiguration> _logger;

        public RabbitMQConfiguration(IConfiguration configuration, ILogger<RabbitMQConfiguration> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        /// <summary>
        /// Inicializa la infraestructura de RabbitMQ:
        /// - Verifica/crea el exchange 'saga.exchange'
        /// - Crea la cola 'saledetail.queue'
        /// - Configura los bindings necesarios
        /// </summary>
        public void Initialize()
        {
            try
            {
                var factory = new ConnectionFactory
                {
                    HostName = _configuration["RabbitMQ:Host"] ?? "localhost",
                    UserName = _configuration["RabbitMQ:User"] ?? "guest",
                    Password = _configuration["RabbitMQ:Password"] ?? "guest"
                };

                var exchange = _configuration["RabbitMQ:Exchange"] ?? "saga.exchange";
                var queueName = "saledetail.queue";

                using var connection = factory.CreateConnection();
                using var channel = connection.CreateModel();

                // 1. Declarar exchange (topic) - idempotente, no falla si ya existe
                channel.ExchangeDeclare(
                    exchange: exchange,
                    type: ExchangeType.Topic,
                    durable: true,
                    autoDelete: false,
                    arguments: null);

                _logger.LogInformation("✅ Exchange '{Exchange}' declarado/verificado", exchange);

                // 2. Declarar cola para este microservicio
                channel.QueueDeclare(
                    queue: queueName,
                    durable: true,        // Persistente (sobrevive reinicios)
                    exclusive: false,     // No exclusiva (múltiples consumidores pueden conectarse)
                    autoDelete: false,    // No se borra automáticamente
                    arguments: null);

                _logger.LogInformation("✅ Cola '{Queue}' creada/verificada", queueName);

                // 3. Bindings: conectar la cola al exchange con routing keys específicos
                // Este microservicio escucha eventos de ventas
                var routingKeys = new[]
                {
                    "sale.created",       // Cuando se crea una venta
                    "sale.completed",     // Cuando se completa una venta
                    "sale.failed",        // Cuando falla una venta
                    "saledetail.*"        // [TESTING] Escuchar sus propios eventos para verificar
                };

                foreach (var routingKey in routingKeys)
                {
                    channel.QueueBind(
                        queue: queueName,
                        exchange: exchange,
                        routingKey: routingKey,
                        arguments: null);

                    _logger.LogInformation("✅ Binding creado: {Queue} <- {Exchange} [{RoutingKey}]", 
                        queueName, exchange, routingKey);
                }

                _logger.LogInformation("🎉 Configuración RabbitMQ completada exitosamente");
                _logger.LogInformation("   Exchange: {Exchange}", exchange);
                _logger.LogInformation("   Cola: {Queue}", queueName);
                _logger.LogInformation("   Routing Keys: {Keys}", string.Join(", ", routingKeys));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error al configurar RabbitMQ: {Message}", ex.Message);
                _logger.LogWarning("⚠️  Asegúrate de que RabbitMQ esté corriendo en {Host}", 
                    _configuration["RabbitMQ:Host"] ?? "localhost");
                throw;
            }
        }
    }
}
