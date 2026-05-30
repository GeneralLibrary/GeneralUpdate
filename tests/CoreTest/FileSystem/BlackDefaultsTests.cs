namespace CoreTest.FileSystem;

using GeneralUpdate.Core.Configuration;
using GeneralUpdate.Core.FileSystem;

/// <summary>
/// AAAT unit tests for <see cref="BlackDefaults"/> — static default values.
/// Covers: DefaultFiles content, DefaultFormats content (incl. Format.ZIP),
/// DefaultDirectories content, list mutability.
/// </summary>
public class BlackDefaultsTests
{
    [Fact]
    public void DefaultFiles_ContainsRequiredRuntimeDlls()
    {
        var files = BlackDefaults.DefaultFiles;

        Assert.Contains("Microsoft.Bcl.AsyncInterfaces.dll", files);
        Assert.Contains("System.Collections.Immutable.dll", files);
        Assert.Contains("System.IO.Pipelines.dll", files);
        Assert.Contains("System.Text.Encodings.Web.dll", files);
        Assert.Contains("System.Text.Json.dll", files);
    }

    [Fact]
    public void DefaultFiles_HasFiveEntries()
    {
        Assert.Equal(5, BlackDefaults.DefaultFiles.Count);
    }

    [Fact]
    public void DefaultFormats_ContainsPatchPdbRarTarJsonZip()
    {
        var formats = BlackDefaults.DefaultFormats;

        Assert.Contains(".patch", formats);
        Assert.Contains(".pdb", formats);
        Assert.Contains(".rar", formats);
        Assert.Contains(".tar", formats);
        Assert.Contains(".json", formats);
        Assert.Contains(Format.Zip.ToExtension(), formats);
    }

    [Fact]
    public void DefaultFormats_HasSixEntries()
    {
        Assert.Equal(6, BlackDefaults.DefaultFormats.Count);
    }

    [Fact]
    public void DefaultDirectories_ContainsAppPrefixAndFail()
    {
        var dirs = BlackDefaults.DefaultDirectories;

        Assert.Contains("app-", dirs);
        Assert.Contains("fail", dirs);
    }

    [Fact]
    public void DefaultDirectories_HasTwoEntries()
    {
        Assert.Equal(2, BlackDefaults.DefaultDirectories.Count);
    }

    [Fact]
    public void DefaultFiles_IsSameInstance_OnRepeatedAccess()
    {
        var a = BlackDefaults.DefaultFiles;
        var b = BlackDefaults.DefaultFiles;

        Assert.Same(a, b);
    }

    [Fact]
    public void DefaultFormats_IsSameInstance_OnRepeatedAccess()
    {
        var a = BlackDefaults.DefaultFormats;
        var b = BlackDefaults.DefaultFormats;

        Assert.Same(a, b);
    }

    [Fact]
    public void DefaultDirectories_IsSameInstance_OnRepeatedAccess()
    {
        var a = BlackDefaults.DefaultDirectories;
        var b = BlackDefaults.DefaultDirectories;

        Assert.Same(a, b);
    }
}
