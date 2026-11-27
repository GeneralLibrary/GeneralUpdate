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
monitor processes, send reports, select or restore backups, change manifest
versions, or claim rollback ownership. In particular, a running process is not
proof of health. Existing update and reporting behavior remains unchanged.
Integrating it requires an agreed cross-process contract, a single rollback
owner shared with Bowl, exact backup identity, durable and idempotent recovery
of interrupted rollback, and lifecycle/reporting changes across the hosts.
