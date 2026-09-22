using System.Text.Json;
using B1Bridge.Agent.Configuration;
using B1Bridge.Contracts.Messaging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace B1Bridge.Agent.Messaging;

public sealed class RabbitMqPublisher : IRabbitMqPublisher, IDisposable
{
    private readonly RabbitMqOptions _options;
    private readonly IConnectionFactory _connectionFactory;
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private readonly SemaphoreSlim _publishLock = new(1, 1);

    private IConnection? _connection;
    private IChannel? _channel;

    public RabbitMqPublisher(IOptions<RabbitMqOptions> options)
        : this(options, CreateConnectionFactory(options.Value))
    {
    }

    internal RabbitMqPublisher(
        IOptions<RabbitMqOptions> options,
        IConnectionFactory connectionFactory)
    {
        _options = options.Value;
        _connectionFactory = connectionFactory;
    }

    private static ConnectionFactory CreateConnectionFactory(RabbitMqOptions options)
    {
        var connectionFactory = new ConnectionFactory();

        if (!string.IsNullOrWhiteSpace(options.Uri))
        {
            connectionFactory.Uri = new Uri(options.Uri);
        }
        else
        {
            connectionFactory.HostName = options.Host;
            connectionFactory.Port = options.Port;
            connectionFactory.UserName = options.UserName;
            connectionFactory.Password = options.Password;
        }

        return connectionFactory;
    }

    public async Task PublishAsync(
        IntegrationRequest request,
        CancellationToken cancellationToken = default)
    {
        var channel = await GetChannelAsync(cancellationToken);

        var body = JsonSerializer.SerializeToUtf8Bytes(request);

        var properties = new BasicProperties
        {
            ContentType = "application/json",
            MessageId = request.OperationId,
            CorrelationId = request.CorrelationId,
            Type = "integration.request",
            Persistent = true
        };

        await _publishLock.WaitAsync(cancellationToken);

        try
        {
            await channel.BasicPublishAsync(
                exchange: _options.Exchange,
                routingKey: _options.RoutingKey,
                mandatory: true,
                basicProperties: properties,
                body: body,
                cancellationToken: cancellationToken);
        }
        finally
        {
            _publishLock.Release();
        }
    }

    private async Task<IChannel> GetChannelAsync(
        CancellationToken cancellationToken)
    {
        if (_channel is { IsOpen: true })
        {
            return _channel;
        }

        await _connectionLock.WaitAsync(cancellationToken);

        try
        {
            if (_channel is { IsOpen: true })
            {
                return _channel;
            }

            if (_connection is not { IsOpen: true })
            {
                _connection = await _connectionFactory
                    .CreateConnectionAsync(cancellationToken);
            }

            var channelOptions = new CreateChannelOptions(
                publisherConfirmationsEnabled: true,
                publisherConfirmationTrackingEnabled: true);

            var channel = await _connection.CreateChannelAsync(
                channelOptions,
                cancellationToken);

            try
            {
                await channel.ExchangeDeclareAsync(
                    exchange: _options.Exchange,
                    type: ExchangeType.Direct,
                    durable: true,
                    autoDelete: false,
                    arguments: null,
                    passive: false,
                    noWait: false,
                    cancellationToken: cancellationToken);

                await channel.QueueDeclareAsync(
                    queue: _options.Queue,
                    durable: true,
                    exclusive: false,
                    autoDelete: false,
                    arguments: null,
                    passive: false,
                    noWait: false,
                    cancellationToken: cancellationToken);

                await channel.QueueBindAsync(
                    queue: _options.Queue,
                    exchange: _options.Exchange,
                    routingKey: _options.RoutingKey,
                    arguments: null,
                    noWait: false,
                    cancellationToken: cancellationToken);

                // Share the channel only after the queue and binding are ready.
                _channel = channel;
                return channel;
            }
            catch
            {
                channel.Dispose();
                throw;
            }
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    public void Dispose()
    {
        _channel?.Dispose();
        _connection?.Dispose();
        _publishLock.Dispose();
        _connectionLock.Dispose();
    }
}
