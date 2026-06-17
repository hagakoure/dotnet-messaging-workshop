namespace Shared.Contracts;

/// <summary>
/// DTO для ответа клиенту при запросе статуса уведомления
/// Используется record для неизменяемости и семантики значений
/// </summary>
public record NotificationStatusResponse(
    Guid CorrelationId,
    NotificationStatus Status,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    string? ErrorMessage = null
);