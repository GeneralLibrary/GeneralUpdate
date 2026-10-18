using System.Text.Json.Serialization;
using GeneralUpdate.Core.Diagnostics;
using GeneralUpdate.Core.SelfCheck;

namespace GeneralUpdate.Core.JsonContext;

/// <summary>
///     AOT source-generation context for the diagnostics channel and the self-check payloads it
///     packages: outbox manifests (Bowl ↔ Core disk contract), bundle metadata sent to the
///     server, plus the serialized self-check / rollback results embedded in real-time bundles.
/// </summary>
[JsonSerializable(typeof(OutboxManifest))]
[JsonSerializable(typeof(DiagnosticsManifest))]
[JsonSerializable(typeof(SelfCheckReport))]
[JsonSerializable(typeof(RollbackResult))]
public partial class DiagnosticsJsonContext : JsonSerializerContext;
