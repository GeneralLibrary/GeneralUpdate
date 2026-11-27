# Diagnostic artifact transport

`ArtifactUploadClient` is an opt-in transport for an already prepared Bowl schema 2
crash ZIP. It does **not** scan an outbox, create snapshots, schedule retries, or
delete evidence. It is not integrated into `LaunchAsync`. In particular, do not
read a live outbox using this API: Bowl's original writer, crash hook and GC do
not coordinate with readers. Integration requires the Bowl `outboxProtocol: 1`
shared-lock contract and a separately implemented durable snapshot/consumer.
Unmarked legacy cases must be skipped unless all legacy participants have been
stopped and an explicit migration is performed.

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
did not interpret the artifact. None of these results by itself authorizes
deleting source evidence: safe cleanup also requires the coordinated Bowl
lifecycle and a server that verifies and returns the stored content hash.
