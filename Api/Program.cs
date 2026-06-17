using Api.Options;
using Api.Services;
using MassTransit;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using Shared.Contracts;

var builder = WebApplication.CreateBuilder(args);

// Регистрация MassTransit с RabbitMQ
builder.Services.AddOptions<RabbitMqOptions>()
    .Bind(builder.Configuration.GetSection(RabbitMqOptions.Section))
    .ValidateDataAnnotations()
    .ValidateOnStart();

// Использование в MassTransit
builder.Services.AddMassTransit(configurator =>
{
    configurator.SetSnakeCaseEndpointNameFormatter();
    
    configurator.UsingRabbitMq((context, cfg) =>
    {
        var rabbitOptions = context.GetRequiredService<IOptions<RabbitMqOptions>>().Value;
        
        cfg.Host(rabbitOptions.Host, "/", h =>
        {
            h.Username(rabbitOptions.UserName);
            h.Password(rabbitOptions.Password);
        });
    });
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton<INotificationStatusStore, InMemoryNotificationStatusStore>();

var app = builder.Build();

//Middleware
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Endpoint: POST /notifications/email
app.MapPost("/notifications/email", async (
        EmailRequested request,
        IPublishEndpoint endpoint,
        INotificationStatusStore statusStore,
        ILogger<Program> logger) =>
    {
        var eventWithCorrelation = request with
        {
            CorrelationId = request.CorrelationId != Guid.Empty
                ? request.CorrelationId
                : Guid.NewGuid()
        };

        // Сохраняем начальный статус
        statusStore.Create(eventWithCorrelation.CorrelationId, eventWithCorrelation.RequestedAt);

        // Публикуем событие
        await endpoint.Publish(eventWithCorrelation);

        logger.LogInformation(" Published EmailRequested: {CorrelationId}",
            eventWithCorrelation.CorrelationId);

        // Возвращаем 202 с Location-заголовком
        var statusUrl = $"/notifications/status/{eventWithCorrelation.CorrelationId}";
        return Results.Accepted(
            statusUrl, //  Заголовок Location
            new
            {
                correlationId = eventWithCorrelation.CorrelationId,
                status = "queued",
                statusUrl //  Удобно для клиента
            });
    })
    .WithName("SendEmailNotification")
    .WithOpenApi(operation =>
    {
        //  Добавляем описание ответа 202 в Swagger
        operation.Responses["202"] = new OpenApiResponse
        {
            Description = "Request accepted for processing",
            Content = new Dictionary<string, OpenApiMediaType>
            {
                ["application/json"] = new OpenApiMediaType
                {
                    Schema = new OpenApiSchema
                    {
                        Type = "object",
                        Properties = new Dictionary<string, OpenApiSchema>
                        {
                            ["correlationId"] = new OpenApiSchema { Type = "string", Format = "uuid" },
                            ["status"] = new OpenApiSchema { Type = "string" },
                            ["statusUrl"] = new OpenApiSchema { Type = "string" }
                        }
                    }
                }
            }
        };
        return operation;
    });

app.MapGet("/notifications/status/{correlationId:guid}", (
        Guid correlationId,
        INotificationStatusStore statusStore,
        ILogger<Program> logger) =>
    {
        var status = statusStore.GetStatus(correlationId);

        if (status is null)
        {
            logger.LogWarning("Status not found for CorrelationId: {CorrelationId}", correlationId);
            return Results.NotFound(new { error = "Notification not found", correlationId });
        }

        logger.LogDebug("Returning status for {CorrelationId}: {Status}",
            correlationId, status.Status);

        return Results.Ok(status);
    })
    .WithName("GetNotificationStatus")
    .WithOpenApi(operation =>
    {
        operation.Summary = "Get status of a notification request";
        operation.Description = "Poll this endpoint to check if your email has been sent";
        return operation;
    });

app.Run();