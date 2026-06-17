using Shared.Contracts;

namespace Api.Services;

public interface INotificationStatusStore
{
    Task CreateAsync(Guid correlationId, EmailRequested request, CancellationToken cancellationToken);

    Task UpdateStatusAsync(Guid correlationId, NotificationStatus status, CancellationToken cancellationToken,
        string? errorMessage = null);

    Task<NotificationStatusResponse?> GetStatusAsync(Guid correlationId, CancellationToken cancellationToken);
}