namespace CoreTest.FileSystem;

using GeneralUpdate.Core.Configuration;
using GeneralUpdate.Core.FileSystem;

/// <summary>
/// AAAT unit tests for <see cref="BlackListDefaults"/> — static default values.
/// Covers: DefaultBlackFiles content, DefaultBlackFormats content (incl. Format.ZIP),
/// DefaultSkipDirectories content, list mutability.
/// </summary>
public class BlackListDefaultsTests
{
    [Fact]
    public void DefaultBlackFiles_ContainsRequiredRuntimeDlls()
    {
        var files = BlackListDefaults.DefaultBlackFiles;

        Assert.Contains("Microsoft.Bcl.AsyncInterfaces.dll", files);
        Assert.Contains("System.Collections.Immutable.dll", files);
        Assert.Contains("System.IO.Pipelines.dll", files);
        Assert.Contains("System.Text.Encodings.Web.dll", files);
        Assert.Contains("System.Text.Json.dll", files);
    }

    [Fact]
    public void DefaultBlackFiles_HasFiveEntries()
    {
        Assert.Equal(5, BlackListDefaults.DefaultBlackFiles.Count);
    }

    [Fact]
    public void DefaultBlackFormats_ContainsPatchPdbRarTarJsonZip()
    {
        var formats = BlackListDefaults.DefaultBlackFormats;

        Assert.Contains(".patch", formats);
        Assert.Contains(".pdb", formats);
        Assert.Contains(".rar", formats);
        Assert.Contains(".tar", formats);
        Assert.Contains(".json", formats);
        Assert.Contains(Format.Zip.ToExtension(), formats);
    }

    [Fact]
    public void DefaultBlackFormats_HasSixEntries()
    {
        Assert.Equal(6, BlackListDefaults.DefaultBlackFormats.Count);
    }

    [Fact]
    public void DefaultSkipDirectories_ContainsAppPrefixAndFail()
    {
        var dirs = BlackListDefaults.DefaultSkipDirectories;

        Assert.Contains("app-", dirs);
        Assert.Contains("fail", dirs);
    }

    [Fact]
    public void DefaultSkipDirectories_HasTwoEntries()
    {
        Assert.Equal(2, BlackListDefaults.DefaultSkipDirectories.Count);
    }

    [Fact]
    public void DefaultBlackFiles_IsSameInstance_OnRepeatedAccess()
    {
        var a = BlackListDefaults.DefaultBlackFiles;
        var b = BlackListDefaults.DefaultBlackFiles;

        Assert.Same(a, b);
    }

    [Fact]
    public void DefaultBlackFormats_IsSameInstance_OnRepeatedAccess()
    {
        var a = BlackListDefaults.DefaultBlackFormats;
        var b = BlackListDefaults.DefaultBlackFormats;

        Assert.Same(a, b);
    }

    [Fact]
    public void DefaultSkipDirectories_IsSameInstance_OnRepeatedAccess()
    {
        var a = BlackListDefaults.DefaultSkipDirectories;
        var b = BlackListDefaults.DefaultSkipDirectories;

        Assert.Same(a, b);
    }
}
