namespace Shared.Contracts;

/// <summary>
/// Статус обработки уведомления
/// </summary>
public enum NotificationStatus
{
    /// <summary>Принято в очередь, ожидает обработки</summary>
    Queued,

    /// <summary>В процессе отправки</summary>
    Processing,

    /// <summary>Успешно отправлено</summary>
    Sent,

    /// <summary>Ошибка при отправке</summary>
    Failed
}