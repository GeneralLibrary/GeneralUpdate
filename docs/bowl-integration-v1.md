# External Bowl integration and update evidence (v1)

## Delivery status

GeneralUpdate now provides the producer side of an **opt-in external process
protocol**. The standalone Bowl host is **not delivered or validated by this
change**. The existing `src/GeneralUpdate.Bowl`, `tests/BowlTest`, resources and
project references remain in this repository pending verified migration.
Do not enable monitoring against the old Bowl library: it does not implement
this host protocol. `tests/MonitoringTestHost` is only a test peer; it does not
implement repair, reporting, a production health checker, or `result.json`.

Monitoring is disabled by default. No service is installed, no cross-repository
project reference is needed, and applications without Bowl continue to update.
Legacy `Bowl` / `SetBowl` / `LaunchBowl` values remain source/deserialization
compatible but no longer kill processes by name or launch an unverified helper
after starting the application.

## Configuration

Configure `UpdateRequest.Monitoring` and `UpdateRequest.DiagnosticsDirectory`
through `SetConfig`, JSON configuration, or `UpdateRequestBuilder`:

```csharp
builder.SetDiagnosticsDirectory(@"C:\ProgramData\MyProduct\UpdateState")
       .SetMonitoring(new BowlOptions
       {
           Enabled = true,
           ExecutablePath = @"C:\ProgramData\MyProduct\Monitor\Bowl.Host.exe",
           ReadyTimeoutSeconds = 10,
           VerifyLaunch = true,
           HealthTimeoutSeconds = 5,
           UpdateTimeoutSeconds = 600,
           AutoRollback = false,
           CredentialEnvironmentVariable = "MYPRODUCT_REPORT_TOKEN"
       });
```

Only enable this after deploying a separately validated compatible host.
Timeouts must be positive and the update timeout must exceed the readiness
timeout. Missing executables, malformed/mismatched ready records, host exit, or
readiness timeout abort the update before application file operations.

Without an explicit root, evidence goes under the current user's
`LocalApplicationData/GeneralUpdate/state`. Use a durable, access-controlled
directory readable/writable by the client, updater, and monitor identities.
The root and monitor installation must not overlap application, updater,
staging, or backup trees. Symbolic links/junctions are rejected on their path.
Never include the state root in update archives or rollback deletion scopes.
Maintain retention and disk capacity separately; this change does not delete
diagnostic history automatically.

Client creates `UpdateAttemptId` once per workflow; standard/silent IPC carries
it, the root and monitor options to Update. OSS transfers these through child
process environment (`GENERALUPDATE_ATTEMPT_ID`,
`GENERALUPDATE_DIAGNOSTICS_ROOT`, `GENERALUPDATE_MONITORING_OPTIONS`,
`GENERALUPDATE_LAUNCH_CLIENT_AFTER_UPDATE`). No credentials are added to these
values. Client-side updater-package application records evidence and retains
its existing rollback behavior. The external monitor starts in the updater
before main-application modification, and monitors that updater's identity.

## Frozen request / readiness / producer contract

The host is started without shell execution:

```text
Bowl.Host --attempt <canonical-GUID> --state-root <absolute-directory>
```

The attempt directory is `<state-root>/attempts/<GUID>`. JSON property names
are camelCase; timestamps are UTC ISO-8601, with process start times preserving
their precision. Each protocol record carries `protocolVersion: 1` and the
same `attemptId`. Readers must reject unknown versions and mismatched IDs.
Each writer publishes by writing/flushing a temporary file in the same
directory, then atomically renaming/replacing it. On Windows readers of
replaceable files must permit `FileShare.Delete` as well as reading.

`request.json` is written once by Update **before** starting the host:

```json
{
  "protocolVersion": 1,
  "attemptId": "4b0a8245-204d-464a-b86b-65c8f4519a8e",
  "createdAtUtc": "2026-10-07T05:00:00+00:00",
  "updater": { "pid": 1000, "startTimeUtc": "2026-10-07T04:59:59+00:00" },
  "installPath": "C:\\Apps\\Example",
  "backupDirectory": "C:\\Apps\\Example\\.backups\\backup-20261007",
  "currentVersion": "1.0.0",
  "targetVersion": "2.0.0",
  "launchMode": "processAlive",
  "healthTimeoutSeconds": 5,
  "updateTimeoutSeconds": 600,
  "autoRollback": false,
  "report": {
    "url": "https://updates.example.test/report",
    "recordId": 42,
    "type": 1,
    "credentialEnvironmentVariable": "MYPRODUCT_REPORT_TOKEN"
  }
}
```

`report.url` may be absent/null. It must be HTTP(S), without user information,
query string or fragment: do not embed credentials in URLs. Credentials are
provisioned separately for the host; the optional variable name is not a token.
Client HTTP authentication callbacks are **not** transferred to an external
process. The host must implement its own compatible credential provider.
`recordId` retains the existing first-main-package record ID; `type` is the
update trigger (1 poll / 2 push), not package `AppType`.

`ready.json` is written only by Bowl after initializing durable recovery:

```json
{
  "protocolVersion": 1,
  "attemptId": "4b0a8245-204d-464a-b86b-65c8f4519a8e",
  "host": { "pid": 2000, "startTimeUtc": "2026-10-07T05:00:01+00:00" }
}
```

Update checks version, attempt ID, exact PID/start time against the process it
started, and current liveness. A stale file alone cannot satisfy readiness.
The executable must be the host itself, not a launcher which spawns a different
process. A failed startup is cleaned up by terminating only that newly started
process. Monitor liveness is checked at package/middleware boundaries and
before manifest writing/application launch; individual synchronous file
operations cannot be interrupted mid-call by this check.

`producer.json` is atomically replaced only by Update:

