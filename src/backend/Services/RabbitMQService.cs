using RabbitMQ.Client;
using System.Text;
using System.Text.Json;

namespace Backend.Services;

public class RabbitMQService : IDisposable
{
    private readonly IConnection? _connection;
    private readonly IChannel _channel;
    private readonly ILogger<RabbitMQService> _logger;
    private readonly bool _ownsConnection;
    private const string QueueName = "task_created";

    // Constructor for Aspire mode (IConnection injected)
    public RabbitMQService(IConnection connection, ILogger<RabbitMQService> logger)
    {
        _logger = logger;
        _connection = connection;
        _ownsConnection = false;

        try
        {
            _channel = _connection.CreateChannelAsync().GetAwaiter().GetResult();
            
            _channel.QueueDeclareAsync(
                queue: QueueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null
            ).GetAwaiter().GetResult();

            _logger.LogInformation("RabbitMQ connection established successfully (Aspire mode)");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize RabbitMQ channel");
            throw;
        }
    }

    // Constructor for Docker mode (manual connection)
    public RabbitMQService(IConfiguration configuration, ILogger<RabbitMQService> logger)
    {
        _logger = logger;
        _ownsConnection = true;
        
        var factory = new ConnectionFactory
        {
            HostName = configuration["RabbitMQ:Host"] ?? "rabbitmq",
            Port = 5672,
            UserName = "guest",
            Password = "guest"
        };

        try
        {
            _connection = factory.CreateConnectionAsync().GetAwaiter().GetResult();
            _channel = _connection.CreateChannelAsync().GetAwaiter().GetResult();
            
            _channel.QueueDeclareAsync(
                queue: QueueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null
            ).GetAwaiter().GetResult();

            _logger.LogInformation("RabbitMQ connection established successfully (Docker mode)");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to RabbitMQ");
            throw;
        }
    }

    public void PublishTaskCreated(Guid taskId, string title, string description)
    {
        try
        {
            var message = new
            {
                task_id = taskId.ToString(),
                title,
                description,
                timestamp = DateTime.UtcNow
            };

            var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message));

            var properties = new BasicProperties
            {
                Persistent = true
            };

            _channel.BasicPublishAsync(
                exchange: string.Empty,
                routingKey: QueueName,
                mandatory: false,
                basicProperties: properties,
                body: body
            ).GetAwaiter().GetResult();

            _logger.LogInformation("Published task created event for task {TaskId}", taskId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error publishing message to RabbitMQ");
        }
    }

    public void Dispose()
    {
        _channel?.CloseAsync().GetAwaiter().GetResult();
        
        // Only dispose connection if we own it (Docker mode)
        if (_ownsConnection)
        {
            _connection?.CloseAsync().GetAwaiter().GetResult();
        }
    }
}