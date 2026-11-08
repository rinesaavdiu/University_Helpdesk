using Microsoft.OpenApi;
using UniversityHelpdesk.Api.Authentication;
using UniversityHelpdesk.Api.Categories;
using UniversityHelpdesk.Api.ErrorHandling;
using UniversityHelpdesk.Api.Persistence;
using UniversityHelpdesk.Api.Persistence.Seeding;
using UniversityHelpdesk.Api.Tickets;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHelpdeskControllers();
builder.Services.AddHelpdeskPersistence();
builder.Services.AddHelpdeskAuthentication();
builder.Services.AddScoped<DemoAccountSeeder>();
builder.Services.AddScoped<TicketService>();
builder.Services.AddScoped<CategoryService>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "University Helpdesk API",
        Version = "v1",
    });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Paste the accessToken returned by POST /auth/login.",
    });
    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = [],
    });
});

var app = builder.Build();

if (args.Contains(DemoAccountSeeder.CommandName))
{
    if (!app.Environment.IsDevelopment())
    {
        app.Logger.LogError("{Command} only runs in the Development environment.", DemoAccountSeeder.CommandName);
        return 1;
    }

    using var scope = app.Services.CreateScope();
    var seeded = await scope.ServiceProvider.GetRequiredService<DemoAccountSeeder>().SeedAsync();
    return seeded ? 0 : 1;
}

// Registered in every environment so Development also returns the sanitized 500.
app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
return 0;