```json
{
  "protocolVersion": 1,
  "attemptId": "4b0a8245-204d-464a-b86b-65c8f4519a8e",
  "stage": "awaitingHealth",
  "updatedAtUtc": "2026-10-07T05:00:03+00:00",
  "application": { "pid": 3000, "startTimeUtc": "2026-10-07T05:00:03+00:00" },
  "error": null
}
```

Stages are `preparing`, `filesApplying`, `filesApplied`, `launching`,
`awaitingHealth`, `completed`, `failed`. Failure adds an `error` object:
`category`, `stage`, `exceptionType`, `message`, `stackTrace`, `hResult`,
`failedPath`. Extra diagnostic fields can be ignored by the host.
Categories distinguish `monitorUnavailable`, `updateFailure`, and
`launchFailure`. Monitor failure after file modification does not claim
rollback or application health; operators must recover if the host is gone.
Unexpected producer termination remains diagnosable from immutable request,
last producer state, and local events without a live IPC connection.

Pre-update hooks are preflight/cancellation callbacks and execute before the
monitor is armed; they must not modify application files. Custom
`AbstractStrategy` implementations must use the normal pipeline boundaries and
await `OnAppStarted(process)` before returning/exiting. Arbitrary `IStrategy`
implementations without this process-identity contract cannot enable monitoring.

## Terminal ownership and health meaning

| Configuration | Producer behavior | Meaning of terminal success |
| --- | --- | --- |
| No monitor | One role-level legacy report, after file application and requested process creation | Files applied; business/startup health is not verified |
| Monitor, `VerifyLaunch=false` | `completed`; Update does not send a terminal report | Bowl confirms file result only |
| Monitor, `VerifyLaunch=true`, launch enabled | `awaitingHealth` with exact application identity | Bowl observes uninterrupted process survival for the configured window |
| `LaunchClientAfterUpdate=false` | Forced `filesOnly`, no launch, `completed` | File application only |

`processAlive` is **not business health**. Absence of a dump is never evidence
of health. Updater exit before a terminal producer stage is an abnormal
termination; exit after `awaitingHealth` permits continued application
observation. Early application exit, PID reuse or timeout must not produce
success. Business-readiness signals are not part of v1.

Bowl exclusively owns monitored terminal decisions, delivery and optional
rollback. Update suppresses per-package network success/failure and all
monitored terminal reports. Client preparation/download is no longer reported
as Success. Unmonitored `Success` remains numeric 2, but means files applied
(and requested process creation returned), not healthy startup. An actual
launch failure produces only Failure, never a preceding Success.

Backups are directory mirrors, not archives. `autoRollback` is explicitly
opt-in and also requires backup support/directory information. When monitor
ownership is active, Update does not run its own rollback. The host must wait
until the updater can no longer write before restoring; never restore
concurrently with an in-flight update. Preserve/exclude nested
`install/.backups` when restoring and never delete the backup before reading
it. Report network failures must not delay rollback.

`result.json` and a durable monitored outbox are **reserved for the standalone
Bowl implementation**. Their complete schemas, retry/recovery implementation,
and production validation are not delivered here. GeneralUpdate neither
writes nor trusts a result file to claim success. A future consumer must
validate version/attempt/process identities before accepting a result.

## Evidence, delivery and privacy

`events/*.json` contains immutable local `UpdateReport` evidence: stable
event ID, attempt ID, role, phase, source/target version, package name/version,
exception type/stack/HResult, known failed path, and timestamp. The original
pipeline exception is preserved, including failures of a full-package fallback.
An unrecovered package failure stops the chain rather than issuing later
successes against partially applied files.

Unmonitored reports are first written under `pending/*.json`. An HTTP failure
leaves the record there, and later records queue behind it rather than
overwriting a terminal result with a stale retry. After its producer stops,
an application-owned scheduler may call:

```csharp
await UpdateAttempt.RetryPendingReportsAsync(attemptDirectory, reporter, token);
```

Only one scheduler may own an attempt; the method refuses monitored attempts.
It validates report version/attempt ID, retries oldest first, stops on error,
and removes a record only after a successful reporter return. A missing
default HTTP endpoint is not treated as delivery. The role's immediate HTTP
attempt uses a five-second cancellation budget; custom reporters must honor
the cancellation token. Pending record creation and local failures never
depend on a successful network call.

Default HTTP serialization is deliberately still exactly
`{"recordId":42,"status":2,"type":1}` (1 Updating / 2 Success / 3 Failure).
The richer local shape is **not uploaded** and no server API is changed.
Non-2xx responses now throw from `HttpUpdateReporter`; role-level wrappers
log/isolate them so application/rollback work can proceed. Direct reporter
callers must handle those exceptions. `monitorUnavailable` has no honest
mapping to the legacy server's update Failure, so it stays local instead.

Delivery is at-least-once: a crash after a 2xx response but before local
acknowledgment can resend. The legacy server does not receive the stable
event ID and cannot be assumed to deduplicate. No exactly-once promise is made.

Known configured token, app secret, and Basic password values are redacted in
the new journal. Local stack traces/paths may still be sensitive; secure the
directory, review retention, and do not upload it wholesale. No dumps,
arbitrary files, environment contents, or diagnostic payloads are uploaded by
this integration. A custom `IUpdateReporter` receives the richer object and
must deliberately redact/allowlist any additional fields it transmits.

## Validation scope

Core targets netstandard2.0, net8.0 and net10.0 and uses source-generated JSON
metadata. The test peer exercises request/ready/producer identity checks,
timeouts/early exit, mid-update monitor loss, updater termination with durable
evidence, launch failure, files-only mode and an observed survival window.
It is not evidence of a real Bowl host's recovery, rollback, diagnostic
collection or reliable outbox implementation.
