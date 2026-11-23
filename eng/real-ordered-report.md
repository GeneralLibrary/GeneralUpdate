# Real ordered-report integration

Run this harness in an independent workflow in the private GeneralSpace
repository, using its own `GITHUB_TOKEN` to check out server SHA
`896920c04423705fc8813818b495dfd51526e6b2` and a pinned revision of this
GeneralUpdate branch. The client revision must descend from GeneralUpdate
#550 at `1bbc64e02d30385af5b08e07cc464dccdeda6aa5`. Verify and print both
exact checked-out SHAs; neither PR is assumed merged.
The existing `ordered-report-aot.yml` separately publishes and executes the
SQLite-backed client as Native AOT on Windows and Linux.

The job uses its own MySQL 8.0.42 service and a fresh `ordered_report_ci`
database, loopback HTTPS with a per-run certificate, temporary storage and
Data Protection keys. The harness refuses to run outside GitHub Actions and
does not upload server logs or credentials. The service and temporary files
are removed when the job ends. No cross-repository secret is needed when
GeneralSpace hosts the workflow and checks out the public GeneralUpdate repo.

The workflow must use an Ubuntu GitHub-hosted runner, a fresh `mysql:8.0.42`
service with database `ordered_report_ci`, .NET SDKs 8 and 10, and
`PyMySQL==1.1.1` and `cryptography==44.0.3`. Check out the server at `server`
and client at `client` in the workspace, then run:

```sh
dotnet publish server/src_api/GeneralSpace.Api -c Release -r linux-x64 --self-contained true -p:Edition=SaaS -o "$GITHUB_WORKSPACE/client/.integration/api"
dotnet publish client/tests/RealReportHarness/RealReportHarness.csproj -c Release -o "$GITHUB_WORKSPACE/client/.integration/client"
ORDERED_REPORT_DB_PORT=3306 ORDERED_REPORT_DB_USER=root ORDERED_REPORT_DB_PASSWORD='<disposable service password>' python3 client/eng/real-ordered-report.py
```

It creates an upgrade record through the actual API, drops a confirmed response
after the server commits, and starts a second client process to replay the
persisted payload. It checks MySQL event and fault-log counts, record status,
duplicate success, late delivery and both kinds of conflict, including global
event ID reuse across records. It also sends a legacy `HttpUpdateReporter`
report with no ordered fields to the pinned server's legacy path and verifies
the record changed without creating an event. This does not test an older
server revision. The TLS connection is validated against the temporary test
CA; redirects and proxies are disabled.

## Terminal rejection candidate suite

The terminal suite additionally requires the client quarantine candidate from
GeneralUpdate #558 at `d78d9f697f37e915f083c3d0e20441a25a2d25a2`
(which descends from #550's `bb97a74f8eac25a665002722a17daea037072f2c`).
The server candidate is not frozen. Do not run this suite against the original
#330 pin or claim it verifies a final server revision: that pin cannot return
`ReportTerminalConflict`. Before running, record and verify an accessible
server candidate SHA and confirm that a committed Success followed by Failure
returns HTTP 200, `code=400`, `body=false`, and
`errorCode=ReportTerminalConflict`.

Use the same publish commands and database environment, adding
`ORDERED_REPORT_TERMINAL_CONFIRMED=true`. This selects a separate candidate
suite, including legacy reporting, lost-response replay and late-event
registration, plus both terminal-direction rejection checks. The default
suite remains unchanged and covers the original protocol where higher
sequences may replace a terminal status; it must not be run against the
terminal-latching candidate.

The process CLI is:

```text
RealReportHarness <mode> <outbox-directory> <report-url> <test-ca-pem> <record-id> [fresh-record-id]
```

`terminal-seed` commits a failure to the real client outbox and interrupts
delivery before transport. `terminal-reject` actually sends it to the SaaS API
and requires the explicit terminal response plus one quarantined event.
`terminal-restart` uses a new process, inspects the retained event, verifies
Replay/Report surface operator review without sending that record, delivers a
fresh record, and explicitly retries the quarantined event. Its sixth argument
is the fresh record ID. `conflict` verifies a real ordinary `Conflict` response
retains the outbox event without quarantine.

The Python suite compares SQLite's original payload byte-for-byte, including
event ID and sequence, with the retained quarantine across process restart
and explicit retry. It checks the stable `ReportTerminalConflict` reason;
arbitrary server messages are not persisted as reasons. MySQL checks show
the terminal record and fault attribution are unchanged, its successor was
not accepted, and the fresh record was accepted. Identity and sequence
collisions are tested separately with real API pre-registration. No payloads,
credentials, or server messages are printed or uploaded.
