using System.Collections.Concurrent;
using Shared.Contracts;
using Shared.DTOs;

namespace Api.Services;

/// <summary>
/// Временное хранилище статусов уведомлений (in-memory)
/// TODO: Для продакшена заменить на реализацию с БД (EF Core + Outbox)
/// </summary>
public interface INotificationStatusStore
{
    void Create(Guid correlationId, DateTime requestedAt);
    void UpdateStatus(Guid correlationId, NotificationStatus status, string? error = null);
    NotificationStatusResponse? GetStatus(Guid correlationId);
}

public class InMemoryNotificationStatusStore : INotificationStatusStore
{
    // ConcurrentDictionary — потоко-безопасный, подходит для демо
    // TODO: В продакшене: использовать распределённое кэширование (Redis) или БД
    private readonly ConcurrentDictionary<Guid, NotificationStatusResponse> _store = new();

    public void Create(Guid correlationId, DateTime requestedAt)
    {
        _store.TryAdd(correlationId, new NotificationStatusResponse(
            CorrelationId: correlationId,
            Status: NotificationStatus.Queued,
            CreatedAt: requestedAt,
            UpdatedAt: requestedAt
        ));
    }

    public void UpdateStatus(Guid correlationId, NotificationStatus status, string? error = null)
    {
        if (_store.TryGetValue(correlationId, out var existing))
        {
            var updated = existing with 
            { 
                Status = status, 
                UpdatedAt = DateTime.UtcNow,
                ErrorMessage = error
            };
            _store.TryUpdate(correlationId, updated, existing);
        }
    }

    public NotificationStatusResponse? GetStatus(Guid correlationId)
    {
        _store.TryGetValue(correlationId, out var status);
        return status;
    }
}