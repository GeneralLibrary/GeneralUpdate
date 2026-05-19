using System.Text.Json;
using Velaris.Sdk.Platform;
using Velaris.Sdk.Serialization;

namespace Velaris.Sdk.Tests;

public class SerializationTests
{
    private readonly JsonSerializerOptions _options;

    public SerializationTests()
    {
        _options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };
    }

    [Fact]
    public void FlashPackMetadata_Roundtrip()
    {
        var original = new FlashPackMetadata
        {
            BundleName = "vela-os",
            BundleVersion = "2.1.3",
            FormatVersion = "1.0.0",
            PayloadType = "full_image",
            PayloadSize = 1048576,
            RequiresVersion = "2.1.0",
        };

        var json = JsonSerializer.Serialize(original, _options);
        var deserialized = JsonSerializer.Deserialize<FlashPackMetadata>(json, _options);

        Assert.NotNull(deserialized);
        Assert.Equal(original.BundleName, deserialized.BundleName);
        Assert.Equal(original.BundleVersion, deserialized.BundleVersion);
        Assert.Equal(original.PayloadSize, deserialized.PayloadSize);
    }

    [Fact]
    public void SlotInfo_Roundtrip()
    {
        var original = new SlotInfo
        {
            Id = "A",
            DevicePath = "/dev/mmcblk0p2",
            CurrentVersion = "1.0.0",
            IsBootable = true,
        };

        var json = JsonSerializer.Serialize(original, _options);
        var deserialized = JsonSerializer.Deserialize<SlotInfo>(json, _options);

        Assert.NotNull(deserialized);
        Assert.Equal("A", deserialized.Id);
        Assert.Equal("/dev/mmcblk0p2", deserialized.DevicePath);
        Assert.True(deserialized.IsBootable);
    }

    [Fact]
    public void VelaPlatform_SerializesAsString()
    {
        var json = JsonSerializer.Serialize(VelaPlatform.Linux, _options);
        Assert.Contains("linux", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void UpdateMethod_SerializesAsString()
    {
        var json = JsonSerializer.Serialize(UpdateMethod.FullImageSwap, _options);
        Assert.Contains("fullImageSwap", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void VelaConfig_Roundtrip()
    {
        var original = new VelaConfig
        {
            HubBaseUrl = "https://hub.example.com",
            PollIntervalSeconds = 120,
            WatchdogEnabled = false,
            MockMode = true,
        };

        var json = JsonSerializer.Serialize(original, _options);
        var deserialized = JsonSerializer.Deserialize<VelaConfig>(json, _options);

        Assert.NotNull(deserialized);
        Assert.Equal(original.HubBaseUrl, deserialized.HubBaseUrl);
        Assert.Equal(original.PollIntervalSeconds, deserialized.PollIntervalSeconds);
        Assert.Equal(original.WatchdogEnabled, deserialized.WatchdogEnabled);
    }

    [Fact]
    public void NullValues_NotSerialized()
    {
        var config = new VelaConfig(); // AuthToken is null by default
        var json = JsonSerializer.Serialize(config, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        });
        Assert.DoesNotContain("authToken", json);
    }
}
