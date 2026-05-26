namespace CoreTest.Tracer;

/// <summary>
/// AAAT unit tests for <see cref="TextTraceListener"/>.
/// Covers: construction, Write, WriteLine, queue behavior, disposal lifecycle, double-dispose, write-after-dispose.
/// </summary>
public class TextTraceListenerTests : IDisposable
{
    private readonly string _logFilePath;
    private TextTraceListener? _listener;

    public TextTraceListenerTests()
    {
        _logFilePath = Path.Combine(Path.GetTempPath(), $"test_trace_{Guid.NewGuid():N}.log");
    }

    public void Dispose()
    {
        try
        {
            _listener?.Dispose();
        }
        catch { }

        try
        {
            if (File.Exists(_logFilePath))
                File.Delete(_logFilePath);
        }
        catch { }
    }

    #region Construction

    [Fact]
    public void Ctor_CreatesInstance()
    {
        _listener = new TextTraceListener(_logFilePath);

        Assert.NotNull(_listener);
        Assert.NotNull(_listener.Name); // TraceListener base has Name
    }

    [Fact]
    public void Ctor_LogFileDoesNotExistYet_Works()
    {
        Assert.False(File.Exists(_logFilePath));

        _listener = new TextTraceListener(_logFilePath);

        Assert.NotNull(_listener);
    }

    #endregion

    #region Write & WriteLine

    [Fact]
    public void Write_SimpleMessage_DoesNotThrow()
    {
        _listener = new TextTraceListener(_logFilePath);

        var ex = Record.Exception(() => _listener.Write("test message"));

        Assert.Null(ex);
    }

    [Fact]
    public void WriteLine_SimpleMessage_DoesNotThrow()
    {
        _listener = new TextTraceListener(_logFilePath);

        var ex = Record.Exception(() => _listener.WriteLine("test line"));

        Assert.Null(ex);
    }

    [Fact]
    public void Write_NullMessage_DoesNotThrow()
    {
        _listener = new TextTraceListener(_logFilePath);

        var ex = Record.Exception(() => _listener.Write(null!));

        Assert.Null(ex);
    }

    [Fact]
    public void WriteLine_NullMessage_DoesNotThrow()
    {
        _listener = new TextTraceListener(_logFilePath);

        var ex = Record.Exception(() => _listener.WriteLine(null!));

        Assert.Null(ex);
    }

    [Fact]
    public void Write_EmptyString_DoesNotThrow()
    {
        _listener = new TextTraceListener(_logFilePath);

        var ex = Record.Exception(() => _listener.Write(string.Empty));

        Assert.Null(ex);
    }

    [Fact]
    public void Write_MultipleMessages_DoesNotThrow()
    {
        _listener = new TextTraceListener(_logFilePath);

        for (int i = 0; i < 100; i++)
        {
            var ex = Record.Exception(() => _listener.Write($"message {i}"));
            Assert.Null(ex);
        }
    }

    #endregion

    #region Dispose lifecycle

    [Fact]
    public void Dispose_SingleCall_DoesNotThrow()
    {
        _listener = new TextTraceListener(_logFilePath);
        var ex = Record.Exception(() => _listener.Dispose());
        Assert.Null(ex);
    }

    [Fact]
    public void Dispose_DoubleCall_DoesNotThrow()
    {
        _listener = new TextTraceListener(_logFilePath);
        _listener.Dispose();
        var ex = Record.Exception(() => _listener.Dispose());
        Assert.Null(ex);
    }

    [Fact]
    public void Write_AfterDispose_DoesNotThrow()
    {
        _listener = new TextTraceListener(_logFilePath);
        _listener.Dispose();

        var ex = Record.Exception(() => _listener.Write("after dispose"));
        Assert.Null(ex);
    }

    [Fact]
    public void WriteLine_AfterDispose_DoesNotThrow()
    {
        _listener = new TextTraceListener(_logFilePath);
        _listener.Dispose();

        var ex = Record.Exception(() => _listener.WriteLine("after dispose line"));
        Assert.Null(ex);
    }

    #endregion

    #region Concurrent writes

    [Fact]
    public void Write_ConcurrentWrites_DoesNotThrow()
    {
        _listener = new TextTraceListener(_logFilePath);

        var tasks = Enumerable.Range(0, 50).Select(i =>
            Task.Run(() =>
            {
                for (int j = 0; j < 20; j++)
                    _listener.Write($"thread-{i}-msg-{j}");
            })).ToArray();

        var aggregate = Record.Exception(() => Task.WaitAll(tasks));
        Assert.Null(aggregate);
    }

    #endregion

    #region Background thread delivers messages

    [Fact]
    public void Write_MessagesEventuallyDeliveredToFile()
    {
        _listener = new TextTraceListener(_logFilePath);

        for (int i = 0; i < 10; i++)
            _listener.Write($"message {i}");

        _listener.Dispose(); // Flush & close

        Assert.True(File.Exists(_logFilePath));
        var content = File.ReadAllText(_logFilePath);
        Assert.Contains("message 0", content);
    }

    [Fact]
    public void WriteLine_AppendsNewlineEventually()
    {
        _listener = new TextTraceListener(_logFilePath);

        _listener.WriteLine("line content");

        _listener.Dispose();

        Assert.True(File.Exists(_logFilePath));
        var content = File.ReadAllText(_logFilePath);
        Assert.Contains("line content", content);
        Assert.EndsWith(Environment.NewLine, content);
    }

    #endregion
}
