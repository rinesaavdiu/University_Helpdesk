namespace UniversityHelpdesk.NotificationWorker;

public static class WorkerServiceCollectionExtensions
{
    public static IServiceCollection AddNotificationWorker(this IServiceCollection services)
    {
        services.AddOptions<RabbitMqOptions>().BindConfiguration(RabbitMqOptions.SectionName);
        services.AddOptions<SmtpOptions>().BindConfiguration(SmtpOptions.SectionName);
        services.AddOptions<DeliveryOptions>().BindConfiguration(DeliveryOptions.SectionName);
        services.AddOptions<HealthOptions>().BindConfiguration(HealthOptions.SectionName);
        services.AddSingleton<IEmailSender, SmtpEmailSender>();
        services.AddSingleton<NotificationConsumer>();
        services.AddHostedService(provider => provider.GetRequiredService<NotificationConsumer>());
        services.AddHostedService<HealthFileWriter>();
        return services;
    }
}
