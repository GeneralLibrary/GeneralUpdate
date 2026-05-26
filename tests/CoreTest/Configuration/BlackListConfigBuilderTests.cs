using GeneralUpdate.Core.Configuration;
using GeneralUpdate.Core.FileSystem;

namespace CoreTest.Configuration;

/// <summary>
/// AAAT unit tests for <see cref="BlackListConfigBuilder"/> and <see cref="BlackListConfig"/>.
/// Covers: fluent chaining, empty builds, null-vs-empty discrimination, HasRules logic, Builder reuse.
/// </summary>
public class BlackListConfigBuilderTests
{
    #region BlackListConfigBuilder fluent API

    [Fact]
    public void Build_NoMethodsCalled_AllPropertiesNull()
    {
        // Arrange
        var builder = new BlackListConfigBuilder();

        // Act
        var config = builder.Build();

        // Assert
        Assert.Null(config.BlackFiles);
        Assert.Null(config.BlackFormats);
        Assert.Null(config.SkipDirectorys);
    }

    [Fact]
    public void Build_OnlyAddBlackFiles_OthersNull()
    {
        // Arrange
        var builder = new BlackListConfigBuilder();
        builder.AddBlackFiles("a.dll", "b.dll");

        // Act
        var config = builder.Build();

        // Assert
        Assert.NotNull(config.BlackFiles);
        Assert.Equal(2, config.BlackFiles!.Count);
        Assert.Contains("a.dll", config.BlackFiles);
        Assert.Contains("b.dll", config.BlackFiles);
        Assert.Null(config.BlackFormats);
        Assert.Null(config.SkipDirectorys);
    }

    [Fact]
    public void Build_OnlyAddBlackFormats_OthersNull()
    {
        var builder = new BlackListConfigBuilder();
        builder.AddBlackFormats(".patch", ".pdb", ".json");

        var config = builder.Build();

        Assert.NotNull(config.BlackFormats);
        Assert.Equal(3, config.BlackFormats!.Count);
        Assert.Null(config.BlackFiles);
        Assert.Null(config.SkipDirectorys);
    }

    [Fact]
    public void Build_OnlyAddSkipDirectories_OthersNull()
    {
        var builder = new BlackListConfigBuilder();
        builder.AddSkipDirectories("app-", "fail");

        var config = builder.Build();

        Assert.NotNull(config.SkipDirectorys);
        Assert.Equal(2, config.SkipDirectorys!.Count);
        Assert.Null(config.BlackFiles);
        Assert.Null(config.BlackFormats);
    }

    [Fact]
    public void Build_AllThreeSectionsFilled_AllReturned()
    {
        var builder = new BlackListConfigBuilder();
        builder.AddBlackFiles("f1.dll");
        builder.AddBlackFormats(".log");
        builder.AddSkipDirectories("tmp");

        var config = builder.Build();

        Assert.Single(config.BlackFiles!);
        Assert.Single(config.BlackFormats!);
        Assert.Single(config.SkipDirectorys!);
    }

    [Fact]
    public void AddBlackFiles_EmptyParams_NoItemsAdded()
    {
        var builder = new BlackListConfigBuilder();
        builder.AddBlackFiles();

        var config = builder.Build();

        Assert.Null(config.BlackFiles);
    }

    [Fact]
    public void AddBlackFormats_EmptyParams_NoItemsAdded()
    {
        var builder = new BlackListConfigBuilder();
        builder.AddBlackFormats();

        var config = builder.Build();

        Assert.Null(config.BlackFormats);
    }

    [Fact]
    public void AddSkipDirectories_EmptyParams_NoItemsAdded()
    {
        var builder = new BlackListConfigBuilder();
        builder.AddSkipDirectories();

        var config = builder.Build();

        Assert.Null(config.SkipDirectorys);
    }

    [Fact]
    public void AddBlackFiles_MultipleCalls_Accumulates()
    {
        var builder = new BlackListConfigBuilder();
        builder.AddBlackFiles("a.dll");
        builder.AddBlackFiles("b.dll", "c.dll");

        var config = builder.Build();

        Assert.Equal(3, config.BlackFiles!.Count);
    }

    [Fact]
    public void AddBlackFormats_MultipleCalls_Accumulates()
    {
        var builder = new BlackListConfigBuilder();
        builder.AddBlackFormats(".dll");
        builder.AddBlackFormats(".exe", ".log");

        var config = builder.Build();

        Assert.Equal(3, config.BlackFormats!.Count);
    }

    [Fact]
    public void AddSkipDirectories_MultipleCalls_Accumulates()
    {
        var builder = new BlackListConfigBuilder();
        builder.AddSkipDirectories("app-");
        builder.AddSkipDirectories("temp", "cache");

        var config = builder.Build();

        Assert.Equal(3, config.SkipDirectorys!.Count);
    }

    [Fact]
    public void Builder_FluentChaining_ReturnsBuilder()
    {
        var builder = new BlackListConfigBuilder();
        var result = builder.AddBlackFiles("a").AddBlackFormats(".z").AddSkipDirectories("d");

        Assert.Same(builder, result);
    }

    [Fact]
    public void Build_ProducesNonNullLists()
    {
        var builder = new BlackListConfigBuilder();
        builder.AddBlackFiles("f.dll");

        var config = builder.Build();

        Assert.NotNull(config.BlackFiles);
        Assert.Single(config.BlackFiles);
    }

    #endregion

    #region BlackListConfig

    [Fact]
    public void Empty_HasNoRules()
    {
        var empty = BlackListConfig.Empty;

        Assert.False(empty.HasRules);
        Assert.Null(empty.BlackFiles);
        Assert.Null(empty.BlackFormats);
        Assert.Null(empty.SkipDirectorys);
    }

    [Theory]
    // All 8 combinations of (hasFiles, hasFormats, hasDirs)
    [InlineData(false, false, false, false)] // none → false
    [InlineData(true,  false, false, true)]  // files only → true
    [InlineData(false, true,  false, true)]  // formats only → true
    [InlineData(false, false, true,  true)]  // dirs only → true
    [InlineData(true,  true,  false, true)]  // files + formats → true
    [InlineData(true,  false, true,  true)]  // files + dirs → true
    [InlineData(false, true,  true,  true)]  // formats + dirs → true
    [InlineData(true,  true,  true,  true)]  // all three → true
    public void HasRules_AllCombinations(bool hasFiles, bool hasFormats, bool hasDirs, bool expected)
    {
        var builder = new BlackListConfigBuilder();
        if (hasFiles) builder.AddBlackFiles("f.dll");
        if (hasFormats) builder.AddBlackFormats(".log");
        if (hasDirs) builder.AddSkipDirectories("tmp");

        var config = builder.Build();

        Assert.Equal(expected, config.HasRules);
    }

    [Fact]
    public void HasRules_AllNull_ReturnsFalse()
    {
        var config = new BlackListConfig(null, null, null);

        Assert.False(config.HasRules);
    }

    [Fact]
    public void HasRules_AllEmptyLists_ReturnsFalse()
    {
        // Empty lists differ from null — the builder produces null for empty
        // Direct record construction with empty lists
        var config = new BlackListConfig(
            new List<string>().AsReadOnly(),
            new List<string>().AsReadOnly(),
            new List<string>().AsReadOnly());

        Assert.False(config.HasRules);
    }

    #endregion
}
