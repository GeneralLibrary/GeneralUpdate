namespace Velaris.Sdk.Tests;

public class VelaExceptionTests
{
    [Fact]
    public void Constructor_StoresMessage()
    {
        var ex = new VelaException("test error");
        Assert.Equal("test error", ex.Message);
    }

    [Fact]
    public void Constructor_WithInnerException_StoresBoth()
    {
        var inner = new InvalidOperationException("inner");
        var ex = new VelaException("outer", inner);
        Assert.Equal("outer", ex.Message);
        Assert.Same(inner, ex.InnerException);
    }

    [Fact]
    public void FromLastError_ReturnsVelaException()
    {
        // Without native DLL, FromLastError may throw DllNotFoundException
        // or return an exception with "unknown error" message.
        // Either behavior is acceptable in test environment.
        try
        {
            var ex = VelaException.FromLastError("test_op");
            Assert.NotNull(ex);
            Assert.Contains("test_op", ex.Message);
        }
        catch (DllNotFoundException)
        {
            // Expected when vela_ffi is not available
        }
    }

    [Fact]
    public void VelaException_IsException()
    {
        var ex = new VelaException("test");
        Assert.IsAssignableFrom<Exception>(ex);
    }
}
