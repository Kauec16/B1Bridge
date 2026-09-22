using System.Text.Json;

namespace B1Bridge.Contracts.Messaging;

public sealed class IntegrationRequest
{
    public RouteInfo Route { get; init; } = new();

    public string OperationId { get; init; } = string.Empty;

    public string CorrelationId { get; init; } = string.Empty;

    public string IdempotencyKey { get; init; } = string.Empty;

    public string? OperationType { get; init; }

    public JsonElement Payload { get; init; }
}
