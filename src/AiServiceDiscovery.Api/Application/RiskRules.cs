using AiServiceDiscovery.Api.Domain;

namespace AiServiceDiscovery.Api.Application;

public static class RiskRules
{
    private const string RuleVersion = "1.0.0";

    public static IReadOnlyList<Finding> Evaluate(Agent agent, IReadOnlyList<Agent> tenantAgents)
    {
        var findings = new List<Finding>();

        if (string.IsNullOrWhiteSpace(agent.OwnerId))
        {
            findings.Add(NewFinding(agent, "NoOwner", null, Severity.High, "Assign an owner or disable the agent."));
        }

        foreach (var credential in agent.Credentials.Where(credential =>
                     tenantAgents.Count(other => other.Credentials.Any(otherCredential => otherCredential.CredentialId == credential.CredentialId)) > 1))
        {
            findings.Add(NewFinding(agent, "SharedCredential", credential.CredentialId, Severity.High, "Issue per-agent credentials and rotate the shared credential."));
        }

        if (agent.Access.Count > 5 ||
            agent.Access.Any(access => access.Target.EndsWith(":*", StringComparison.Ordinal) || access.Scope == AccessScope.Admin))
        {
            findings.Add(NewFinding(agent, "OverBroadAccess", null, Severity.Medium, "Narrow the agent's access targets and scopes."));
        }

        return findings;
    }

    private static Finding NewFinding(Agent agent, string ruleId, string? credentialId, Severity severity, string action) =>
        new(agent.TenantId, agent.AgentId, ruleId, RuleVersion, credentialId, severity, action);
}
