using GeneralUpdate.Core.Configuration;
using GeneralUpdate.Core.FileSystem;

namespace CoreTest.FileSystem;

public class BlackMatcherTests
{
    private static BlackPolicy CreateConfig(List<string> files = null, List<string> formats = null, List<string> dirs = null)
        => new(files, formats, dirs);

    [Fact]
    public void IsBlacklisted_ExactFileNameMatch_ReturnsTrue()
    {
        var matcher = new BlackMatcher(CreateConfig(
            files: new List<string> { "test.exe" }));
        Assert.True(matcher.IsBlacklisted("test.exe"));
    }

    [Fact]
    public void IsBlacklisted_FileNameNoMatch_ReturnsFalse()
    {
        var matcher = new BlackMatcher(CreateConfig(
            files: new List<string> { "test.exe" }));
        Assert.False(matcher.IsBlacklisted("other.dll"));
    }

    [Fact]
    public void IsBlacklisted_GlobPatternMatch_ReturnsTrue()
    {
        var matcher = new BlackMatcher(CreateConfig(
            files: new List<string> { "*.pdb" }));
        Assert.True(matcher.IsBlacklisted("myassembly.pdb"));
    }

    [Fact]
    public void IsBlacklisted_GlobPatternNoMatch_ReturnsFalse()
    {
        var matcher = new BlackMatcher(CreateConfig(
            files: new List<string> { "*.pdb" }));
        Assert.False(matcher.IsBlacklisted("myassembly.dll"));
    }

    [Fact]
    public void IsBlacklisted_FormatMatchCaseInsensitive_ReturnsTrue()
    {
        var matcher = new BlackMatcher(CreateConfig(
            formats: new List<string> { ".tmp" }));
        Assert.True(matcher.IsBlacklisted("file.TMP"));
    }

    [Fact]
    public void IsBlacklisted_FormatNoMatch_ReturnsFalse()
    {
        var matcher = new BlackMatcher(CreateConfig(
            formats: new List<string> { ".tmp" }));
        Assert.False(matcher.IsBlacklisted("file.exe"));
    }

    [Fact]
    public void IsBlacklisted_EmptyBlackLists_ReturnsFalse()
    {
        var matcher = new BlackMatcher(CreateConfig());
        Assert.False(matcher.IsBlacklisted("anything.exe"));
    }

    [Fact]
    public void ShouldSkipDirectory_MatchFound_ReturnsTrue()
    {
        var matcher = new BlackMatcher(CreateConfig(
            dirs: new List<string> { "app-" }));
        Assert.True(matcher.ShouldSkipDirectory("app-1.0.0"));
    }

    [Fact]
    public void ShouldSkipDirectory_NoMatch_ReturnsFalse()
    {
        var matcher = new BlackMatcher(CreateConfig(
            dirs: new List<string> { "temp" }));
        Assert.False(matcher.ShouldSkipDirectory("data"));
    }

    [Fact]
    public void ShouldSkipDirectory_EmptyList_ReturnsFalse()
    {
        var matcher = new BlackMatcher(CreateConfig());
        Assert.False(matcher.ShouldSkipDirectory("app-1.0.0"));
    }

    [Fact]
    public void IsBlacklistedFormat_ExactMatch_ReturnsTrue()
    {
        var matcher = new BlackMatcher(CreateConfig(
            formats: new List<string> { ".zip" }));
        Assert.True(matcher.IsBlacklistedFormat(".zip"));
    }

    [Fact]
    public void FromConfigInfo_FilesEmpty_UsesNullInConfig()
    {
        var config = new UpdateContext { Files = new List<string>() };
        var matcher = BlackMatcher.FromConfigInfo(config);
        Assert.False(matcher.IsBlacklisted("test.dll"));
    }
}
