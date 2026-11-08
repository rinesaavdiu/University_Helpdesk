using Microsoft.EntityFrameworkCore;
using UniversityHelpdesk.Api.Persistence;
using UniversityHelpdesk.Api.Tickets;

namespace UniversityHelpdesk.Api.Categories;

public class CategoryService(HelpdeskDbContext db, CategoryCache cache)
{
    public async Task<IReadOnlyList<CategorySummary>> ListActiveAsync(CancellationToken cancellationToken)
    {
        if (await cache.GetAsync(cancellationToken) is { } cached)
        {
            return cached;
        }

        var categories = await db.Categories
            .AsNoTracking()
            .Where(category => category.IsActive)
            .OrderBy(category => category.Name)
            .Select(category => new CategorySummary(category.Id, category.Name))
            .ToListAsync(cancellationToken);

        await cache.SetAsync(categories, cancellationToken);
        return categories;
    }
}
