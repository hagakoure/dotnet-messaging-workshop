using System.Text.Json;
using Api.Data;
using Microsoft.EntityFrameworkCore;
using Shared.Contracts;

namespace Api.Services;

public class OutboxService(
    IServiceScopeFactory scopeFactory,
    ILogger<OutboxService> logger,
    IHostApplicationLifetime lifetime)
    : BackgroundService
{
    private readonly TimeSpan _pollingInterval = TimeSpan.FromSeconds(2);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("OutboxService started. Polling interval: {Interval}", _pollingInterval);
    
        while (!stoppingToken.IsCancellationRequested)
        {
            logger.LogDebug("OutboxService: Starting iteration...");
        
            try
            {
                await ProcessOutboxMessagesAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                logger.LogInformation("OutboxService: Cancellation requested, stopping...");
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Critical error in OutboxService processing loop");
            }

            logger.LogDebug("OutboxService: Iteration completed. Waiting {Interval}...", _pollingInterval);
        
            try
            {
                await Task.Delay(_pollingInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        logger.LogInformation("OutboxService stopped");
    }

    private async Task ProcessOutboxMessagesAsync(CancellationToken stoppingToken)
    {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        logger.LogInformation("🔍 Получаем IMessagePublisher из DI...");
        var publisher = scope.ServiceProvider.GetRequiredService<IMessagePublisher>();
        logger.LogInformation("✅ IMessagePublisher получен. Тип: {PublisherType}", publisher.GetType().Name);

        var messages = await dbContext.OutboxMessages
            .Where(m => m.ProcessedAt == null)
            .OrderBy(m => m.OccurredOn)
            .Take(10)
            .ToListAsync(stoppingToken);

        logger.LogInformation("📦 Найдено необработанных сообщений: {Count}", messages.Count);

        if (messages.Count == 0) return;

        foreach (var message in messages)
        {
            stoppingToken.ThrowIfCancellationRequested();

            try
            {
                logger.LogInformation(
                    "🔄 Обрабатываем сообщение {MessageId}, Type: {MessageType}",
                    message.Id, message.MessageType);

                var messageType = Type.GetType(message.MessageType);
                if (messageType == null)
                {
                    logger.LogWarning("❌ Неизвестный тип: {MessageType}", message.MessageType);
                    message.ProcessedAt = DateTime.UtcNow;
                    continue;
                }

                var @event = System.Text.Json.JsonSerializer.Deserialize(message.Payload, messageType);
                if (@event == null)
                {
                    logger.LogWarning("❌ Не удалось десериализовать {MessageId}", message.Id);
                    continue;
                }

                logger.LogInformation("📤 Вызываем publisher.PublishAsync для {MessageId}", message.Id);
                await publisher.PublishAsync(@event, stoppingToken);
                logger.LogInformation("✅ publisher.PublishAsync завершился для {MessageId}", message.Id);

                message.ProcessedAt = DateTime.UtcNow;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "❌ Ошибка при публикации {MessageId}", message.Id);
            }
        }

        await dbContext.SaveChangesAsync(stoppingToken);
        logger.LogInformation("💾 SaveChangesAsync выполнен");
    }
}