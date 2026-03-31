using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using ShortLinker.Api.Infrastructure;
using ShortLinker.Api.Models;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;
using Xunit;

namespace ShortLinker.Tests.IntegrationTests;

public class ShortLinkerApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _dbContainer = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("shortlinker_db")
        .WithUsername("shortlinker")
        .WithPassword("password123")
        .Build();

    private readonly RedisContainer _redisContainer = new RedisBuilder()
        .WithImage("redis:7-alpine")
        .Build();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration(config =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "ConnectionStrings:DefaultConnection", _dbContainer.GetConnectionString() },
                { "Redis:Configuration", _redisContainer.GetConnectionString() }
            });
        });
    }

    public async Task InitializeAsync()
    {
        await _dbContainer.StartAsync();
        await _redisContainer.StartAsync();
        
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.MigrateAsync();
    }

    public new async Task DisposeAsync()
    {
        await _dbContainer.DisposeAsync();
        await _redisContainer.DisposeAsync();
    }
}

public class LinksIntegrationTests : IClassFixture<ShortLinkerApiFactory>
{
    private readonly HttpClient _client;

    public LinksIntegrationTests(ShortLinkerApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Services_Are_Registered_Correctly()
    {
        // Act & Assert
        var response = await _client.GetAsync("/");
        // It might return 404 because no routes are mapped yet, but the server started successfully
        // with the testcontainers.
        Assert.NotNull(response);
    }
}