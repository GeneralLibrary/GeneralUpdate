# Real ordered-report integration

The independent `Real ordered report protocol` workflow runs the client from its
checked-out revision (which must descend from GeneralUpdate #550 at
`1bbc64e02d30385af5b08e07cc464dccdeda6aa5`) against the SaaS API from
GeneralSpace #330 at `896920c04423705fc8813818b495dfd51526e6b2`. The workflow
prints both exact checked-out SHAs and does not assume either PR is merged.
The existing `ordered-report-aot.yml` separately publishes and executes the
SQLite-backed client as Native AOT on Windows and Linux.

The job uses its own MySQL 8.0.42 service and a fresh `ordered_report_ci`
database, loopback HTTPS with a per-run certificate, temporary storage and
Data Protection keys. The harness refuses to run outside GitHub Actions and
does not upload server logs or credentials. The service and temporary files
are removed when the job ends. Since the server repository is private, configure
the `GENERALSPACE_READ_TOKEN` Actions secret with read-only access to
`TSLH-Technology/GeneralSpace`. The workflow runs on the integration branch
or by manual dispatch, not on untrusted pull request code.

It creates an upgrade record through the actual API, drops a confirmed response
after the server commits, and starts a second client process to replay the
persisted payload. It checks MySQL event and fault-log counts, record status,
duplicate success, late delivery and both kinds of conflict, including global
event ID reuse across records. The TLS connection is validated against the
temporary test CA; redirects and proxies are disabled.
