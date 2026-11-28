# Diagnostic artifact transport

`ArtifactUploadClient` is an opt-in transport for an already prepared Bowl schema 2
crash ZIP. `DiagnosticOutboxConsumer` separately implements the cooperative Bowl
`outboxProtocol: 1` case lifecycle. Neither component is integrated into
`LaunchAsync`. The default-off standalone `GeneralUpdate.Diagnostics.Host`
now schedules startup replay and periodic retries independently of upgrades.

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

## Standalone host (default off)

Publish the executable separately from the application and upgrade worker. It
must not be a download/pipeline hook or a child whose completion is awaited by
`LaunchAsync`. Existing update APIs, reports, hooks and IPC are unchanged. A
supervisor starts the diagnostic process after consent/configuration deployment
and restarts it when it exits unexpectedly; Windows service integration is the
supervisor's responsibility (this is a foreground executable, not a Windows
Service). Use .NET 10 for managed deployment, or publish its Native AOT binary:

```powershell
dotnet publish src\GeneralUpdate.Diagnostics.Host -c Release -r win-x64 -o diagnostic-host
# Environment values must come from trusted deployment configuration, never a case manifest.
$env:GENERALUPDATE_DIAGNOSTICS_ENABLED = 'true'
$env:GENERALUPDATE_DIAGNOSTICS_TENANT = 'configured-tenant'
$env:GENERALUPDATE_DIAGNOSTICS_APP_KEY = 'configured-app'
$env:GENERALUPDATE_DIAGNOSTICS_SCAN_SECONDS = '60'
$env:GENERALUPDATE_DIAGNOSTICS_UPLOAD_SECONDS = '30'
.\diagnostic-host\GeneralUpdate.Diagnostics.Host.exe `
  'C:\application\fail' 'https://gateway.example/Fault/UploadArtifact'
