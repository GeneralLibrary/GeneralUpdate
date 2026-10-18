using System.Text.Json;
using GeneralUpdate.Core.Configuration;
using GeneralUpdate.Core.JsonContext;

namespace CoreTest.SelfCheck;

/// <summary>
///     Self-check IPC contract tests: the additive ProcessContract fields ride the mapping
///     tail, serialize round-trip, and old JSON without the new fields deserializes to the
///     compatibility defaults (old updaters ignore, new updaters fall back).
/// </summary>
public sealed class ProcessContractSelfCheckTests
{
    private static UpdateContext NewContext() => new()
    {
        MainAppName = "app.exe",
        InstallPath = AppContext.BaseDirectory,
        ClientVersion = "1.0.1",
        LastVersion = "1.0.2",
        Encoding = System.Text.Encoding.UTF8,
        Format = Format.Zip,
        DownloadTimeOut = 30,
        AppSecretKey = "secret",
        BackupDirectory = AppContext.BaseDirectory,
        SelfCheckEnabled = true,
        SelfCheckStartupWindowSeconds = 60,
        SelfCheckRequireAck = true,
    };

    private static ProcessContract MapFrom(UpdateContext source) =>
        ConfigurationMapper.MapToProcessContract(
            source, new List<VersionEntry>
            {
                new() { RecordId = 1, Name = "pkg", Hash = "h", Url = "u", Version = "1.0.2", AppType = 1 },
            },
            new List<string>(), new List<string>(), new List<string>());

    [Fact]
    public void MapToProcessContract_CarriesSelfCheckFields()
    {
        var contract = MapFrom(NewContext());

        Assert.True(contract.SelfCheckEnabled);
        Assert.Equal(60, contract.SelfCheckStartupWindowSeconds);
        Assert.True(contract.SelfCheckRequireAck);
    }

    [Fact]
    public void SerializeDeserialize_RoundTripsSelfCheckFields()
    {
        var contract = MapFrom(NewContext());

        var json = JsonSerializer.Serialize(contract, ProcessContractJsonContext.Default.ProcessContract);
        var restored = JsonSerializer.Deserialize(json, ProcessContractJsonContext.Default.ProcessContract);

        Assert.NotNull(restored);
        Assert.True(restored!.SelfCheckEnabled);
        Assert.Equal(60, restored.SelfCheckStartupWindowSeconds);
        Assert.True(restored.SelfCheckRequireAck);
    }

    [Fact]
    public void Deserialize_OldJsonWithoutSelfCheckFields_FallsBackCompatibly()
    {
        // A JSON document written by an older client: no self-check fields at all.
        const string oldJson = """
            {
              "UpdateAppName": "app.exe",
              "InstallPath": "%BASE%",
              "CurrentVersion": "1.0.1",
              "LastVersion": "1.0.2",
              "AppSecretKey": "secret",
              "BackupDirectory": "%BASE%",
              "ReportType": 1,
              "UpdateVersions": []
            }
            """;

        var restored = JsonSerializer.Deserialize(
            oldJson.Replace("%BASE%", AppContext.BaseDirectory.Replace("\\", "\\\\")),
            ProcessContractJsonContext.Default.ProcessContract);

        Assert.NotNull(restored);
        // Additive-only: absent fields fall back so the updater keeps legacy behaviour.
        Assert.Null(restored!.SelfCheckEnabled);
        Assert.Null(restored.SelfCheckRequireAck);
        Assert.Equal(30, restored.SelfCheckStartupWindowSeconds);
    }
}
