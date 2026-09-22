using B1Bridge.Contracts.Messaging;

namespace B1Bridge.Agent.Messaging
{
    public interface IRabbitMqPublisher
    {
        Task PublishAsync(
            IntegrationRequest request,
            CancellationToken cancellationToken = default);
    }
}