```

Unset or `false` enablement exits successfully with `Disabled` without checking
arguments, touching the outbox, or making HTTP requests. Any other enablement
value is a configuration error. Enabled mode requires an explicit root, HTTPS
endpoint, nonempty tenant and app key. Scan interval is 0.1 seconds through one
day; upload timeout is positive and at most two minutes. Numeric environment
values use invariant decimal seconds. Options are copied at construction, not
read from evidence. The executable rejects redirects, disables proxies, and uses
normal system TLS trust; it has no certificate-validation bypass. Install the
deployment CA in the OS trust store and secure/authenticate the gateway.

On startup it immediately enumerates version/GUID directories, skips `.locks`
and non-case names, and finishes confirmed tombstones under their stripe lock.
It scans serially, with a full configured interval after each scan, so scans
never overlap and each case has at most one attempt per scan. Recoverable disk,
manifest, HTTP and upload-timeout errors are reported per case/directory and
preserve evidence; malformed cases do not prevent other cases being processed.
A missing root is reported and retried, not created automatically. Directory
chains are checked for links/reparse points before enumeration and consumption;
streaming enumeration checks cancellation between entries. An inaccessible or
changed directory can defer the rest of that directory to the next scan. The
same private-local-filesystem/ACL and protocol-1 deployment prerequisites apply;
these checks do not defeat a hostile local writer racing path replacement.
Legacy unmarked cases remain skipped: the executable offers no legacy override.

`Started`, `ScanStarted`, `ScanCompleted`, `ConfirmedAndDeleted`,
`TombstoneDeleted`, `Deferred` and `Stopped` are timestamped on stdout; errors
include exception type and message on stderr. Paths/messages may contain
sensitive information: protect and retain these logs appropriately. `Deferred`
means busy, incomplete, unmarked or unconfirmed, not success. A failed scan will
still emit `ScanCompleted`; inspect `Error` events, not that marker alone.
Configuration/unexpected process errors exit 1; ordinary scan errors keep
retrying. Ctrl+C and Unix SIGTERM cancel the scan/upload and emit `Stopped`;
supervisors must allow the handler to finish before force-killing. Force-kill
releases OS locks, and next startup replays persisted snapshots/tombstones.
Cancellation cannot interrupt a synchronous filesystem operation or a broken
HTTP handler that ignores its token; shutdown grace must account for this.

Applications with their own long-lived process can instead explicitly construct
`DiagnosticOutboxHost(options, observer, httpClient)` and retain the task from
`StartAsync(shutdownToken)`. `StartAsync` queues work and returns immediately;
await that task only at host shutdown, not in the upgrade lifecycle. The caller
owns the HTTP client and policies and must keep it alive until completion.
Observer callbacks must be quick and thread-safe. Observer failures and
unexpected faults fail the completion task, so callers must observe it. Host
cancellation completes normally; upload timeout is an observable case error.
Do not dynamically change tenant/app/endpoint for an existing outbox.

### No inferred attempt association

Core's version-response/report `RecordId` and IPC do **not** provide a durable
mapping to a Bowl case. `BowlContext` and schema-2 manifests provide no trustworthy
attempt record. Consequently this host sends no `RecordId`/`RecordType`, never
reports an upgrade failure, and does not associate an arbitrary crash with an
upgrade or push. Tenant/app configuration identifies the upload destination,
not a particular attempt.

Only opt in for a dedicated outbox whose ordinary crashes may be uploaded
**without association**. If a deployment requires attempt association, leave
this host disabled for that outbox until a trusted, persistent binding exists
and is fixed before first send. GeneralSpace's duplicate-GUID path checks tenant
and ZIP hash, but does not add or repair record/AppKey associations later; a
successful unassociated first upload cannot be repaired by retrying it with
record fields. Do not fabricate a record from version, current/latest attempt,
process environment, or untrusted manifest data. This PR deliberately does not
implement a record-context API in the absence of a trustworthy source.

### Verification boundary

Diagnostic unit tests cover startup replay, periodic discovery/retry, missing
root recovery, per-case errors, timeout/cancellation, another process holding the
stripe, trusted configuration, no record fields, and tombstone recovery. Native
AOT smoke covers the scheduler and consumer; CI publishes the production host
on Windows and Linux. These use local fixtures/mock HTTP and are **not** evidence
of real Bowl watchdog/dump capture or a deployed GeneralSpace service.
The cross-repository follow-up reuses GeneralSpace #326's
`diagnostic-three-repo.yml`/`eng/diagnostic-three-repo.py` with exact source SHAs
and must separately report real writer/API verification versus complete
watchdog/dump coverage.

The lock is `{fail}/.locks/{first two lowercase GUID hex characters}.lock`,
opened with `FileShare.None` and never deleted. It remains held through snapshot,
upload and cleanup. The required upload timeout is at most two minutes; the HTTP
handler must honor cancellation. Even with a timeout, a slow upload can prevent
Bowl from acquiring the same stripe. Bowl tries another GUID; exhausted
admission or lock retries can degrade monitoring to exit-code-only, losing
crash evidence. Choose a short timeout appropriate for the deployment and
monitor this failure.

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

## Local integration harness

`tests/OutboxConsumerHarness` invokes the real consumer once against a loopback
HTTPS service. Supply the server's PEM certificate to pin its exact bytes;
hostname validation remains enabled. It disables proxies and redirects. For
example, with a test certificate valid for `localhost`:

```powershell
$env:OUTBOX_TEST_TENANT = 'test-tenant'
$env:OUTBOX_TEST_APP_KEY = 'test-app'
dotnet run --project tests\OutboxConsumerHarness -- `
  'C:\fixture\fail' '1.0.2' '<case-guid>' `
  'https://localhost:5001/Fault/UploadArtifact' 'C:\fixture\server.pem'
```

Exit code 0 means hash-confirmed deletion, 2 means skipped or not confirmed,
and 1 means an error. This test harness does not schedule retries: invoke it
again to exercise persisted snapshots. Never use its certificate pinning to
disable certificate validation in production.
