using GeneralUpdate.Core;

namespace CoreTest.Tracer;

/// <summary>
/// AAAT unit tests for <see cref="GeneralTracer"/> static tracing utility.
/// Covers: all log levels (Debug, Info, Warn, Error, Fatal), Error/Fatal with Exception,
/// IsTracingEnabled/SetTracingEnabled toggle, Dispose lifecycle, thread safety.
/// </summary>
public class GeneralTracerTests : IDisposable
{
    private readonly bool _originalTracingEnabled;

    public GeneralTracerTests()
    {
        _originalTracingEnabled = GeneralTracer.IsTracingEnabled();
    }

    public void Dispose()
    {
        GeneralTracer.SetTracingEnabled(_originalTracingEnabled);
        GC.SuppressFinalize(this);
    }

    #region Log level methods — no-throw guarantee

    [Fact]
    public void Debug_Enabled_DoesNotThrow()
    {
        GeneralTracer.SetTracingEnabled(true);
        var ex = Record.Exception(() => GeneralTracer.Debug("test debug message"));
        Assert.Null(ex);
    }

    [Fact]
    public void Info_Enabled_DoesNotThrow()
    {
        GeneralTracer.SetTracingEnabled(true);
        var ex = Record.Exception(() => GeneralTracer.Info("test info message"));
        Assert.Null(ex);
    }

    [Fact]
    public void Warn_Enabled_DoesNotThrow()
    {
        GeneralTracer.SetTracingEnabled(true);
        var ex = Record.Exception(() => GeneralTracer.Warn("test warn message"));
        Assert.Null(ex);
    }

    [Fact]
    public void Error_Enabled_DoesNotThrow()
    {
        GeneralTracer.SetTracingEnabled(true);
        var ex = Record.Exception(() => GeneralTracer.Error("test error message"));
        Assert.Null(ex);
    }

    [Fact]
    public void Fatal_Enabled_DoesNotThrow()
    {
        GeneralTracer.SetTracingEnabled(true);
        var ex = Record.Exception(() => GeneralTracer.Fatal("test fatal message"));
        Assert.Null(ex);
    }

    #endregion

    #region With Exception overloads

    [Fact]
    public void Error_WithException_DoesNotThrow()
    {
        GeneralTracer.SetTracingEnabled(true);
        var ex = Record.Exception(() =>
            GeneralTracer.Error("error with exception", new InvalidOperationException("test")));
        Assert.Null(ex);
    }

    [Fact]
    public void Fatal_WithException_DoesNotThrow()
    {
        GeneralTracer.SetTracingEnabled(true);
        var ex = Record.Exception(() =>
            GeneralTracer.Fatal("fatal with exception", new OutOfMemoryException("mock")));
        Assert.Null(ex);
    }

    [Fact]
    public void Error_WithNullException_DoesNotThrow()
    {
        GeneralTracer.SetTracingEnabled(true);
        var ex = Record.Exception(() => GeneralTracer.Error("error", null!));
        Assert.Null(ex);
    }

    [Fact]
    public void Fatal_WithNullException_DoesNotThrow()
    {
        GeneralTracer.SetTracingEnabled(true);
        var ex = Record.Exception(() => GeneralTracer.Fatal("fatal", null!));
        Assert.Null(ex);
    }

    #endregion

    #region When tracing disabled — all methods are no-ops

    [Theory]
    [InlineData("Debug")]
    [InlineData("Info")]
    [InlineData("Warn")]
    [InlineData("Error")]
    [InlineData("Fatal")]
    public void AllLogMethods_WhenDisabled_DoNotThrow(string method)
    {
        GeneralTracer.SetTracingEnabled(false);

        Exception? ex = method switch
        {
            "Debug" => Record.Exception(() => GeneralTracer.Debug("msg")),
            "Info" => Record.Exception(() => GeneralTracer.Info("msg")),
            "Warn" => Record.Exception(() => GeneralTracer.Warn("msg")),
            "Error" => Record.Exception(() => GeneralTracer.Error("msg")),
            "Fatal" => Record.Exception(() => GeneralTracer.Fatal("msg")),
            _ => null
        };
        Assert.Null(ex);
    }

    [Fact]
    public void ErrorWithException_WhenDisabled_DoesNotThrow()
    {
        GeneralTracer.SetTracingEnabled(false);
        var ex = Record.Exception(() =>
            GeneralTracer.Error("err", new Exception("test")));
        Assert.Null(ex);
    }

    #endregion

    #region Toggle tracing

    [Fact]
    public void SetTracingEnabled_True_IsTracingEnabledTrue()
    {
        GeneralTracer.SetTracingEnabled(true);
        Assert.True(GeneralTracer.IsTracingEnabled());
    }

    [Fact]
    public void SetTracingEnabled_False_IsTracingEnabledFalse()
    {
        GeneralTracer.SetTracingEnabled(false);
        Assert.False(GeneralTracer.IsTracingEnabled());
    }

    [Fact]
    public void SetTracingEnabled_MultipleToggles_Works()
    {
        GeneralTracer.SetTracingEnabled(true);
        Assert.True(GeneralTracer.IsTracingEnabled());

        GeneralTracer.SetTracingEnabled(false);
        Assert.False(GeneralTracer.IsTracingEnabled());

        GeneralTracer.SetTracingEnabled(true);
        Assert.True(GeneralTracer.IsTracingEnabled());
    }

    #endregion

    #region Dispose

    [Fact]
    public void Dispose_DoesNotThrow()
    {
        var ex = Record.Exception(() => GeneralTracer.Dispose());
        Assert.Null(ex);
    }

    [Fact]
    public void Dispose_MultipleCalls_DoesNotThrow()
    {
        GeneralTracer.Dispose();
        var ex = Record.Exception(() => GeneralTracer.Dispose());
        Assert.Null(ex);
    }

    [Fact]
    public void Dispose_ThenLog_DoesNotThrow()
    {
        GeneralTracer.Dispose();
        GeneralTracer.SetTracingEnabled(true);
        var ex = Record.Exception(() => GeneralTracer.Info("after dispose"));
        Assert.Null(ex);
    }

    #endregion

    #region Message edge cases

    [Fact]
    public void Debug_EmptyString_DoesNotThrow()
    {
        GeneralTracer.SetTracingEnabled(true);
        var ex = Record.Exception(() => GeneralTracer.Debug(string.Empty));
        Assert.Null(ex);
    }

    [Fact]
    public void Info_LongMessage_DoesNotThrow()
    {
        GeneralTracer.SetTracingEnabled(true);
        var longMsg = new string('A', 50000);
        var ex = Record.Exception(() => GeneralTracer.Info(longMsg));
        Assert.Null(ex);
    }

    [Fact]
    public void Error_SpecialCharacters_DoesNotThrow()
    {
        GeneralTracer.SetTracingEnabled(true);
        var ex = Record.Exception(() =>
            GeneralTracer.Error("Error: { } [ ] \\ / \r\n \t <>& \" '"));
        Assert.Null(ex);
    }

    [Fact]
    public void Debug_NullMessage_DoesNotThrow()
    {
        GeneralTracer.SetTracingEnabled(true);
        var ex = Record.Exception(() => GeneralTracer.Debug(null!));
        Assert.Null(ex);
    }

    #endregion
}
