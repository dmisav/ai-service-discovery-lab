namespace AiServiceDiscovery.Api.Domain;

public enum AgentLifecycle
{
    Persistent,
    Ephemeral
}

public enum AccessScope
{
    Read,
    Write,
    Admin
}

public enum Severity
{
    Low,
    Medium,
    High,
    Critical
}

public sealed record Credential(string CredentialId, string Kind, string? CreatedByAgentId, DateTimeOffset? LastUsedAt);

public sealed record Access(string Target, AccessScope Scope);

public sealed record Agent(
    string TenantId,
    string AgentId,
    long Version,
    string Name,
    AgentLifecycle Lifecycle,
    string? OwnerId,
    string? ParentAgentId,
    IReadOnlyList<Credential> Credentials,
    IReadOnlyList<Access> Access,
    DateTimeOffset? DeletedAt = null);

public sealed record Finding(
    string TenantId,
    string AgentId,
    string RuleId,
    string RuleVersion,
    string? CredentialId,
    Severity Severity,
    string RecommendedAction);

public sealed record IngestRequest(string BatchId, IReadOnlyList<IngestEvent>? Events, string? Tenant = null);

public sealed record IngestEvent(
    string Op,
    string AgentId,
    long Version,
    string? Name = null,
    AgentLifecycle? Lifecycle = null,
    string? OwnerId = null,
    string? ParentAgentId = null,
    IReadOnlyList<Credential>? Credentials = null,
    IReadOnlyList<Access>? Access = null);

public sealed record EventResult(string AgentId, string Result);

public sealed record IngestResponse(string BatchId, IReadOnlyList<EventResult> Results);
