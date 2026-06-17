using Api.Data;
using Api.Filters;
using Api.HealthChecks;
using Api.Options;
using Api.Services;
using Api.Validators;
using FluentValidation;
using FluentValidation.AspNetCore;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Shared.Contracts;

var builder = WebApplication.CreateBuilder(args);

// Регистрация опций
builder.Services.AddOptions<MessageBrokerOptions>()
    .Bind(builder.Configuration.GetSection(MessageBrokerOptions.Section))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<RabbitMqOptions>()
    .Bind(builder.Configuration.GetSection(RabbitMqOptions.Section))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<KafkaOptions>()
    .Bind(builder.Configuration.GetSection(KafkaOptions.Section))
    .ValidateDataAnnotations()
    .ValidateOnStart();

// Регистрация валидаторов и фильтра
builder.Services.AddValidatorsFromAssemblyContaining<EmailRequestedValidator>();

// MassTransit с поддержкой переключения брокеров
var brokerType = builder.Configuration.GetValue<string>("MessageBroker:Type")?.ToLower();
builder.Services.AddMassTransit(configurator =>
{
    configurator.SetSnakeCaseEndpointNameFormatter();
    
    if (brokerType == "kafka")
    {
        var kafkaOptions = builder.Configuration.GetSection(KafkaOptions.Section).Get<KafkaOptions>()!;
        
        configurator.AddRider(rider =>
        {
            rider.AddProducer<EmailRequested>("email-events");
            
            rider.UsingKafka((context, kafkaConfigurator) =>
            {
                kafkaConfigurator.Host(kafkaOptions.BootstrapServers);
            });
        });
        
        configurator.UsingInMemory(); // Fallback для IBus
    }
    else // rabbitmq
    {
        var rabbitOptions = builder.Configuration.GetSection(RabbitMqOptions.Section).Get<RabbitMqOptions>()!;
        
        configurator.UsingRabbitMq((_, cfg) =>
        {
            cfg.Host(rabbitOptions.Host, "/", h =>
            {
                h.Username(rabbitOptions.UserName);
                h.Password(rabbitOptions.Password);
            });
        });
    }
});

if (brokerType == "kafka")
{
    builder.Services.AddScoped<IMessagePublisher, KafkaMessagePublisher>();
}
else // rabbitmq
{
    builder.Services.AddScoped<IMessagePublisher, RabbitMqMessagePublisher>();
}

// База данных
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

// Сервисы
builder.Services.AddScoped<INotificationStatusStore, EfNotificationStatusStore>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddHostedService<OutboxService>();

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// OpenTelemetry
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService(
        serviceName: "notification-api",
        serviceVersion: "1.0.0"))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation(options =>
            options.Filter = ctx => !ctx.Request.Path.StartsWithSegments("/health"))
        .AddHttpClientInstrumentation()
        .AddEntityFrameworkCoreInstrumentation(options => options.SetDbStatementForText = true)
        .AddSource("MassTransit"))
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddMeter("MassTransit"))
    .WithLogging(logging => { })
    .UseOtlpExporter();

// Health Checks для Kubernetes
builder.Services.AddHealthChecks()
    .AddNpgSql(
        connectionString: builder.Configuration.GetConnectionString("Default")!,
        name: "postgres",
        failureStatus: Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Unhealthy,
        tags: new[] { "ready", "live" })
    .AddCheck<RabbitMqHealthCheck>(
        name: "rabbitmq",
        failureStatus: Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Degraded,
        tags: new[] { "ready" })
    .AddCheck("outbox-service", () =>
            Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy("OutboxService is running"),
        tags: new[] { "live" });

// RabbitMqHealthCheck
builder.Services.AddSingleton(new Api.HealthChecks.RabbitMqHealthCheck(
    host: builder.Configuration["RabbitMQ:Host"]!,
    port: 5672));

var app = builder.Build();

// Middleware
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Endpoints
app.MapPost("/notifications/email", async (
        EmailRequested request,
        INotificationService notificationService,
        ILogger<Program> logger,
        CancellationToken cancellationToken) =>
    {
        var correlationId = await notificationService.RequestEmailNotificationAsync(request, cancellationToken);
        var statusUrl = $"/notifications/status/{correlationId}";
        logger.LogInformation("Event saved to Outbox with CorrelationId: {CorrelationId}", correlationId);
        return Results.Accepted(statusUrl, new { correlationId, status = "queued", statusUrl });
    })
    .WithName("SendEmailNotification")
    .WithOpenApi()
    .AddEndpointFilter<FluentValidationEndpointFilter<EmailRequested>>();

app.MapGet("/notifications/status/{correlationId:guid}", async (
        Guid correlationId,
        INotificationService notificationService,
        CancellationToken cancellationToken) =>
    {
        var status = await notificationService.GetStatusAsync(correlationId, cancellationToken);
        return status is not null
            ? Results.Ok(status)
            : Results.NotFound(new { error = "Notification not found", correlationId });
    })
    .WithName("GetNotificationStatus")
    .WithOpenApi();

// Health endpoints
app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("live")
});

app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var response = new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description,
                duration = e.Value.Duration.TotalMilliseconds
            })
        };
        await context.Response.WriteAsJsonAsync(response);
    }
});

app.MapHealthChecks("/health");

app.Run();

public partial class Program
{
}