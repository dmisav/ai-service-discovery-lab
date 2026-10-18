using AiServiceDiscovery.Api.Domain;

namespace AiServiceDiscovery.Api.Data;

public sealed class InMemoryAgentStore : IAgentStore
{
    private readonly object _gate = new();
    private readonly Dictionary<(string TenantId, string AgentId), Agent> _agents = new();
    private readonly Dictionary<(string TenantId, string AgentId, string RuleId, string CredentialId), Finding> _findings = new();

    public StoreWriteResult Apply(string tenantId, IngestEvent ingestEvent)
    {
        if (string.IsNullOrWhiteSpace(ingestEvent.AgentId) || ingestEvent.Version < 1)
        {
            return StoreWriteResult.Invalid;
        }

        var isUpsert = string.Equals(ingestEvent.Op, "upsert", StringComparison.OrdinalIgnoreCase);
        var isDelete = string.Equals(ingestEvent.Op, "delete", StringComparison.OrdinalIgnoreCase);
        if (!isUpsert && !isDelete)
        {
            return StoreWriteResult.Invalid;
        }

        if (isUpsert && (string.IsNullOrWhiteSpace(ingestEvent.Name) || ingestEvent.Lifecycle is null))
        {
            return StoreWriteResult.Invalid;
        }

        lock (_gate)
        {
            var key = (tenantId, ingestEvent.AgentId);
            if (_agents.TryGetValue(key, out var current))
            {
                if (ingestEvent.Version <= current.Version || (current.DeletedAt is not null && isUpsert))
                {
                    return StoreWriteResult.IgnoredStale;
                }
            }

            _agents[key] = isDelete
                ? new Agent(tenantId, ingestEvent.AgentId, ingestEvent.Version, string.Empty, AgentLifecycle.Persistent, null, null, [], [], DateTimeOffset.UtcNow)
                : new Agent(
                    tenantId,
                    ingestEvent.AgentId,
                    ingestEvent.Version,
                    ingestEvent.Name!,
                    ingestEvent.Lifecycle!.Value,
                    ingestEvent.OwnerId,
                    ingestEvent.ParentAgentId,
                    ingestEvent.Credentials ?? [],
                    ingestEvent.Access ?? []);

            return StoreWriteResult.Applied;
        }
    }

    public IReadOnlyList<Agent> GetActiveAgents(string tenantId)
    {
        lock (_gate)
        {
            return _agents.Values
                .Where(agent => agent.TenantId == tenantId && agent.DeletedAt is null)
                .ToArray();
        }
    }

    public void ReplaceFindings(string tenantId, string agentId, IReadOnlyList<Finding> findings)
    {
        lock (_gate)
        {
            foreach (var key in _findings.Keys.Where(key => key.TenantId == tenantId && key.AgentId == agentId).ToArray())
            {
                _findings.Remove(key);
            }

            foreach (var finding in findings)
            {
                _findings[(finding.TenantId, finding.AgentId, finding.RuleId, finding.CredentialId ?? string.Empty)] = finding;
            }
        }
    }

    public IReadOnlyList<Finding> GetFindings(string tenantId, Severity? severity, int limit)
    {
        lock (_gate)
        {
            return _findings.Values
                .Where(finding => finding.TenantId == tenantId && (severity is null || finding.Severity == severity))
                .OrderByDescending(finding => finding.Severity)
                .ThenBy(finding => finding.AgentId, StringComparer.Ordinal)
                .Take(limit)
                .ToArray();
        }
    }
}
