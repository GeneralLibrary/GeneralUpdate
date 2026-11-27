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
