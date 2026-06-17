using MassTransit;
using EmailService.Consumers;
using EmailService.Options;
using Microsoft.Extensions.Options;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddOptions<RabbitMqOptions>()
    .Bind(builder.Configuration.GetSection(RabbitMqOptions.Section))
    .ValidateDataAnnotations()
    .ValidateOnStart();

// Регистрация MassTransit с конфигурацией
builder.Services.AddMassTransit(configurator =>
{
    configurator.AddConsumer<EmailConsumer>();
    configurator.SetSnakeCaseEndpointNameFormatter();
    
    configurator.UsingRabbitMq((context, cfg) =>
    {
        var rabbitOptions = context.GetRequiredService<IOptions<RabbitMqOptions>>().Value;
        
        cfg.Host(rabbitOptions.Host, "/", h =>
        {
            h.Username(rabbitOptions.UserName);
            h.Password(rabbitOptions.Password);
        });
        
        cfg.ReceiveEndpoint("email-queue", e =>
        {
            e.ConfigureConsumer<EmailConsumer>(context);
            e.PrefetchCount = 10;
            e.UseMessageRetry(r => r.Interval(3, TimeSpan.FromSeconds(5)));
        });
    });
});

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(LogLevel.Information);

var host = builder.Build();
host.Run();