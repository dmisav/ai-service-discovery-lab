using AiServiceDiscovery.Api.Application;
using AiServiceDiscovery.Api.Data;
using AiServiceDiscovery.Api.Domain;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddSingleton<IAgentStore, InMemoryAgentStore>();
builder.Services.AddSingleton<IngestService>();
builder.Services.AddSingleton<ITenantResolver>(new ApiKeyTenantResolver(new Dictionary<string, string>
{
    ["tenant-a-key"] = "tenant-a",
    ["tenant-b-key"] = "tenant-b"
}));

var app = builder.Build();

app.UseStaticFiles();

app.MapPost("/ingest", (IngestRequest request, HttpContext context, ITenantResolver tenantResolver, IngestService ingestService) =>
{
    if (!tenantResolver.TryResolve(context.Request.Headers["X-Api-Key"], out var tenantId))
    {
        return Results.Unauthorized();
    }

    return Results.Ok(ingestService.Ingest(tenantId, request));
});

app.MapGet("/findings", (string? severity, int? limit, HttpContext context, ITenantResolver tenantResolver, IAgentStore store) =>
{
    if (!tenantResolver.TryResolve(context.Request.Headers["X-Api-Key"], out var tenantId))
    {
        return Results.Unauthorized();
    }

    if (severity is not null && !Enum.TryParse<Severity>(severity, true, out _))
    {
        return Results.BadRequest(new { error = "severity must be Low, Medium, High, or Critical." });
    }

    if (limit is <= 0 or > 500)
    {
        return Results.BadRequest(new { error = "limit must be between 1 and 500." });
    }

    Severity? parsedSeverity = severity is null ? null : Enum.Parse<Severity>(severity, true);
    return Results.Ok(store.GetFindings(tenantId, parsedSeverity, limit ?? 100));
});

app.Run();

public partial class Program;
