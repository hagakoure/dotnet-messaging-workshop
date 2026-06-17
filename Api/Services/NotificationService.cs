using Shared.Contracts;

namespace Api.Services;

public class NotificationService(INotificationStatusStore statusStore) : INotificationService
{
    public async Task<Guid> RequestEmailNotificationAsync(EmailRequested request, CancellationToken cancellationToken)
    {
        // 1. Бизнес-логика: определение CorrelationId
        var correlationId = request.CorrelationId != Guid.Empty ? request.CorrelationId : Guid.NewGuid();
        var eventWithCorrelation = request with { CorrelationId = correlationId };

        // 2. Делегирование инфраструктурной задачи (Outbox + DB)
        await statusStore.CreateAsync(correlationId, eventWithCorrelation, cancellationToken);

        return correlationId;
    }

    public async Task<NotificationStatusResponse?> GetStatusAsync(Guid correlationId,
        CancellationToken cancellationToken)
    {
        return await statusStore.GetStatusAsync(correlationId, cancellationToken);
    }
}