using GeneralUpdate.Core;

namespace CoreTest;

public class GracefulExitTests
{
    [Fact]
    public async Task ShutdownAsync_ProcessNull_ReturnsWithoutException()
    {
        var ex = await Record.ExceptionAsync(() => GracefulExit.ShutdownAsync(null));
        Assert.Null(ex);
    }
}
