using MassTransit;
using Shared.Contracts;

namespace EmailService.Consumers;

/// <summary>
/// Обрабатывает события отправки писем
/// </summary>
public class EmailConsumer : IConsumer<EmailRequested>
{
    private readonly ILogger<EmailConsumer> _logger;

    public EmailConsumer(ILogger<EmailConsumer> logger) => 
        _logger = logger;

    public async Task Consume(ConsumeContext<EmailRequested> context)
    {
        var message = context.Message;
        
        _logger.LogInformation(" Processing email to {To} [CorrelationId: {CorrelationId}]", 
            message.To, message.CorrelationId);
        
        // TODO: statusStore.UpdateStatus(message.CorrelationId, NotificationStatus.Processing);
        
        await Task.Delay(500, context.CancellationToken);
        
        //  TODO: Имитация успешной отправки
        _logger.LogInformation(" Email sent to {To} [CorrelationId: {CorrelationId}]", 
            message.To, message.CorrelationId);
        
        // TODO: statusStore.UpdateStatus(message.CorrelationId, NotificationStatus.Sent);
    }
}