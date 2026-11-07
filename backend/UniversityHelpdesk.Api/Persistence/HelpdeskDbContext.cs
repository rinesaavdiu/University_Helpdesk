using Microsoft.EntityFrameworkCore;

namespace UniversityHelpdesk.Api.Persistence;

public class HelpdeskDbContext(DbContextOptions<HelpdeskDbContext> options) : DbContext(options)
{
}
