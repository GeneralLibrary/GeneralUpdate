# Durable update attempts and exact backup binding

> **Not deployable yet.** This opt-in protocol is still a draft. It suppresses
> success reports and rejects configured Bowl/custom-strategy paths, but cannot
> prove that an arbitrary pre-existing updater executable understands the
> required-attempt environment marker. Do not enable it in production until a
> versioned producer/consumer handshake and real recovery owner are implemented
> and validated.

This layer is opt-in. The caller supplies and durably retains a unique,
non-empty `DurableUpdateAttemptId` before starting an update:

```csharp
var attemptId = Guid.NewGuid();
// Persist attemptId and its update association before calling the updater.
var request = new UpdateRequest
{
    DurableUpdateAttemptId = attemptId,
    // existing update settings...
};
```

`UpdateRequestBuilder.SetDurableUpdateAttemptId` is also available. Never reuse
an ID, even when a crash occurs before an attempt record becomes readable.
Without an ID, the existing workflow, backup naming, and rollback behavior stay
unchanged.

For an opted-in update, ClientStrategy requires backups to be enabled, creates
the durable attempt under
`{InstallPath}/.backups/.attempts`, and reserves an attempt-specific backup
path before copying. The path includes the attempt ID rather than relying on
the legacy one-second timestamp alone. The backup is copied to a fresh staging
directory and published with a non-overwriting directory move. Only after that copy succeeds is the exact path bound into the journal and
passed in `ProcessContract`, together with the attempt ID, journal directory,
and `DisableLegacyRollback=true`. The built-in launcher also supplies the
attempt ID in the `GENERALUPDATE_REQUIRED_DURABLE_ATTEMPT_ID` child-process
environment. A matching current Core bootstrap rejects missing, unreadable,
corrupt, or mismatched IPC rather than silently continuing in legacy mode, and
reopens the journal record to validate the exact bound snapshot. This detects a
lost contract only when the child is running a consumer that implements this
marker. An older or unrelated executable can ignore it; there is no version
handshake proving which consumer was launched.

The attempt journal uses atomic state-file publication and an OS-released
cross-process lock. `CleanBackup` takes the same lock while reading pins and
deleting candidates. Reserved and bound paths are excluded from retention; a
corrupt state file causes cleanup to fail rather than risk deleting an
unreadable pin. The attempt-specific staging/publish path avoids overwriting a
prior attempt snapshot through the opt-in Core path.

## Reliability boundary

The record binds an attempt to a **path**, not to an immutable snapshot
identity. This layer does not hash or verify a file manifest, prevent a
same-permission external writer from modifying that directory, or coordinate
with older binaries and external cleanup tools that do not use the journal
lock. Consequently it does not claim global snapshot immutability or a
filesystem-wide fence. The installing application should be quiescent during
backup; operators must protect the installation and journal from untrusted
writers.

`ClaimRecovery` is a cooperative journal compare-and-swap: simultaneous
claimants using one expected fence cannot both win, and a later claim makes an
earlier claim stale for journal completion. It does not fence writes to the
installation tree and does not establish that a previous owner is dead. A
caller may advance a fence only after independently establishing that the
prior claimant is no longer authorized. No `RecoveryOwner` is designated here.
`Complete` only records a caller-supplied external outcome; it does not inspect
business readiness or restore files.

For opted-in attempts, Core's legacy `AbstractStrategy.TryRollback` is
suppressed, and success reports from ClientStrategy, AbstractStrategy,
UpdateStrategy, and OssStrategy are suppressed; failure reports remain enabled.
This intentionally reports no success until a future application-owned
readiness protocol exists. The built-in Client and Upgrade paths reject a
non-empty Bowl setting and a custom OS strategy before backup or file
application; an IPC contract containing a Bowl executable is also rejected.
The OSS workflow is not part of this protocol and rejects durable-attempt
requests.
`BowlContext.Normalize` only disables `AutoRestore` when an external caller
explicitly supplies an attempt ID; Core does not currently map its IPC contract
into an externally launched Bowl process. This PR therefore does not claim
that Bowl/Host rollback is disabled in an arbitrary deployed process tree.
External startup scripts, old binaries, and other writers can ignore the
marker or perform their own rollback and must not participate. Bowl/Host
automatic recovery must remain disabled. The Bowl.Host PR #3 Recovery
implementation is fail-closed and is not a production recovery owner; its
Controlled* prototypes and fixtures are not enabled by this layer. No automatic
restore is enabled.

This is not a recovery or health closure. The updater does not recover after a
crash, select a recovery owner, make recovery idempotent, or report application
readiness. Durable-mode `Success` reports are intentionally omitted rather than
relabeling file application as health. Process survival, Bowl
`filesOnly`/`processAlive`, and successful file application are not business
health. The attempt remains pinned until an authorized cooperating caller
records an externally established terminal outcome.

GeneralUpdate #541 changes report routing and phase timing, but does not create
a business-readiness signal. This layer suppresses both the #549 MainOnly
pre-launch applied report and execution-side package-application success for
durable attempts, including every package role. A future Bowl/Host integration
must establish a versioned required-attempt handshake, consume the attempt
contract, keep `AutoRollback=false` until a single real recovery owner is
implemented and fenced, and report health only after application-owned
readiness confirmation.
