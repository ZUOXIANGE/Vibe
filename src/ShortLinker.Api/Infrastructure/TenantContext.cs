namespace ShortLinker.Api.Infrastructure;

public interface ITenantContext
{
    string? TenantId { get; }
    void SetTenantId(string tenantId);
}

public class TenantContext : ITenantContext
{
    public string? TenantId { get; private set; }
    public void SetTenantId(string tenantId) => TenantId = tenantId;
}
