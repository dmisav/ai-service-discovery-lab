# AI Service Discovery Lab

Minimal .NET 10 implementation of agent metadata ingestion and risk assessment. It stores metadata only; credential secret values are neither accepted nor persisted.

## Run

```bash
dotnet run --project src/AiServiceDiscovery.Api --urls http://localhost:5111
```

The development API-key mapping is server-owned:

| API key | Tenant |
| --- | --- |
| `tenant-a-key` | `tenant-a` |
| `tenant-b-key` | `tenant-b` |

`POST /ingest` and `GET /findings` require the `X-Api-Key` header. A tenant supplied in JSON cannot influence storage or reads because tenant identity is resolved only from that header's server-side mapping.

## HTTP demo

With the API running on port 5111, open [demo.http](./demo.http) in VS Code with the REST Client extension. Run the requests in order to trigger `NoOwner`, `SharedCredential`, and `OverBroadAccess`, then retrieve the tenant-scoped findings. The shared-credential rule generates one finding for each of the two agents sharing the credential.

Example:

```bash
curl -X POST http://localhost:5000/ingest \
  -H 'Content-Type: application/json' \
  -H 'X-Api-Key: tenant-a-key' \
  -d '{"batchId":"batch-1","events":[{"op":"upsert","agentId":"crm-agent","version":1,"name":"CRM agent","lifecycle":"persistent","credentials":[],"access":[{"target":"mcp:crm","scope":"read"}]}]}'
```

## Production design direction

Collectors should push batches over HTTPS to a dedicated ingestion service. Replace the in-memory store with a tenant-partitioned durable database that enforces row-level tenant isolation, and publish accepted changes to a queue for asynchronous rule evaluation. The API-key mapping should be replaced by managed customer credentials with rotation, audit, and rate limits.
