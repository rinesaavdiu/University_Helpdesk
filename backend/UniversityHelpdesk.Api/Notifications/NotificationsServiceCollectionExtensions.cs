namespace UniversityHelpdesk.Api.Notifications;

public static class NotificationsServiceCollectionExtensions
{
    public static IServiceCollection AddHelpdeskNotifications(this IServiceCollection services)
    {
        services.AddOptions<RabbitMqOptions>().BindConfiguration(RabbitMqOptions.SectionName);
        services.AddOptions<OutboxPublisherOptions>().BindConfiguration(OutboxPublisherOptions.SectionName);
        services.AddSingleton<IEventPublisher, RabbitMqEventPublisher>();
        services.AddHostedService<OutboxPublisher>();
        return services;
    }
}
