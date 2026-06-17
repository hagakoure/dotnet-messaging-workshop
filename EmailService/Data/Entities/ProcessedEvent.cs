namespace EmailService.Data.Entities;

/// <summary>
/// Сущность для хранения идентификаторов обработанных сообщений.
/// Используется для обеспечения идемпотентности консьюмера.
/// </summary>
public class ProcessedEvent
{
    public Guid EventId { get; set; }
    public string EventType { get; set; } = null!;
    public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
}