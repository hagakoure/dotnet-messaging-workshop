using MassTransit;

namespace Api.Services;

public class RabbitMqMessagePublisher(IPublishEndpoint publishEndpoint) : IMessagePublisher
{
    public Task PublishAsync(object message, CancellationToken cancellationToken)
    {
        return publishEndpoint.Publish(message, message.GetType(), cancellationToken);
    }
}