using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UniversityHelpdesk.Api.Categories;
using UniversityHelpdesk.Api.Tickets;

namespace UniversityHelpdesk.Api.Controllers;

[ApiController]
[Route("categories")]
[Authorize]
public class CategoriesController(CategoryService categories) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<CategorySummary>>(StatusCodes.Status200OK)]
    public Task<IReadOnlyList<CategorySummary>> List(CancellationToken cancellationToken) =>
        categories.ListActiveAsync(cancellationToken);
}
