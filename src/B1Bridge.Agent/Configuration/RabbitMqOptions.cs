namespace B1Bridge.Agent.Configuration;

public sealed class RabbitMqOptions
{
    public string? Uri { get; init; }

    public string Host { get; init; } = "localhost";
    public int Port { get; init; } = 5672;
    public string UserName { get; init; } = "guest";
    public string Password { get; init; } = "guest";
    public string Exchange { get; init; } = "b1bridge.integration";
    public string Queue { get; init; } = "b1bridge.integration.requests";
    public string RoutingKey { get; init; } = "integration.requested";
}
