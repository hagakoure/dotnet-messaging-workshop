using Api.Filters;
using Confluent.Kafka;
using EmailService.Consumers;
using EmailService.Data;
using EmailService.Options;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Shared.Contracts;

var builder = Host.CreateApplicationBuilder(args);

// 1. Регистрация опций
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

// 2. База данных
builder.Services.AddDbContext<EmailDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

// 3. MassTransit с поддержкой переключения брокеров
builder.Services.AddMassTransit(configurator =>
{
    configurator.SetSnakeCaseEndpointNameFormatter();

    var brokerType = builder.Configuration.GetValue<string>("MessageBroker:Type")?.ToLower();

    if (brokerType == "kafka")
    {
        var kafkaOptions = builder.Configuration.GetSection(KafkaOptions.Section).Get<KafkaOptions>()!;

        configurator.AddRider(rider =>
        {
            rider.AddConsumer<EmailConsumer>();

            rider.UsingKafka((context, kafkaConfigurator) =>
            {
                kafkaConfigurator.Host(kafkaOptions.BootstrapServers);

                kafkaConfigurator.TopicEndpoint<EmailRequested>(
                    topicName: "email-events",
                    groupId: kafkaOptions.GroupId,
                    configure: e =>
                    {
                        e.ConfigureConsumer<EmailConsumer>(context);
                        e.AutoOffsetReset = AutoOffsetReset.Earliest;
                        e.CheckpointInterval = TimeSpan.FromSeconds(5);
                    });
            });
        });

        configurator.UsingInMemory();
    }
    else // rabbitmq (по умолчанию)
    {
        configurator.AddConsumer<EmailConsumer>();

        var rabbitOptions = builder.Configuration.GetSection(RabbitMqOptions.Section).Get<RabbitMqOptions>()!;

        configurator.UsingRabbitMq((context, cfg) =>
        {
            cfg.Host(rabbitOptions.Host, "/", h =>
            {
                h.Username(rabbitOptions.UserName);
                h.Password(rabbitOptions.Password);
            });

            cfg.UseMessageRetry(r => r.Exponential(
                3, // retryLimit (не retryCount!)
                TimeSpan.FromSeconds(2), // minInterval
                TimeSpan.FromSeconds(30), // maxInterval
                TimeSpan.FromSeconds(2))); // intervalDelta

            cfg.ReceiveEndpoint("email-queue", e =>
            {
                e.ConfigureConsumer<EmailConsumer>(context);
                e.PrefetchCount = 10;

                // MassTransit автоматически создаёт error queue: email-queue_error
                // и перемещает туда сообщения после исчерпания retry
            });
        });
    }
});

// 4. OpenTelemetry
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService(
        serviceName: "notification-consumer",
        serviceVersion: "1.0.0"))
    .WithTracing(tracing => tracing
        .AddHttpClientInstrumentation()
        .AddEntityFrameworkCoreInstrumentation(options => options.SetDbStatementForText = true)
        .AddSource("MassTransit"))
    .WithMetrics(metrics => metrics
        .AddHttpClientInstrumentation()
        .AddMeter("MassTransit"))
    .WithLogging(logging => { })
    .UseOtlpExporter();

// 5. Логирование
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(LogLevel.Information);

var host = builder.Build();

host.Run();

public partial class Program
{
}