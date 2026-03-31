var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddScoped<ShortLinker.Api.Infrastructure.ITenantContext, ShortLinker.Api.Infrastructure.TenantContext>();

var app = builder.Build();

app.UseMiddleware<ShortLinker.Api.Middleware.TenantResolutionMiddleware>();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
