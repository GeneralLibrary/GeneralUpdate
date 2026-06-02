using GeneralUpdate.Drivelution.Abstractions;
using GeneralUpdate.Drivelution.Abstractions.Configuration;
using GeneralUpdate.Drivelution.Core;
using GeneralUpdate.Drivelution.Core.Execution;
using Microsoft.Extensions.DependencyInjection;

namespace DrivelutionTest.Core;

/// <summary>
/// Unit tests for <see cref="ServiceCollectionExtensions"/> following AAAT pattern.
/// Tests the DI registration of Drivelution services.
/// </summary>
public class ServiceCollectionExtensionsTests
{
    #region AddDrivelution — basic registration

    [Fact]
    public void AddDrivelution_RegistersDrivelutionOptions()
    {
        var services = new ServiceCollection();

        services.AddDrivelution();

        var provider = services.BuildServiceProvider();
        var options = provider.GetService<DrivelutionOptions>();
        Assert.NotNull(options);
    }

    [Fact]
    public void AddDrivelution_RegistersCommandRunner()
    {
        var services = new ServiceCollection();

        services.AddDrivelution();

        var provider = services.BuildServiceProvider();
        var runner = provider.GetService<ICommandRunner>();
        Assert.NotNull(runner);
        Assert.IsType<CommandRunner>(runner);
    }

    [Fact]
    public void AddDrivelution_RegistersPlatformServices()
    {
        var services = new ServiceCollection();

        services.AddDrivelution();

        var provider = services.BuildServiceProvider();

        // At least one platform's services should be registered
        var validator = provider.GetService<IDriverValidator>();
        var backup = provider.GetService<IDriverBackup>();
        var updater = provider.GetService<IGeneralDrivelution>();

        Assert.NotNull(validator);
        Assert.NotNull(backup);
        Assert.NotNull(updater);
    }

    #endregion

    #region AddDrivelution — with options configuration

    [Fact]
    public void AddDrivelution_WithConfigure_UsesCustomOptions()
    {
        var services = new ServiceCollection();

        services.AddDrivelution(options =>
        {
            options.DefaultRetryCount = 7;
            options.DefaultTimeoutSeconds = 999;
        });

        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<DrivelutionOptions>();

        Assert.Equal(7, options.DefaultRetryCount);
        Assert.Equal(999, options.DefaultTimeoutSeconds);
    }

    #endregion

    #region AddDrivelution — returns service collection for chaining

    [Fact]
    public void AddDrivelution_ReturnsSameServiceCollection()
    {
        var services = new ServiceCollection();

        var result = services.AddDrivelution();

        Assert.Same(services, result);
    }

    #endregion

    #region AddDrivelution — services are singleton

    [Fact]
    public void AddDrivelution_CommandRunner_IsSingleton()
    {
        var services = new ServiceCollection();
        services.AddDrivelution();
        var provider = services.BuildServiceProvider();

        var runner1 = provider.GetRequiredService<ICommandRunner>();
        var runner2 = provider.GetRequiredService<ICommandRunner>();

        Assert.Same(runner1, runner2);
    }

    [Fact]
    public void AddDrivelution_DrivelutionOptions_IsSingleton()
    {
        var services = new ServiceCollection();
        services.AddDrivelution();
        var provider = services.BuildServiceProvider();

        var opt1 = provider.GetRequiredService<DrivelutionOptions>();
        var opt2 = provider.GetRequiredService<DrivelutionOptions>();

        Assert.Same(opt1, opt2);
    }

    #endregion
}
