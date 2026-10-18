using AiServiceDiscovery.Api.Data;
using AiServiceDiscovery.Api.Domain;

namespace AiServiceDiscovery.Api.Application;

public sealed class IngestService(IAgentStore store)
{
    public IngestResponse Ingest(string tenantId, IngestRequest request)
    {
        var results = new List<EventResult>();
        if (string.IsNullOrWhiteSpace(request.BatchId) || request.Events is null)
        {
            return new IngestResponse(request.BatchId, [new EventResult(string.Empty, "invalid")]);
        }

        foreach (var ingestEvent in request.Events)
        {
            var writeResult = store.Apply(tenantId, ingestEvent);
            results.Add(new EventResult(ingestEvent.AgentId, ToResponseResult(writeResult)));

            if (writeResult == StoreWriteResult.Applied)
            {
                ReevaluateAffectedAgents(tenantId);
            }
        }

        return new IngestResponse(request.BatchId, results);
    }

    private void ReevaluateAffectedAgents(string tenantId)
    {
        var agents = store.GetActiveAgents(tenantId);
        foreach (var agent in agents)
        {
            store.ReplaceFindings(tenantId, agent.AgentId, RiskRules.Evaluate(agent, agents));
        }
    }

    private static string ToResponseResult(StoreWriteResult result) => result switch
    {
        StoreWriteResult.Applied => "applied",
        StoreWriteResult.IgnoredStale => "ignored_stale",
        _ => "invalid"
    };
}
