using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Shared.Contracts;
using Api.Data;
using Api.Data.Entities;

namespace Api.Services;

public class EfNotificationStatusStore(AppDbContext dbContext) : INotificationStatusStore
{
    public async Task CreateAsync(Guid correlationId, EmailRequested request,
        CancellationToken cancellationToken = default)
    {
        // проверка идемпотентность
        var existingStatus = await dbContext.NotificationStatuses
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.CorrelationId == correlationId, cancellationToken);

        if (existingStatus != null)
        {
            // Запись уже существует
            return;
        }

        // Создаём сущность статуса
        var statusEntity = new NotificationStatusEntity
        {
            CorrelationId = correlationId,
            Status = NotificationStatus.Queued,
            CreatedAt = request.RequestedAt,
            UpdatedAt = request.RequestedAt
        };

        // Создаём сообщение для Outbox
        var outboxMessage = new OutboxMessage
        {
            MessageType = typeof(EmailRequested).AssemblyQualifiedName!,
            Payload = JsonSerializer.Serialize(request),
            OccurredOn = DateTime.UtcNow
        };

        dbContext.NotificationStatuses.Add(statusEntity);
        dbContext.OutboxMessages.Add(outboxMessage);

        try
        {
            // Атомарное сохранение
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        // Fallback — обработка race condition
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            // Между проверкой и вставкой пришёл дубликат (идемпотентность)
            // Детализируем сущности, чтобы избежать проблем с DbContext
            dbContext.Entry(statusEntity).State = EntityState.Detached;
            dbContext.Entry(outboxMessage).State = EntityState.Detached;
        }
    }

    /// <summary>
    /// Проверяет, является ли исключение нарушением уникального ограничения PostgreSQL.
    /// Код 23505 (unique_violation) означает, что запись с таким ключом уже существует.
    /// </summary>
    private static bool IsUniqueConstraintViolation(DbUpdateException ex)
    {
        return ex.InnerException is Npgsql.PostgresException pgEx
               && pgEx.SqlState == "23505";
    }

    public async Task UpdateStatusAsync(Guid correlationId, NotificationStatus status,
        CancellationToken cancellationToken, string? errorMessage = null)
    {
        var entity = await dbContext.NotificationStatuses
            .FirstOrDefaultAsync(e => e.CorrelationId == correlationId, cancellationToken: cancellationToken);

        if (entity != null)
        {
            entity.Status = status;
            entity.UpdatedAt = DateTime.UtcNow;
            entity.ErrorMessage = errorMessage;
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<NotificationStatusResponse?> GetStatusAsync(Guid correlationId,
        CancellationToken cancellationToken)
    {
        var entity = await dbContext.NotificationStatuses
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.CorrelationId == correlationId, cancellationToken: cancellationToken);

        if (entity == null)
        {
            return null;
        }

        return new NotificationStatusResponse(
            CorrelationId: entity.CorrelationId,
            Status: entity.Status,
            CreatedAt: entity.CreatedAt,
            UpdatedAt: entity.UpdatedAt,
            ErrorMessage: entity.ErrorMessage
        );
    }
}