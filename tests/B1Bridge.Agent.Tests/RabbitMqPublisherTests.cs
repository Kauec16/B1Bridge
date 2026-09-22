using System.Reflection;
using System.Text.Json;
using B1Bridge.Agent.Configuration;
using B1Bridge.Agent.Messaging;
using B1Bridge.Contracts.Messaging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace B1Bridge.Agent.Tests;

public sealed class RabbitMqPublisherTests
{
    [Fact]
    public async Task PublishAsync_ShouldWaitForBindingBeforeConcurrentPublications()
    {
        var bindingStarted = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var finishBinding = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var channel = new ChannelStub(() =>
        {
            bindingStarted.SetResult(true);
            return finishBinding.Task;
        });
        using var publisher = CreatePublisher(channel);

        var firstPublication = publisher.PublishAsync(CreateRequest());
        await bindingStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Invoke directly so the call reaches its first incomplete await before asserting.
        var secondPublication = publisher.PublishAsync(CreateRequest());

        try
        {
            Assert.False(firstPublication.IsCompleted);
            Assert.False(secondPublication.IsCompleted);
            Assert.Equal(0, channel.PublishCount);
        }
        finally
        {
            finishBinding.TrySetResult(true);
            await Task.WhenAll(firstPublication, secondPublication)
                .WaitAsync(TimeSpan.FromSeconds(5));
        }

        Assert.Equal(1, channel.BindCount);
        Assert.Equal(2, channel.PublishCount);
    }

    [Fact]
    public async Task PublishAsync_ShouldDisposeFailedChannelAndInitializeAnother()
    {
        var bindingFailure = new InvalidOperationException("Queue binding failed.");
        var failedChannel = new ChannelStub(() => Task.FromException(bindingFailure));
        var healthyChannel = new ChannelStub(() => Task.CompletedTask);
        using var publisher = CreatePublisher(failedChannel, healthyChannel);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => publisher.PublishAsync(CreateRequest()));

        Assert.Same(bindingFailure, exception);
        Assert.Equal(1, failedChannel.DisposeCount);
        Assert.Equal(0, failedChannel.PublishCount);

        await publisher.PublishAsync(CreateRequest()).WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(1, failedChannel.BindCount);
        Assert.Equal(1, healthyChannel.BindCount);
        Assert.Equal(1, healthyChannel.PublishCount);
        Assert.Equal(0, healthyChannel.DisposeCount);
    }

    private static RabbitMqPublisher CreatePublisher(params ChannelStub[] channels)
    {
        var availableChannels = new Queue<ChannelStub>(channels);
        var connection = CreateProxy<IConnection>(method => method.Name switch
        {
            "get_IsOpen" => true,
            nameof(IConnection.CreateChannelAsync) =>
                Task.FromResult(availableChannels.Dequeue().Channel),
            nameof(IDisposable.Dispose) => null,
            _ => throw new NotSupportedException(method.Name)
        });
        var factory = CreateProxy<IConnectionFactory>(method => method.Name switch
        {
            nameof(IConnectionFactory.CreateConnectionAsync) => Task.FromResult(connection),
            _ => throw new NotSupportedException(method.Name)
        });

        return new RabbitMqPublisher(Options.Create(new RabbitMqOptions()), factory);
    }

    private static IntegrationRequest CreateRequest() => new()
    {
        OperationId = Guid.NewGuid().ToString("N"),
        Payload = JsonSerializer.SerializeToElement(new { item = "A123" })
    };

    private static T CreateProxy<T>(Func<MethodInfo, object?> handler) where T : class
    {
        var proxy = DispatchProxy.Create<T, StubProxy>();
        ((StubProxy)(object)proxy).Handler = handler;
        return proxy;
    }

    public class StubProxy : DispatchProxy
    {
        public Func<MethodInfo, object?> Handler { get; set; } = null!;

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
            Handler(targetMethod ?? throw new InvalidOperationException("Missing proxy method."));
    }

    private sealed class ChannelStub
    {
        public ChannelStub(Func<Task> bindAsync)
        {
            Channel = CreateProxy<IChannel>(method =>
            {
                switch (method.Name)
                {
                    case "get_IsOpen":
                        return DisposeCount == 0;
                    case nameof(IChannel.ExchangeDeclareAsync):
                        return Task.CompletedTask;
                    case nameof(IChannel.QueueDeclareAsync):
                        return Task.FromResult(new QueueDeclareOk("requests", 0, 0));
                    case nameof(IChannel.QueueBindAsync):
                        BindCount++;
                        return bindAsync();
                    case nameof(IChannel.BasicPublishAsync):
                        PublishCount++;
                        return ValueTask.CompletedTask;
                    case nameof(IDisposable.Dispose):
                        DisposeCount++;
                        return null;
                    default:
                        throw new NotSupportedException(method.Name);
                }
            });
        }

        public IChannel Channel { get; }
        public int BindCount { get; private set; }
        public int PublishCount { get; private set; }
        public int DisposeCount { get; private set; }
    }
}
