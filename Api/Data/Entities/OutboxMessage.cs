namespace Api.Data.Entities;

public class OutboxMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string MessageType { get; set; } = null!;
    public string Payload { get; set; } = null!; // JSON
    public DateTime OccurredOn { get; set; } = DateTime.UtcNow;
    public DateTime? ProcessedAt { get; set; }
    public int RetryCount { get; set; }
    public DateTime? DeadLetteredAt { get; set; }
}