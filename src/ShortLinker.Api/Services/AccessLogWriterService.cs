using ShortLinker.Api.Infrastructure;
using ShortLinker.Api.Models;

namespace ShortLinker.Api.Services;

public class AccessLogWriterService : BackgroundService
{
    private readonly AccessLogChannel _channel;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AccessLogWriterService> _logger;

    public AccessLogWriterService(AccessLogChannel channel, IServiceProvider serviceProvider, ILogger<AccessLogWriterService> logger)
    {
        _channel = channel;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var log in _channel.ReadAllAsync(stoppingToken))
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                
                // Parse simple OS/Browser logic
                var ua = log.UserAgent?.ToLower() ?? "";
                log.Browser = ua.Contains("chrome") ? "Chrome" : ua.Contains("safari") ? "Safari" : ua.Contains("firefox") ? "Firefox" : "Other";
                log.OS = ua.Contains("windows") ? "Windows" : ua.Contains("mac") ? "MacOS" : ua.Contains("linux") ? "Linux" : ua.Contains("android") ? "Android" : ua.Contains("iphone") ? "iOS" : "Other";

                // bypass query filter because this is system level insert
                db.AccessLogs.Add(log);
                await db.SaveChangesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save access log for {ShortCode}", log.ShortCode);
            }
        }
    }
}