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
            logger.LogInformation(
                "KafkaMessagePublisher: Publishing EmailRequested to topic 'email-events'. CorrelationId: {CorrelationId}", 
                emailEvent.CorrelationId);
            
            await producer.Produce(emailEvent, cancellationToken);
            
            logger.LogInformation(
                "KafkaMessagePublisher: Successfully published to Kafka. CorrelationId: {CorrelationId}", 
                emailEvent.CorrelationId);
            return;
        }
        
        logger.LogError(
            "KafkaMessagePublisher: Unsupported message type: {MessageType}", 
            message.GetType().Name);
        
        throw new NotSupportedException(
            $"Type {message.GetType().Name} is not supported by Kafka publisher.");
    }
}