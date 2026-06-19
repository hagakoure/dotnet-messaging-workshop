using MassTransit;
using Microsoft.Extensions.Logging;
using Shared.Contracts;

namespace Api.Services;

public class KafkaMessagePublisher(
    ITopicProducer<EmailRequested> producer,
    ILogger<KafkaMessagePublisher> logger)
    : IMessagePublisher
{
    public async Task PublishAsync(object message, CancellationToken cancellationToken)
    {
        if (message is EmailRequested emailEvent)
        {
            await producer.Produce(emailEvent, cancellationToken);
            
            return;
        }
        
        logger.LogError(
            "KafkaMessagePublisher: Unsupported message type: {MessageType}", 
            message.GetType().Name);
        
        throw new NotSupportedException(
            $"Type {message.GetType().Name} is not supported by Kafka publisher.");
    }
}