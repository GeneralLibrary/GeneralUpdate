# Native AOT verification

The executable in `tests/AotSmoke` targets .NET 10 and disables reflection-based
JSON serialization. It exercises manifest persistence, the HTTP version request
and response over loopback, OSS version parsing, update reporting, encrypted
process-contract IPC, Bowl crash-report serialization, and a BSDiff patch
round trip. The OSS and reporting HTTP transports are in-memory test handlers;
the Bowl reporter is invoked directly without launching external dump tools.
These checks do not constitute an end-to-end application upgrade.

On a machine with the [.NET Native AOT prerequisites](https://aka.ms/nativeaot-prerequisites),
publish and run the executable for the host RID, for example on Windows x64:

```powershell
dotnet publish tests\AotSmoke\AotSmoke.csproj -c Release -r win-x64 -o artifacts\aot-smoke
.\artifacts\aot-smoke\AotSmoke.exe
```

CI publishes and runs the same executable for `win-x64` and `linux-x64`.
Other RIDs, .NET 8, and cross-compilation are not covered by this smoke test.
The host needs the matching native compiler and system dependencies; a successful
ordinary build or `IsAotCompatible` setting alone is not proof of AOT support.

The smoke test covers only the paths listed above, not every Core API or
dependency. In particular SignalR push connections, complete download and
upgrade orchestration, external Bowl monitoring/diagnostics and rollback,
and third-party extensions or host-provided JSON types are not validated.
Callers of `StorageManager` must supply suitable `JsonTypeInfo`; arbitrary
application types require their own AOT-compatible metadata. The Differential
and Bowl dependencies currently target netstandard2.0, so their compatibility
cannot be inferred from Core's analyzer settings. Native publish warnings must
be evaluated for the paths used by each application. The smoke publish reports
IL2026 for Core and Bowl tracing: stack-frame method metadata may be unavailable
after trimming, so diagnostic method names are best-effort.
