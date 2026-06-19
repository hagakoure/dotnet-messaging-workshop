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

        var publisher = scope.ServiceProvider.GetRequiredService<IMessagePublisher>();

        var messages = await dbContext.OutboxMessages
            .Where(m => m.ProcessedAt == null)
            .OrderBy(m => m.OccurredOn)
            .Take(10)
            .ToListAsync(stoppingToken);
        
        if (messages.Count == 0) return;

        foreach (var message in messages)
        {
            stoppingToken.ThrowIfCancellationRequested();

            try
            {
                var messageType = Type.GetType(message.MessageType);
                if (messageType == null)
                {
                    message.ProcessedAt = DateTime.UtcNow;
                    continue;
                }

                var @event = System.Text.Json.JsonSerializer.Deserialize(message.Payload, messageType);
                if (@event == null)
                {
                    continue;
                }

                await publisher.PublishAsync(@event, stoppingToken);

                message.ProcessedAt = DateTime.UtcNow;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error when publishing {MessageId}", message.Id);
            }
        }

        await dbContext.SaveChangesAsync(stoppingToken);
    }
}