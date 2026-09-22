using System.Text.Json;

namespace B1Bridge.Contracts.Messaging;

public sealed class IntegrationResult
{
    public string OperationId { get; init; } = string.Empty;

    public string CorrelationId { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public JsonElement? Payload { get; init; }

    public string? ErrorCode { get; init; }

    public string? ErrorMessage { get; init; }
}
