using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniversityHelpdesk.Api.Domain;

namespace UniversityHelpdesk.Api.Persistence.Configurations;

public class PendingNotificationConfiguration : IEntityTypeConfiguration<PendingNotification>
{
    public void Configure(EntityTypeBuilder<PendingNotification> builder)
    {
        builder.ToTable("PendingNotifications", table =>
        {
            table.HasCheckConstraint("CK_PendingNotifications_EventType", $"\"EventType\" IN ('{PendingNotification.TicketResolved}')");
            table.HasCheckConstraint("CK_PendingNotifications_Attempts_NotNegative", "\"Attempts\" >= 0");
        });

        builder.HasKey(notification => notification.Id);
        builder.Property(notification => notification.Id).ValueGeneratedNever();

        builder.Property(notification => notification.RecipientEmail).HasMaxLength(254);
        builder.Property(notification => notification.EventType).HasMaxLength(50);
        builder.Property(notification => notification.Payload).HasColumnType("jsonb");

        builder.HasOne(notification => notification.Ticket)
            .WithMany()
            .HasForeignKey(notification => notification.TicketId)
            .OnDelete(DeleteBehavior.Restrict);

        // A ticket is resolved at most once, so it can never have two resolution events.
        builder.HasIndex(notification => new { notification.TicketId, notification.EventType }).IsUnique();

        // Lets the publisher find unpublished rows in creation order without scanning published ones.
        builder.HasIndex(notification => notification.CreatedAt)
            .HasDatabaseName("IX_PendingNotifications_Unpublished")
            .HasFilter("\"PublishedAt\" IS NULL");
    }
}
