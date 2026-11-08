using Microsoft.EntityFrameworkCore;
using UniversityHelpdesk.Api.Persistence;
using UniversityHelpdesk.Api.Tickets;

namespace UniversityHelpdesk.Api.Categories;

public class CategoryService(HelpdeskDbContext db)
{
    public async Task<IReadOnlyList<CategorySummary>> ListActiveAsync(CancellationToken cancellationToken) =>
        await db.Categories
            .AsNoTracking()
            .Where(category => category.IsActive)
            .OrderBy(category => category.Name)
            .Select(category => new CategorySummary(category.Id, category.Name))
            .ToListAsync(cancellationToken);
}
