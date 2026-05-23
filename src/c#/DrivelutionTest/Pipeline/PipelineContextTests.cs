using GeneralUpdate.Drivelution.Abstractions.Models;
using GeneralUpdate.Drivelution.Core.Pipeline;

namespace DrivelutionTest.Pipeline;

public class PipelineContextTests
{
    [Fact]
    public void Constructor_InitializesAllProperties()
    {
        var driverInfo = new DriverInfo { Name = "test" };
        var strategy = new UpdateStrategy();
        var result = new UpdateResult();

        var ctx = new PipelineContext(driverInfo, strategy, result);

        Assert.Same(driverInfo, ctx.DriverInfo);
        Assert.Same(strategy, ctx.Strategy);
        Assert.Same(result, ctx.Result);
        Assert.NotNull(ctx.Bag);
        Assert.Empty(ctx.Bag);
    }

    [Fact]
    public void Bag_CanStoreAndRetrieveValues()
    {
        var ctx = CreateContext();

        ctx.Bag["BackupPath"] = "/tmp/backup";
        ctx.Bag["CustomData"] = 42;

        Assert.Equal("/tmp/backup", ctx.Bag["BackupPath"]);
        Assert.Equal(42, ctx.Bag["CustomData"]);
    }
    
    [Fact]
    public void Constructor_ThrowsOnNull()
    {
        Assert.Throws<ArgumentNullException>(() => new PipelineContext(null!, new UpdateStrategy(), new UpdateResult()));
        Assert.Throws<ArgumentNullException>(() => new PipelineContext(new DriverInfo(), null!, new UpdateResult()));
        Assert.Throws<ArgumentNullException>(() => new PipelineContext(new DriverInfo(), new UpdateStrategy(), null!));
    }

    private static PipelineContext CreateContext() => new(
        new DriverInfo { Name = "test" },
        new UpdateStrategy(),
        new UpdateResult());
}
