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
