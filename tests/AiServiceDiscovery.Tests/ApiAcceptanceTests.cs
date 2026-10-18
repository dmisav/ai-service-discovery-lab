using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AiServiceDiscovery.Api.Application;
using AiServiceDiscovery.Api.Domain;
using Microsoft.AspNetCore.Mvc.Testing;

namespace AiServiceDiscovery.Tests;

public sealed class ApiAcceptanceTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    [Fact]
    public async Task Ingest_uses_authenticated_tenant_and_findings_are_tenant_scoped()
    {
        var agentId = $"tenant-isolation-{Guid.NewGuid():N}";
        var response = await IngestAsync("tenant-a-key", new IngestRequest(
            "tenant-isolation",
            [new IngestEvent("upsert", agentId, 1, "Agent", AgentLifecycle.Persistent, null, null, [], [])],
            Tenant: "tenant-b"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("applied", await ResultAtAsync(response, 0));

        var tenantAFindings = await GetFindingsAsync("tenant-a-key");
        var tenantBFindings = await GetFindingsAsync("tenant-b-key");

        Assert.Contains(tenantAFindings.RootElement.EnumerateArray(), finding =>
            finding.GetProperty("agentId").GetString() == agentId &&
            finding.GetProperty("ruleId").GetString() == "NoOwner");
        Assert.DoesNotContain(tenantBFindings.RootElement.EnumerateArray(), finding =>
            finding.GetProperty("agentId").GetString() == agentId);
    }

    [Fact]
    public async Task Ingest_is_versioned_idempotent_and_tombstones_agents()
    {
        var agentId = $"versioned-{Guid.NewGuid():N}";
        var initial = new IngestEvent("upsert", agentId, 2, "Versioned", AgentLifecycle.Persistent, "owner", null, [], []);

        Assert.Equal("applied", await ResultAtAsync(await IngestAsync("tenant-a-key", new IngestRequest("v1", [initial])), 0));
        Assert.Equal("ignored_stale", await ResultAtAsync(await IngestAsync("tenant-a-key", new IngestRequest("duplicate", [initial])), 0));
        Assert.Equal("ignored_stale", await ResultAtAsync(await IngestAsync("tenant-a-key", new IngestRequest("old", [(initial with { Version = 1 })])), 0));
        Assert.Equal("applied", await ResultAtAsync(await IngestAsync("tenant-a-key", new IngestRequest("delete", [new IngestEvent("delete", agentId, 3)])), 0));
        Assert.Equal("ignored_stale", await ResultAtAsync(await IngestAsync("tenant-a-key", new IngestRequest("late-create", [(initial with { Version = 4 })])), 0));
    }

    [Fact]
    public async Task Findings_have_rule_versions_and_recommended_actions()
    {
        var agentId = $"broad-{Guid.NewGuid():N}";
        var response = await IngestAsync("tenant-a-key", new IngestRequest(
            "broad",
            [new IngestEvent(
                "upsert",
                agentId,
                1,
                "Broad agent",
                AgentLifecycle.Persistent,
                "owner",
                null,
                [],
                [new Access("mcp:*", AccessScope.Admin)])]));

        Assert.Equal("applied", await ResultAtAsync(response, 0));
        var findings = await GetFindingsAsync("tenant-a-key");
        var finding = findings.RootElement.EnumerateArray().Single(item =>
            item.GetProperty("agentId").GetString() == agentId &&
            item.GetProperty("ruleId").GetString() == "OverBroadAccess");

        Assert.Equal("1.0.0", finding.GetProperty("ruleVersion").GetString());
        Assert.False(string.IsNullOrWhiteSpace(finding.GetProperty("recommendedAction").GetString()));
    }

    [Fact]
    public void NoOwner_rule_has_positive_and_negative_cases()
    {
        var unowned = AgentFor(ownerId: null);
        var owned = AgentFor(ownerId: "owner-1");

        Assert.Contains(RiskRules.Evaluate(unowned, [unowned]), finding => finding.RuleId == "NoOwner");
        Assert.DoesNotContain(RiskRules.Evaluate(owned, [owned]), finding => finding.RuleId == "NoOwner");
    }

    [Fact]
    public void SharedCredential_rule_has_positive_and_negative_cases()
    {
        var first = AgentFor(agentId: "first", credentials: [new Credential("shared", "api-key", null, null)]);
        var second = AgentFor(agentId: "second", credentials: [new Credential("shared", "api-key", null, null)]);
        var isolated = AgentFor(agentId: "isolated", credentials: [new Credential("unique", "api-key", null, null)]);

        Assert.Contains(RiskRules.Evaluate(first, [first, second]), finding => finding.RuleId == "SharedCredential");
        Assert.DoesNotContain(RiskRules.Evaluate(isolated, [isolated]), finding => finding.RuleId == "SharedCredential");
    }

    [Fact]
    public void OverBroadAccess_rule_has_positive_and_negative_cases()
    {
        var broad = AgentFor(access: [new Access("mcp:*", AccessScope.Admin)]);
        var narrow = AgentFor(access: [new Access("mcp:crm", AccessScope.Read)]);

        Assert.Contains(RiskRules.Evaluate(broad, [broad]), finding => finding.RuleId == "OverBroadAccess");
        Assert.DoesNotContain(RiskRules.Evaluate(narrow, [narrow]), finding => finding.RuleId == "OverBroadAccess");
    }

    private async Task<HttpResponseMessage> IngestAsync(string apiKey, IngestRequest request)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
        return await client.PostAsJsonAsync("/ingest", request, JsonOptions);
    }

    private async Task<JsonDocument> GetFindingsAsync(string apiKey)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
        var response = await client.GetAsync("/findings?limit=500");
        response.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await response.Content.ReadAsStreamAsync());
    }

    private static async Task<string?> ResultAtAsync(HttpResponseMessage response, int index)
    {
        var document = JsonDocument.Parse(await response.Content.ReadAsStreamAsync());
        return document.RootElement.GetProperty("results")[index].GetProperty("result").GetString();
    }

    private static Agent AgentFor(
        string agentId = "agent",
        string? ownerId = "owner",
        IReadOnlyList<Credential>? credentials = null,
        IReadOnlyList<Access>? access = null) =>
        new("tenant", agentId, 1, "Agent", AgentLifecycle.Persistent, ownerId, null, credentials ?? [], access ?? []);
}
