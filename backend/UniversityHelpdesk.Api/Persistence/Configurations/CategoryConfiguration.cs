using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniversityHelpdesk.Api.Domain;

namespace UniversityHelpdesk.Api.Persistence.Configurations;

public class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("Categories", table =>
            table.HasCheckConstraint("CK_Categories_Name_NotBlank", "btrim(\"Name\") <> ''"));

        builder.HasKey(category => category.Id);

        // Category IDs are fixed by the seed data, so the database does not generate them.
        builder.Property(category => category.Id).ValueGeneratedNever();

        builder.Property(category => category.Name).HasMaxLength(50);
        builder.HasIndex(category => category.Name).IsUnique();

        builder.HasData(
            new Category { Id = 1, Name = "Audio", IsActive = true },
            new Category { Id = 2, Name = "Computer", IsActive = true },
            new Category { Id = 3, Name = "Projector", IsActive = true },
            new Category { Id = 4, Name = "Network", IsActive = true },
            new Category { Id = 5, Name = "Smart Board", IsActive = true },
            new Category { Id = 6, Name = "Other", IsActive = true });
    }
}
