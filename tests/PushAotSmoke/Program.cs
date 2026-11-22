using System.Text.Json;
using GeneralUpdate.Core;
using GeneralUpdate.Core.Configuration;
using GeneralUpdate.Core.Download;
using GeneralUpdate.Core.Download.Sources;
using GeneralUpdate.Core.JsonContext;

const string packet = """
    {"Name":"pkg.zip","Url":"https://example.test/a","Version":"2.0.0",
     "AppType":2,"RecordId":123,"PackageType":2,"Size":42}
    """;
var source = new PushDownloadSource(packet);
_ = new GeneralUpdateBootstrap().DownloadSource(source);
var result = await source.ListAsync();
var asset = result.Assets.Single();
if (asset.Name != "pkg" || asset.RecordId != 123 || asset.AppType != 2 || asset.Size != 42 ||
    DownloadPlanBuilder.Build(result.Assets, "1.0.0", "1.0.0").Assets.Count != 1)
    throw new InvalidOperationException("Push packet mapping or plan construction failed.");

var legacy = await new PushDownloadSource(
    """{"Url":"https://example.test/a","Version":"2.0.0","AppType":1}""").ListAsync();
if (legacy.Assets.Single().RecordId != 0)
    throw new InvalidOperationException("Legacy packet record ID was not absent.");

var contract = new ProcessContract
{
    ReportType = 2,
    UpdateVersions = new List<VersionEntry> { new() { AppType = 2, RecordId = 123 } }
};
var json = JsonSerializer.Serialize(contract, ProcessContractJsonContext.Default.ProcessContract);
var roundTrip = JsonSerializer.Deserialize(json, ProcessContractJsonContext.Default.ProcessContract);
if (roundTrip?.ReportType != 2 || roundTrip.UpdateVersions?.Single().RecordId != 123)
    throw new InvalidOperationException("Push IPC serialization failed.");

Console.WriteLine("Push Native AOT smoke passed.");
