using GeneralUpdate.Core;
using GeneralUpdate.Core.Configuration;

namespace CoreTest.Configuration;

/// <summary>
/// Unit tests for <see cref="ObjectTranslator"/> following AAAT (Arrange-Act-Assert-TearDown).
/// </summary>
public class ObjectTranslatorTests
{
    #region GetPacketHash

    [Fact]
    public void GetPacketHash_ValidVersionInfo_ReturnsFormattedHash()
    {
        // Arrange
        GeneralTracer.SetTracingEnabled(true);
        var version = new VersionInfo { Hash = "abc123def" };

        // Act
        var result = ObjectTranslator.GetPacketHash(version);

        // Assert
        Assert.Equal("[PacketHash]:abc123def ", result);
    }

    [Fact]
    public void GetPacketHash_NonVersionInfoObject_ReturnsEmpty()
    {
        // Arrange
        GeneralTracer.SetTracingEnabled(true);
        var notVersion = "just a string";

        // Act
        var result = ObjectTranslator.GetPacketHash(notVersion);

        // Assert
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void GetPacketHash_TracingDisabled_ReturnsEmptyEvenForVersionInfo()
    {
        // Arrange
        GeneralTracer.SetTracingEnabled(false);
        var version = new VersionInfo { Hash = "abc123def" };

        // Act
        var result = ObjectTranslator.GetPacketHash(version);

        // Assert
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void GetPacketHash_NullObject_ReturnsEmpty()
    {
        // Arrange
        GeneralTracer.SetTracingEnabled(true);

        // Act
        var result = ObjectTranslator.GetPacketHash(null!);

        // Assert
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void GetPacketHash_VersionInfoWithNullHash_ReturnsEmptyHashSegment()
    {
        // Arrange
        GeneralTracer.SetTracingEnabled(true);
        var version = new VersionInfo { Hash = null };

        // Act
        var result = ObjectTranslator.GetPacketHash(version);

        // Assert
        Assert.Equal("[PacketHash]: ", result);
    }

    [Fact]
    public void GetPacketHash_VersionInfoWithEmptyHash_ReturnsEmptyHashSegment()
    {
        // Arrange
        GeneralTracer.SetTracingEnabled(true);
        var version = new VersionInfo { Hash = string.Empty };

        // Act
        var result = ObjectTranslator.GetPacketHash(version);

        // Assert
        Assert.Equal("[PacketHash]: ", result);
    }

    #endregion
}
