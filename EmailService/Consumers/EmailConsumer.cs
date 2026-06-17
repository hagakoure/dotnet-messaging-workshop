using MassTransit;
using Microsoft.EntityFrameworkCore;
using Shared.Contracts;
using EmailService.Data;
using EmailService.Data.Entities;

namespace EmailService.Consumers;

public class EmailConsumer(ILogger<EmailConsumer> logger, EmailDbContext dbContext) : IConsumer<EmailRequested>
{
    public async Task Consume(ConsumeContext<EmailRequested> context)
    {
        var message = context.Message;
        var uniqueEventId = context.MessageId ?? message.CorrelationId;

        // Проверка идемпотентности: проверяем, обрабатывали ли мы уже это событие
        var isAlreadyProcessed = await dbContext.ProcessedEvents
            .AnyAsync(e => e.EventId == uniqueEventId, context.CancellationToken);

        if (isAlreadyProcessed)
        {
            logger.LogWarning("Duplicate message detected and skipped. EventId: {EventId}", uniqueEventId);
            return; // Возвращаем управление, MassTransit считает сообщение успешно обработанным (Ack)
        }

        logger.LogInformation("Processing email to {To} [CorrelationId: {CorrelationId}]",
            message.To, message.CorrelationId);

        try
        {
            //Имитация бизнес-логики (отправка письма)
            await Task.Delay(500, context.CancellationToken);

            logger.LogInformation("Email sent successfully to {To} [CorrelationId: {CorrelationId}]",
                message.To, message.CorrelationId);

            // Пытаемся сохранить факт обработки
            var processedEvent = new ProcessedEvent
            {
                EventId = uniqueEventId,
                EventType = nameof(EmailRequested),
                ProcessedAt = DateTime.UtcNow
            };

            await dbContext.ProcessedEvents.AddAsync(processedEvent, context.CancellationToken);
            await dbContext.SaveChangesAsync(context.CancellationToken);

            logger.LogInformation("ProcessedEvent saved for EventId: {EventId}", uniqueEventId);
        }
        // Fallback — обработка race condition
        // Если между AnyAsync и SaveChangesAsync пришёл дубликат — обрабатываем как успех
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            logger.LogWarning(
                "Duplicate message detected via unique constraint (race condition handled). " +
                "EventId: {EventId}. Treating as successful (idempotency).",
                uniqueEventId);

            // Возвращаем управление без throw — MassTransit считает сообщение обработанным (Ack)
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process email [CorrelationId: {CorrelationId}]",
                message.CorrelationId);
            throw; // Пробрасываем для retry policy
        }
    }

    /// <summary>
    /// Проверяет, является ли исключение нарушением уникального ограничения PostgreSQL.
    /// Код ошибки 23505 (unique_violation) означает, что запись с таким ключом уже существует.
    /// </summary>
    private static bool IsUniqueConstraintViolation(DbUpdateException ex)
    {
        return ex.InnerException is Npgsql.PostgresException pgEx
               && pgEx.SqlState == "23505";
    }
}