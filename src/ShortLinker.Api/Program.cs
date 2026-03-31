using Microsoft.EntityFrameworkCore;
using ZiggyCreatures.Caching.Fusion;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Metrics;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();

var otlpEndpoint = builder.Configuration["OpenObserve:Endpoint"] ?? "http://localhost:5080/api/default/v1/traces";
var otlpHeaders = builder.Configuration["OpenObserve:Headers"] ?? "";

builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService("ShortLinker.Api"))
    .WithTracing(tracing =>
    {
        tracing.AddAspNetCoreInstrumentation()
               .AddHttpClientInstrumentation()
               .AddEntityFrameworkCoreInstrumentation()
               .AddRedisInstrumentation()
               .AddOtlpExporter(opt => {
                   opt.Endpoint = new Uri(otlpEndpoint);
                   if (!string.IsNullOrEmpty(otlpHeaders)) opt.Headers = otlpHeaders;
               });
    })
    .WithMetrics(metrics =>
    {
        metrics.AddAspNetCoreInstrumentation()
               .AddHttpClientInstrumentation()
               .AddOtlpExporter(opt => {
                   var metricsEndpoint = otlpEndpoint.Replace("/traces", "/metrics");
                   opt.Endpoint = new Uri(metricsEndpoint);
                   if (!string.IsNullOrEmpty(otlpHeaders)) opt.Headers = otlpHeaders;
               });
    });

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddDbContext<ShortLinker.Api.Infrastructure.ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<ShortLinker.Api.Infrastructure.ITenantContext, ShortLinker.Api.Infrastructure.TenantContext>();

builder.Services.AddScoped<ShortLinker.Api.Services.IShortcodeGenerator, ShortLinker.Api.Services.Base62ShortcodeGenerator>();

builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration["Redis:Configuration"] ?? "localhost:6379";
});

builder.Services.AddFusionCache()
    .WithDefaultEntryOptions(new ZiggyCreatures.Caching.Fusion.FusionCacheEntryOptions {
        Duration = TimeSpan.FromMinutes(10),
        FailSafeMaxDuration = TimeSpan.FromHours(2),
        IsFailSafeEnabled = true
    })
    .WithSerializer(new ZiggyCreatures.Caching.Fusion.Serialization.SystemTextJson.FusionCacheSystemTextJsonSerializer())
    .WithRegisteredDistributedCache()
    .WithBackplane(new ZiggyCreatures.Caching.Fusion.Backplane.StackExchangeRedis.RedisBackplane(new ZiggyCreatures.Caching.Fusion.Backplane.StackExchangeRedis.RedisBackplaneOptions { 
        Configuration = builder.Configuration["Redis:Configuration"] ?? "localhost:6379" 
    }));

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

public partial class Program { }
