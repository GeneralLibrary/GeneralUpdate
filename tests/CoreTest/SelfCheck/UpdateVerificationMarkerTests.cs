using GeneralUpdate.Core.SelfCheck;

namespace CoreTest.SelfCheck;

/// <summary>
///     Verify-marker lifecycle tests: write/read/delete, corrupted-JSON tolerance, unknown
///     schema handling, orphan-age decision and the ack-file protocol (fail wins over ok).
/// </summary>
public sealed class UpdateVerificationMarkerTests : IDisposable
{
    private readonly string _installPath;

    public UpdateVerificationMarkerTests()
    {
        _installPath = Path.Combine(Path.GetTempPath(), $"gu-marker-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_installPath);
    }

    public void Dispose()
    {
        try { Directory.Delete(_installPath, true); } catch { /* best effort */ }
    }

    [Fact]
    public void Write_TryRead_Delete_RoundTrip()
    {
        Assert.Null(UpdateVerificationMarker.TryRead(_installPath));

        UpdateVerificationMarker.Write(_installPath, "1.0.2", "1.0.1", @"D:\app\.backups\backup-x");
        var marker = UpdateVerificationMarker.TryRead(_installPath);

        Assert.NotNull(marker);
        Assert.Equal(UpdateVerificationMarker.CurrentSchema, marker!.Schema);
        Assert.Equal("1.0.2", marker.TargetVersion);
        Assert.Equal("1.0.1", marker.PreviousVersion);
        Assert.Equal(@"D:\app\.backups\backup-x", marker.BackupDirectory);
        Assert.Equal(MarkerStates.Launched, marker.State);
        Assert.True(File.Exists(UpdateVerificationMarker.GetPath(_installPath)));

        Assert.True(UpdateVerificationMarker.Delete(_installPath));
        Assert.Null(UpdateVerificationMarker.TryRead(_installPath));
    }

    [Fact]
    public void TryRead_CorruptedJson_ToleratedAsAbsent()
    {
        File.WriteAllText(UpdateVerificationMarker.GetPath(_installPath), "{ not valid json !!");

        Assert.Null(UpdateVerificationMarker.TryRead(_installPath));
    }

    [Fact]
    public void TryRead_UnknownSchema_ToleratedAsAbsent()
    {
        File.WriteAllText(UpdateVerificationMarker.GetPath(_installPath),
            """{ "schema": 99, "targetVersion": "1.0.2" }""");

        Assert.Null(UpdateVerificationMarker.TryRead(_installPath));
    }

    [Fact]
    public void IsOrphanedBeyondWindow_AgeDecides()
    {
        UpdateVerificationMarker.Write(_installPath, "1.0.2", "1.0.1", null);
        var fresh = UpdateVerificationMarker.TryRead(_installPath)!;
        Assert.False(UpdateVerificationMarker.IsOrphanedBeyondWindow(fresh));

        var ancient = new UpdateVerificationMarkerInfo
        {
            TargetVersion = "1.0.2",
            CreatedAtUtc = DateTime.UtcNow - UpdateVerificationMarker.OrphanWindow - TimeSpan.FromMinutes(1)
        };
        Assert.True(UpdateVerificationMarker.IsOrphanedBeyondWindow(ancient));
    }

    [Fact]
    public void AckProtocol_FailWinsOverOk_AndReasonIsCarried()
    {
        Assert.Equal(VerificationAckState.None,
            UpdateVerificationMarker.TryReadAck(_installPath, out var noneReason));
        Assert.Null(noneReason);

        UpdateVerificationMarker.WriteAck(_installPath, healthy: true, "ok");
        Assert.Equal(VerificationAckState.Ok,
            UpdateVerificationMarker.TryReadAck(_installPath, out _));

        // The application writes .fail after .ok (health flipped) — the verdict must fail.
        UpdateVerificationMarker.WriteAck(_installPath, healthy: false, "db unreachable");
        Assert.Equal(VerificationAckState.Fail,
            UpdateVerificationMarker.TryReadAck(_installPath, out var reason));
        Assert.Contains("db unreachable", reason);

        UpdateVerificationMarker.DeleteAckFiles(_installPath);
        Assert.Equal(VerificationAckState.None,
            UpdateVerificationMarker.TryReadAck(_installPath, out _));
    }

    [Fact]
    public void UpdateSelfCheck_PendingVerificationAndReportHealth()
    {
        Assert.Null(UpdateSelfCheck.GetPendingVerification(_installPath));

        UpdateVerificationMarker.Write(_installPath, "1.0.2", "1.0.1", null);
        var pending = UpdateSelfCheck.GetPendingVerification(_installPath);

        Assert.NotNull(pending);
        Assert.Equal("1.0.2", pending!.TargetVersion);
        Assert.Equal("1.0.1", pending.PreviousVersion);
        Assert.True(File.Exists(pending.MarkerPath));

        UpdateSelfCheck.ReportHealth(_installPath, healthy: false, "licence invalid");
        Assert.Equal(VerificationAckState.Fail,
            UpdateVerificationMarker.TryReadAck(_installPath, out var reason));
        Assert.Contains("licence invalid", reason);
    }
}
