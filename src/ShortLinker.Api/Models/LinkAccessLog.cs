namespace ShortLinker.Api.Models;

public class LinkAccessLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string TenantId { get; set; } = string.Empty;
    public string ShortCode { get; set; } = string.Empty;
    public DateTime AccessedAt { get; set; } = DateTime.UtcNow;
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? Referer { get; set; }
    public string? Browser { get; set; }
    public string? OS { get; set; }
}
