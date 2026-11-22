# Diagnostic artifact transport

`ArtifactUploadClient` is an opt-in transport for an already prepared Bowl schema 2
crash ZIP. `DiagnosticOutboxConsumer` separately implements the cooperative Bowl
`outboxProtocol: 1` case lifecycle. Neither component is integrated into
`LaunchAsync`; the host explicitly schedules and retries cases.

The host must provide a stable ZIP snapshot, `manifest.json` at its root, and
matching metadata. Retain the **same ZIP bytes** for retries, including after a
restart. Do not retry a GUID with newly generated or changed ZIP bytes. This API
does not validate the archive or its manifest. The host must also obtain consent
and apply its own evidence selection, redaction, storage protection and retention
policy: crash dumps and logs can contain credentials and personal information.

```csharp
using GeneralUpdate.Core.Diagnostics;
using System.Net.Http;

// Configure TLS, authentication and timeouts for the deployment. Do not allow
// automatic redirects: a redirect could disclose the ZIP or tenant header.
using var handler = new HttpClientHandler { AllowAutoRedirect = false };
using var http = new HttpClient(handler);
var upload = new ArtifactUploadClient(
    http, new Uri("https://example.com/Fault/UploadArtifact"),
    tenantId: tenantId, appKey: appKey);
var result = await upload.UploadAsync(new ArtifactUploadMetadata {
    Guid = caseGuid, Kind = "crash", Schema = 2,
    Version = version, TargetName = targetName
}, zipBytes, cancellationToken);
```

Tenant and app identity must come from trusted host configuration, not the
manifest. The current GeneralSpace anonymous endpoint accepts `X-Tenant-Id` but
does not authenticate `AppKey`; the host must secure access via its deployment
gateway or another authenticated channel. Do not treat either field as proof of
identity. The host owns the `HttpClient` and must restrict redirects, credentials,
proxies and destination appropriately.

The default ZIP limit is 19 MiB, configurable up to the GeneralSpace 20 MiB
request limit; multipart overhead is included when checking the hard limit.
Servers may impose a lower limit. HTTP and application-level failures throw;
cancellation propagates. The response is accepted only for HTTP success,
`code=200`, `isSuccess=true`, a matching artifact ID, and explicit `duplicate`
and `parsed` flags. A returned `contentSha256` must match the sent bytes.
`HashVerified` is false when the server omits it; older servers cannot prove a
duplicate contains the same content. `Parsed=false` means the server stored but
did not interpret the artifact.

## Cooperative outbox consumer

Only enable this after installing a Bowl writer and GC that both implement
protocol 1. Configure `{install}/fail` as the root, and have the host enumerate
version and D-format GUID directories and call `ProcessCaseAsync` for each one.
Do not pass `.locks` or the consumer's `.generalupdate-uploaded-*` directories
as cases. The host should report exceptions, retry transient failures with
backoff, and separately manage scheduling, retention and consent.

```csharp
var consumer = new DiagnosticOutboxConsumer(
    failDirectory, upload, TimeSpan.FromSeconds(30));
bool deleted = await consumer.ProcessCaseAsync(version, caseGuid, cancellationToken);
```

`false` means busy, incomplete, unmarked, or not confirmed by a server hash;
errors and cancellation propagate. Invalid cases are preserved for investigation.
The consumer validates the manifest and its top-level evidence list, rejects
links and unexpected entries, and bounds the manifest, file count and ZIP size.
It persists a ZIP and SHA-256 inside the case before sending and reuses the same
bytes across retries and restarts. A corrupt or incomplete persisted snapshot
stops automatic processing: preserve the case and investigate; do not delete the
snapshot and retry the same GUID. A hash-confirmed case is first atomically
renamed to `.generalupdate-uploaded-{guid}` before deletion, so interruption
during deletion cannot trigger a regenerated upload. The host must also enumerate
`.generalupdate-uploaded-{guid}` tombstones and call
`CleanupConfirmedCase(version, guid)` to finish an interrupted deletion under
the same stripe lock. Tombstones count against Bowl's admission limits until
removed; a cleanup failure can therefore cause exit-code-only degradation.

The lock is `{fail}/.locks/{first two lowercase GUID hex characters}.lock`,
opened with `FileShare.None` and never deleted. It remains held through snapshot,
upload and cleanup. The required upload timeout is at most two minutes; the HTTP
handler must honor cancellation. Even with a timeout, a slow upload can prevent
Bowl from acquiring the same stripe and force it to degrade to exit-code-only
monitoring, losing crash evidence. Choose a short timeout appropriate for the
deployment and monitor this failure.

This contract requires a local filesystem, compatible .NET processes running
with matching permissions, and a private outbox protected by OS permissions.
It does not protect against a malicious local process that can change the
outbox or replace paths while it is being read. It does not provide power-loss
durability guarantees beyond the filesystem's flush and rename guarantees.
Evidence and retained ZIPs may contain secrets; apply host access controls,
redaction and a retention policy. Never remove lock files while any participant
may be running. Do not allow hooks to leave background writers.

Legacy schema 2 cases without `outboxProtocol` are skipped by default. Set
`allowLegacyAfterQuiescence` only after **all** old watchdogs, uploaders and
collectors are stopped; this is an operational prerequisite, not something the
consumer can verify. GeneralSpace must provide hash-confirmed acknowledgments
before cleanup; older servers without `contentSha256` retain every case.
