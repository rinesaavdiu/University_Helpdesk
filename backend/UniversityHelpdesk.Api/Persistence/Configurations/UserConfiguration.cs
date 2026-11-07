using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniversityHelpdesk.Api.Domain;

namespace UniversityHelpdesk.Api.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users", table =>
        {
            table.HasCheckConstraint("CK_Users_Role", "\"Role\" IN ('Student', 'Technician')");
            // Emails are stored lowercase so the unique index is case-insensitive.
            table.HasCheckConstraint("CK_Users_Email_Lowercase", "\"Email\" = lower(\"Email\")");
            table.HasCheckConstraint("CK_Users_DisplayName_NotBlank", "btrim(\"DisplayName\") <> ''");
            table.HasCheckConstraint("CK_Users_PasswordHash_NotBlank", "\"PasswordHash\" <> ''");
        });

        builder.HasKey(user => user.Id);

        builder.Property(user => user.Email).HasMaxLength(254);
        builder.HasIndex(user => user.Email).IsUnique();

        builder.Property(user => user.DisplayName).HasMaxLength(100);

        builder.Property(user => user.Role)
            .HasConversion<string>()
            .HasMaxLength(20);
    }
}
