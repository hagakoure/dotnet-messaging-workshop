using Shared.Contracts;

namespace Api.Data.Entities;

public class NotificationStatusEntity
{
    public Guid CorrelationId { get; set; }
    public NotificationStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string? ErrorMessage { get; set; }
}