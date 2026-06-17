using Shared.Contracts;

namespace Api.Services;

public interface INotificationService
{
    Task<Guid> RequestEmailNotificationAsync(EmailRequested request, CancellationToken cancellationToken);
    Task<NotificationStatusResponse?> GetStatusAsync(Guid correlationId, CancellationToken cancellationToken);
}