using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniversityHelpdesk.Api.Domain;

namespace UniversityHelpdesk.Api.Persistence.Configurations;

public class TicketConfiguration : IEntityTypeConfiguration<Ticket>
{
    public void Configure(EntityTypeBuilder<Ticket> builder)
    {
        builder.ToTable("Tickets", table =>
        {
            table.HasCheckConstraint("CK_Tickets_Status", "\"Status\" IN ('Open', 'InProgress', 'Resolved')");
            table.HasCheckConstraint("CK_Tickets_Version_Positive", "\"Version\" >= 1");
            table.HasCheckConstraint("CK_Tickets_ResolvedAt_MatchesStatus", "(\"Status\" = 'Resolved') = (\"ResolvedAt\" IS NOT NULL)");
            table.HasCheckConstraint("CK_Tickets_UpdatedAt_NotBeforeCreatedAt", "\"UpdatedAt\" >= \"CreatedAt\"");
            table.HasCheckConstraint("CK_Tickets_Room_NotBlank", "btrim(\"Room\") <> ''");
            table.HasCheckConstraint("CK_Tickets_Description_NotBlank", "btrim(\"Description\") <> ''");
        });

        builder.HasKey(ticket => ticket.Id);

        builder.Property(ticket => ticket.Room).HasMaxLength(50);
        builder.Property(ticket => ticket.Description).HasMaxLength(2000);

        builder.Property(ticket => ticket.Status)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(ticket => ticket.Version).IsConcurrencyToken();

        builder.HasOne(ticket => ticket.Student)
            .WithMany(user => user.Tickets)
            .HasForeignKey(ticket => ticket.StudentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(ticket => ticket.Category)
            .WithMany(category => category.Tickets)
            .HasForeignKey(ticket => ticket.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(ticket => new { ticket.StudentId, ticket.CreatedAt });
        builder.HasIndex(ticket => new { ticket.Status, ticket.CreatedAt });
    }
}
