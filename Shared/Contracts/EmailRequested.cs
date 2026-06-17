namespace Shared.Contracts;

/// <summary>
/// Событие: запрошена отправка письма
/// </summary>
/// <param name="CorrelationId">ID для сквозной трассировки</param>
/// <param name="To">Email получателя</param>
/// <param name="Subject">Тема письма</param>
/// <param name="Body">Тело письма</param>
/// <param name="RequestedAt">Время запроса</param>
public record EmailRequested(
    Guid CorrelationId,
    string To,
    string Subject,
    string Body,
    DateTime RequestedAt
);