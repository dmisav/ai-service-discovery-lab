namespace AiServiceDiscovery.Api.Application;

public interface ITenantResolver
{
    bool TryResolve(string? apiKey, out string tenantId);
}

public sealed class ApiKeyTenantResolver(IReadOnlyDictionary<string, string> tenantKeys) : ITenantResolver
{
    public bool TryResolve(string? apiKey, out string tenantId)
    {
        if (apiKey is not null && tenantKeys.TryGetValue(apiKey, out var resolvedTenant))
        {
            tenantId = resolvedTenant;
            return true;
        }

        tenantId = string.Empty;
        return false;
    }
}
