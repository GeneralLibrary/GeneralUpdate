# Internal controlled full ZIP apply foundation

This Windows-only component is **not wired into production**, is not an
authorization entrypoint, and does not perform rollback or recovery. It assumes
a trusted caller with externally enforced exclusive access to the target tree,
staging files, snapshots, and state directory for the whole operation and any
subsequent recovery. Path and hash checks do not provide fencing or establish
ACL authorization. Do not call it against a live, concurrently writable tree.

`GeneralUpdate.ControlledWorker` is a separate executable placeholder for a
future production controller. It currently rejects every invocation with exit
code 2 without reading requests, preparing snapshots, or writing to disk. The
test-only READY/GO protocol below must not be used to enable it. A production
implementation requires an independently verified Host protocol and full
recovery/reporting lifecycle before this guard can be removed.

`ControlledFullZipPlan` copies the supplied entries and protected relative paths.
Each `ControlledFullZipEntry` describes a relative destination, staged payload and
SHA-256, and either a snapshot plus original SHA-256, or two nulls indicating a
destination that must not exist. Paths use canonical Windows spelling and `\`
separators. The caller must provide existing destination parent directories,
existing stage and snapshot files, and an existing trusted state directory.
State and inputs must be outside the target tree; state is also separate from
inputs. Only fixed local NTFS volumes are supported. Reparse points, hard-linked
files, alternate streams, sparse, encrypted, offline, read-only and device files,
short-name-style components, reserved names, overlapping entries and protected
paths are rejected. `.backups` is always protected. There is no directory
creation, file deletion, metadata preservation contract, or support for
arbitrary package executors.

`ControlledFullZipPrepare.Prepare` builds this write set from one staged full ZIP.
It requires disjoint existing target, state, stage and empty backup directories,
checks the complete set of target paths and opens existing originals before
copying and flushing snapshots under `backup\<relative path>`. The target parent
directories must already exist. Preparation retains any partial snapshots after
failure and does not create an intent or modify the installation. Like apply,
it requires an independently authenticated controller and exclusive access;
its checks cannot establish either. It does not preserve file metadata.
Once an intent is published, the owner must retain the stage as well as the
snapshots until a trusted recovery or completion decision. Calling
`StagedZip.RetainForRecovery` in the intent callback prevents normal disposal
from removing files referenced by the intent; this transfers cleanup ownership
to the controller and is not itself a cleanup or recovery protocol.

`ControlledFullZipApply.Apply(plan)` validates every entry and verifies the
original, snapshot and payload hashes before writing targets. It flushes supplied
snapshots, publishes a complete versioned JSON intent in the state directory
using create-new and `Flush(true)`, then copies each payload to a create-new
temporary file in the destination directory, flushes and verifies it, checks the
target again, replaces or moves without overwrite, and verifies the result. The
intent includes entries with `path`, `originalSha256` (null for new files), and
`updatedSha256`, plus paths required to inspect the operation. Snapshots remain
at the caller-provided paths and must remain protected and available. On failure,
intent, snapshots, and any temporary files are retained; exceptions report
conflicts. Nothing automatically removes unknown content.

There is no cross-file transaction, external fencing, progress journal, resume,
or automatic recovery. A process interruption can leave some files applied and
temporary files or incomplete `.writing` state files. Even after `Flush(true)`
and rename, power-loss durability of directory entries and replace operations is
not proven; neither the intent nor targets are guaranteed to be in only an old
or new state after power loss. A future Host must independently validate and
interpret the persisted intent and snapshots, maintain its own recovery journal,
and decide how to handle conflicts and retained artifacts.

The opt-in cross-repository tests can set `GENERALUPDATE_BOWL_SUPERVISOR` to a
separately built `Bowl.TestPeer.exe`. Its **test-only** supervisor starts the
`MonitoringTestHost` fixture in a Windows Job and exchanges READY/GO/DONE over
a PID-checked pipe. Only after GO does the fixture download a loopback ZIP,
prepare the write set and apply it. Failure cases exercise Bowl's test recovery;
another test kills the supervisor and checks worker termination and rejection of
attempt reuse. This is not the production Bowl.Host entrypoint, service
authentication, an authorization protocol, or proof of safe crash restart.
An additional test enables the supervisor's test-only durable delivery and
verifies a 503 leaves a pending outbox, then a separate retry process sends the
same event and receives a business acknowledgment. No credential is stored in
the outbox. This exercises the test supervisor, not production reporting.
