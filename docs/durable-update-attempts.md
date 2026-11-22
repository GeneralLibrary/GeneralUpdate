# Durable update attempts and exact backup binding

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
directory and published with a non-overwriting directory move. Only after that
copy succeeds is the exact path bound into the journal and passed in
`ProcessContract`, together with the attempt ID, journal directory, and
`DisableLegacyRollback=true`. The executing Core bootstrap reopens the record
and rejects an absent, corrupt, unbound, mismatched, or missing snapshot rather
than using `GetLatestBackup`.

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
suppressed, and `BowlContext.Normalize` suppresses `AutoRestore` when the
attempt ID is propagated to that context. The process contract carries
`DisableLegacyRollback` for cooperating consumers. However, the current
GeneralUpdate producer does not construct Bowl.Host requests or pass this field
into an external Host; Bowl.Host's separate `AutoRollback` setting is not wired
or verified here. Keep Bowl/Host automatic rollback disabled for this mode.
The installed Bowl.Host PR #3 Recovery implementation is fail-closed and is
not a production recovery owner; its Controlled* prototypes and fixtures are
not enabled by this layer. No automatic restore is enabled.

This is not a recovery or health closure. The updater does not recover after a
crash, select a recovery owner, make recovery idempotent, or report application
readiness. Process survival, Bowl `filesOnly`/`processAlive`, and successful
file application are not business health. The attempt remains pinned until an
authorized cooperating caller records an externally established terminal
outcome.

The reporting changes from GeneralUpdate #541 concern report routing and
phase timing; they do not make a report a business-readiness signal. On the
#549 base, the MainOnly client path can report applied immediately before it
launches the upgrade process; #541 removes that particular early report. In
either version, execution-side package-application success still does not prove
that the updated application launched or passed its own readiness checks. A
future Bowl/Host integration must consume the attempt contract, keep
`AutoRollback=false` until a single real recovery owner is implemented and
fenced, and report health only after application-owned readiness confirmation.
