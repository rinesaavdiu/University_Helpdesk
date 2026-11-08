using UniversityHelpdesk.NotificationWorker;

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(options =>
{
    options.IncludeScopes = true;
    options.UseUtcTimestamp = true;
    options.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";
});

builder.Services.AddOptions<RabbitMqOptions>().BindConfiguration(RabbitMqOptions.SectionName);
builder.Services.AddOptions<SmtpOptions>().BindConfiguration(SmtpOptions.SectionName);
builder.Services.AddOptions<DeliveryOptions>().BindConfiguration(DeliveryOptions.SectionName);
builder.Services.AddSingleton<IEmailSender, SmtpEmailSender>();
builder.Services.AddHostedService<NotificationConsumer>();

var host = builder.Build();
host.Run();
