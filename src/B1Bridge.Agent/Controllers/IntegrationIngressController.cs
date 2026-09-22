using System.Text.Json;
using B1Bridge.Agent.Messaging;
using B1Bridge.Contracts.Messaging;
using Microsoft.AspNetCore.Mvc;

namespace B1Bridge.Agent.Controllers;

[ApiController]
[Route("")]
public sealed class IntegrationIngressController : ControllerBase
{
    private readonly IRabbitMqPublisher _publisher;

    public IntegrationIngressController(IRabbitMqPublisher publisher)
    {
        _publisher = publisher;
    }

    [AcceptVerbs("GET", "POST", "PUT", "PATCH", "DELETE")]
    [Route("{**path}")]
    public async Task<IActionResult> Receive(
        string? path,
        CancellationToken cancellationToken)
    {
        JsonElement payload;

        try
        {
            payload = await ReadPayloadAsync(cancellationToken);
        }
        catch (JsonException)
        {
            return BadRequest(new
            {
                error = "invalid_json",
                message = "O corpo da requisição precisa ser um JSON válido."
            });
        }

        var operationId = Guid.NewGuid().ToString("N");

        var correlationId =
            Request.Headers["X-Correlation-Id"].FirstOrDefault()
            ?? Guid.NewGuid().ToString("N");

        var idempotencyKey =
            Request.Headers["Idempotency-Key"].FirstOrDefault()
            ?? operationId;

        var request = new IntegrationRequest
        {
            Route = new RouteInfo
            {
                Method = Request.Method,
                Path = Request.Path.Value ?? "/",
                Query = Request.Query.ToDictionary(
                    item => item.Key,
                    item => (string?)item.Value.ToString())
            },
            OperationId = operationId,
            CorrelationId = correlationId,
            IdempotencyKey = idempotencyKey,
            Payload = payload
        };

        await _publisher.PublishAsync(request, cancellationToken);

        return Accepted(new
        {
            request.OperationId,
            request.CorrelationId,
            Status = "Accepted"
        });
    }

    private async Task<JsonElement> ReadPayloadAsync(
        CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(Request.Body);
        var rawBody = await reader.ReadToEndAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(rawBody))
        {
            using var emptyDocument = JsonDocument.Parse("{}");
            return emptyDocument.RootElement.Clone();
        }

        using var document = JsonDocument.Parse(rawBody);
        return document.RootElement.Clone();
    }
}