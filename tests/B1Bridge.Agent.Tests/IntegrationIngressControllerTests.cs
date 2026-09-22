using System.Text;
using B1Bridge.Agent.Controllers;
using B1Bridge.Agent.Messaging;
using B1Bridge.Contracts.Messaging;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace B1Bridge.Agent.Tests;

public sealed class IntegrationIngressControllerTests
{
    [Fact]
    public async Task Receive_ShouldPublishRequestAndReturnAccepted()
    {
        var publisher = new InMemoryPublisher();
        var controller = CreateController(
            publisher,
            method: "POST",
            path: "/CriacaoItem/A123",
            queryString: "?source=integration",
            body: "{\"name\":\"Produto teste\"}",
            correlationId: "corr-456",
            idempotencyKey: "external-request-789");

        var result = await controller.Receive(
            "CriacaoItem/A123",
            CancellationToken.None);

        var accepted = Assert.IsType<AcceptedResult>(result);
        Assert.Equal(StatusCodes.Status202Accepted, accepted.StatusCode);

        var request = Assert.Single(publisher.PublishedRequests);

        Assert.Equal("POST", request.Route.Method);
        Assert.Equal("/CriacaoItem/A123", request.Route.Path);
        Assert.Equal("integration", request.Route.Query["source"]);
        Assert.Equal("corr-456", request.CorrelationId);
        Assert.Equal("external-request-789", request.IdempotencyKey);
        Assert.False(string.IsNullOrWhiteSpace(request.OperationId));
        Assert.Equal("Produto teste", request.Payload.GetProperty("name").GetString());
    }

    [Fact]
    public async Task Receive_ShouldCreateIdentifiersWhenHeadersAreMissing()
    {
        var publisher = new InMemoryPublisher();
        var controller = CreateController(
            publisher,
            method: "POST",
            path: "/CriacaoItem/A123",
            body: "{}",
            correlationId: null,
            idempotencyKey: null);

        await controller.Receive("CriacaoItem/A123", CancellationToken.None);

        var request = Assert.Single(publisher.PublishedRequests);

        Assert.False(string.IsNullOrWhiteSpace(request.OperationId));
        Assert.False(string.IsNullOrWhiteSpace(request.CorrelationId));
        Assert.Equal(request.OperationId, request.IdempotencyKey);
    }

    [Fact]
    public async Task Receive_ShouldReturnBadRequestWhenPayloadIsInvalidJson()
    {
        var publisher = new InMemoryPublisher();
        var controller = CreateController(
            publisher,
            method: "POST",
            path: "/CriacaoItem/A123",
            body: "{invalid-json");

        var result = await controller.Receive(
            "CriacaoItem/A123",
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Empty(publisher.PublishedRequests);
    }

    private static IntegrationIngressController CreateController(
        InMemoryPublisher publisher,
        string method,
        string path,
        string body,
        string? queryString = null,
        string? correlationId = null,
        string? idempotencyKey = null)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = method;
        httpContext.Request.Path = path;
        httpContext.Request.QueryString = new QueryString(queryString);
        httpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));

        if (correlationId is not null)
        {
            httpContext.Request.Headers["X-Correlation-Id"] = correlationId;
        }

        if (idempotencyKey is not null)
        {
            httpContext.Request.Headers["Idempotency-Key"] = idempotencyKey;
        }

        return new IntegrationIngressController(publisher)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            }
        };
    }

    private sealed class InMemoryPublisher : IRabbitMqPublisher
    {
        public List<IntegrationRequest> PublishedRequests { get; } = [];

        public Task PublishAsync(
            IntegrationRequest request,
            CancellationToken cancellationToken = default)
        {
            PublishedRequests.Add(request);
            return Task.CompletedTask;
        }
    }
}
