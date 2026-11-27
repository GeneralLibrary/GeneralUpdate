# Startup health state foundation

`StartupHealthStore` is opt-in and independent of the update pipeline. A trusted
host creates an attempt with a future UTC deadline, shares its directory and ID
with the updated application, and the application calls `ConfirmHealthy` only
after its own business readiness checks pass. The host can record `Failed` or
`ProcessExited` when it observes a failure. `GetStatus` records `TimedOut` on
the first access at or after the deadline if no terminal outcome was recorded.
A restarted host can reopen the same attempt. Terminal outcomes cannot be
overwritten; a `false` return means no new outcome was recorded, so read the
status to distinguish a retry from a conflicting outcome.

The store uses a local, host-controlled persistent directory, atomic file
publication and an OS-released per-attempt lock. Use the same path for all
participants, outside the installation and backup trees, and restrict write
access to trusted processes. An attempt ID is not an authentication credential.
Do not delete an attempt or its lock while any participant might still use it.
The host owns eventual cleanup. UTC clock changes can affect deadlines. This
mechanism is intended for process crashes, not a guarantee against power loss
or filesystems without reliable local locking and atomic renames.

This is **not an end-to-end rollback implementation**. It does not launch or
monitor processes by itself, send reports, select or restore backups, change manifest
versions, or claim rollback ownership. In particular, a running process is not
proof of health. Existing update and reporting behavior remains unchanged.
Integrating it requires an agreed cross-process contract, a single rollback
owner shared with Bowl, exact backup identity, durable and idempotent recovery
of interrupted rollback, and lifecycle/reporting changes across the hosts.

## Explicit process integration

`StartupHealthProcess` is a separate opt-in adapter, not an update-pipeline
switch. A host that owns a new application process may create a store attempt,
persist its ID in its own durable journal, and then start and observe the child:

```csharp
var store = new StartupHealthStore(stateDirectory);
var id = store.Begin(DateTimeOffset.UtcNow.AddMinutes(2));
// Persist id before starting the process.
var supervisor = new StartupHealthProcess(stateDirectory);
var start = new ProcessStartInfo(applicationPath) { UseShellExecute = false };
using var child = supervisor.Start(id, start);
var status = await supervisor.WaitAsync(id, child, cancellationToken);
```

In the updated application, call
`StartupHealthProcess.ConfirmCurrentProcess()` **only after application-owned
business readiness checks have passed**. It returns false when not enabled or
when the attempt is already terminal; an incomplete environment is an error.
Starting a process and surviving a time window never confirm readiness. A
failed process creation records `Failed`; an observed exit before confirmation
records `ProcessExited`; the deadline records `TimedOut`. These outcomes are
distinct from file application success. Cancellation only stops observation;
it does not kill the child or change the attempt. The caller owns the process
and must decide how to stop it safely.

After a host crash, reopen the persisted ID using `GetStatus`. A confirmation
survives restart, and a pending attempt eventually times out even if no host was
running. A restarted host cannot infer process exit or safely reattach from an
attempt ID alone; `WaitAsync` is only for the process returned by `Start` in the
same host. This adapter does not provide an exclusive launch lease, a durable
process identity, or crash-safe launch deduplication. Never launch the same
attempt twice. Storage errors and inability to inspect a process are errors,
not evidence of failed health.

The child receives the store path and ID via its environment; neither is an
authentication credential. Only trusted processes with access to the protected
state directory may participate. Callers must also ensure unrelated child
processes do not inherit and use these variables.

This adapter is **not wired into** the existing GeneralUpdate updater,
Bowl.Host v1, or server reports. Bowl.Host v1's `filesOnly` and `processAlive`
verification do not consume this readiness signal. The current updater can
report `Success` before application launch; it must not be interpreted by a
server as business health. Do not combine this adapter with automatic rollback
or claim an end-to-end health guarantee until the host protocol, reporting
semantics, a single rollback owner, exact snapshot identity, process fencing,
and idempotent restart recovery are coordinated. Existing behavior remains
unchanged when this adapter is not explicitly used.

## Durable handoff (opt-in)

`StartupHealthHandoff` connects the store and process adapter for a cooperating
host. Before calling `Prepare`, the host must generate and durably journal a
unique attempt ID associated with its own update. It can then create the
attempt, start a child, and observe its explicit readiness:

```csharp
var handoff = new StartupHealthHandoff(stateDirectory);
// Persist attemptId and its update association in the host's journal first.
handoff.Prepare(attemptId, DateTimeOffset.UtcNow.AddMinutes(2));
using var child = handoff.Start(attemptId, startInfo); // UseShellExecute = false
var status = await handoff.WaitAsync(attemptId, child, cancellationToken);
```

After a host restart, use the journaled ID with `GetStatus`. If preparation
completed but no launch was claimed, the host may call `Start`. `Start` creates
an exclusive persistent launch claim before starting the process. Competing
handoff launchers cannot both claim it, and a claimed launch must never be
retried, even when the host crashes before actually starting the child. Such
a crash is ambiguous: pending is not proof of either launch or health. Recovery
can observe confirmation or timeout, but cannot infer process exit or safely
reattach. A missing or corrupt attempt is an error, not a fresh attempt.
In particular, interrupted preparation can leave an incomplete attempt; never
reuse its ID. Launch and storage errors are surfaced, not silently retried.

The claim guarantees at-most-once launch only if every participant uses
`StartupHealthHandoff.Start` for that ID and nobody deletes the state. The
lower-level `StartupHealthProcess.Start` does not enforce it. This protocol
assumes a trusted local filesystem with reliable exclusive creation, local
locking and atomic renames; it covers process crashes, not power-loss
durability. The host still owns its journal, update association, cleanup,
child lifetime and any recovery policy. It must not pass the health variables
to unrelated descendants.

This layer is not enabled by configuration and is not connected to the existing
updater or Bowl. It does not record an exact backup, own rollback, publish a
server result, or make the updater's file-application `Success` a health
confirmation. In the existing core path, `ClientStrategy` creates the backup
and sends its path in the process contract; the executing `AbstractStrategy`
attempts restoration on an apply failure only when no earlier version in that
batch succeeded, and can fall back to the latest backup. Separately, the legacy
`BowlBootstrap` can restore on a captured crash in Upgrade mode when
`AutoRestore` is enabled. Thus there is no verified single rollback owner
across all these paths. This handoff adds no restore path; connecting it to a
deployment must first establish one owner, exact snapshot identity, fencing,
and crash-safe idempotent restoration and reporting. Bowl.Host v1's
`filesOnly`/`processAlive` checks are not business readiness and have not been
integrated or validated with this handoff.
