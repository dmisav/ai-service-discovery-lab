using AiServiceDiscovery.Api.Domain;

namespace AiServiceDiscovery.Api.Data;

public interface IAgentStore
{
    StoreWriteResult Apply(string tenantId, IngestEvent ingestEvent);
    IReadOnlyList<Agent> GetActiveAgents(string tenantId);
    void ReplaceFindings(string tenantId, string agentId, IReadOnlyList<Finding> findings);
    IReadOnlyList<Finding> GetFindings(string tenantId, Severity? severity, int limit);
}

public enum StoreWriteResult
{
    Applied,
    IgnoredStale,
    Invalid
}
