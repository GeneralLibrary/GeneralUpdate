using GeneralUpdate.Core.Configuration;

namespace CoreTest.Configuration;

public class EnvironmentsTests
{
    [Fact]
    public void SetAndGet_RoundTrip_ReturnsSameValue()
    {
        var key = $"TEST_KEY_{Guid.NewGuid():N}";
        var value = "Hello World 123!@#";

        Environments.SetEnvironmentVariable(key, value);
        var result = Environments.GetEnvironmentVariable(key);

        Assert.Equal(value, result);
    }

    [Fact]
    public void Get_NonexistentKey_ReturnsEmptyString()
    {
        var result = Environments.GetEnvironmentVariable($"NONEXISTENT_{Guid.NewGuid():N}");
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void Get_AfterFirstGet_FileDeleted_ReturnsEmptyOnSecondGet()
    {
        var key = $"ONCE_KEY_{Guid.NewGuid():N}";
        Environments.SetEnvironmentVariable(key, "secret");
        var first = Environments.GetEnvironmentVariable(key);
        var second = Environments.GetEnvironmentVariable(key);

        Assert.Equal("secret", first);
        Assert.Equal(string.Empty, second);
    }

    [Fact]
    public void Set_OverwritesPreviousValue()
    {
        var key = $"OVERWRITE_KEY_{Guid.NewGuid():N}";
        Environments.SetEnvironmentVariable(key, "first");
        Environments.SetEnvironmentVariable(key, "second");
        var result = Environments.GetEnvironmentVariable(key);

        Assert.Equal("second", result);
    }

    [Fact]
    public void SetAndGet_SpecialCharacters_Preserved()
    {
        var key = $"SPECIAL_KEY_{Guid.NewGuid():N}";
        var value = "{\"name\":\"test\",\"value\":123}\n\t\r";
        Environments.SetEnvironmentVariable(key, value);
        var result = Environments.GetEnvironmentVariable(key);

        Assert.Equal(value, result);
    }
}
