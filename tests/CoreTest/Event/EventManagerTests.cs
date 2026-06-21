using GeneralUpdate.Core.Event;

namespace CoreTest.Event;

/// <summary>
/// Unit tests for <see cref="EventManager"/> following AAAT (Arrange-Act-Assert-TearDown).
/// Implements <see cref="IDisposable"/> for explicit TearDown — clears singleton state
/// after each test to ensure test isolation regardless of test execution order.
/// </summary>
[Collection("NonParallel_EventManager")]
public class EventManagerTests : IDisposable
{
    public class TestEventArgs : EventArgs
    {
        public int Value { get; set; }
    }

    /// <summary>TearDown: clear singleton state after each test for isolation.</summary>
    public void Dispose()
    {
        EventManager.Instance.Clear();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void AddListener_NullListener_ThrowsArgumentNullException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            EventManager.Instance.AddListener<TestEventArgs>(null!));
    }

    [Fact]
    public void AddListener_SingleListener_Registered()
    {
        // Arrange
        var called = false;
        Action<object, TestEventArgs> handler = (s, e) => called = true;

        // Act
        EventManager.Instance.AddListener(handler);
        EventManager.Instance.Dispatch(this, new TestEventArgs());

        // Assert
        Assert.True(called);
    }

    [Fact]
    public void Dispatch_NoListeners_DoesNotThrow()
    {
        // Arrange & Act
        var ex = Record.Exception(() =>
            EventManager.Instance.Dispatch(this, new TestEventArgs()));

        // Assert
        Assert.Null(ex);
    }

    [Fact]
    public void Dispatch_SenderNull_ThrowsArgumentNullException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            EventManager.Instance.Dispatch<TestEventArgs>(null!, new TestEventArgs()));
    }

    [Fact]
    public void Dispatch_EventArgsNull_ThrowsArgumentNullException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            EventManager.Instance.Dispatch<TestEventArgs>(this, null!));
    }

    [Fact]
    public void Dispatch_MultipleListeners_AllCalled()
    {
        // Arrange
        var count = 0;
        Action<object, TestEventArgs> h1 = (s, e) => count++;
        Action<object, TestEventArgs> h2 = (s, e) => count++;

        // Act
        EventManager.Instance.AddListener(h1);
        EventManager.Instance.AddListener(h2);
        EventManager.Instance.Dispatch(this, new TestEventArgs());

        // Assert
        Assert.Equal(2, count);
    }

    [Fact]
    public void Dispatch_OneHandlerThrows_OtherStillCalled()
    {
        // Arrange
        var secondCalled = false;
        Action<object, TestEventArgs> h1 = (s, e) => throw new InvalidOperationException("boom");
        Action<object, TestEventArgs> h2 = (s, e) => secondCalled = true;

        // Act
        EventManager.Instance.AddListener(h1);
        EventManager.Instance.AddListener(h2);
        EventManager.Instance.Dispatch(this, new TestEventArgs());

        // Assert
        Assert.True(secondCalled);
    }

    [Fact]
    public void RemoveListener_ExistingListener_Removed()
    {
        // Arrange
        var called = false;
        Action<object, TestEventArgs> handler = (s, e) => called = true;

        // Act
        EventManager.Instance.AddListener(handler);
        EventManager.Instance.RemoveListener(handler);
        EventManager.Instance.Dispatch(this, new TestEventArgs());

        // Assert
        Assert.False(called);
    }

    [Fact]
    public void RemoveListener_Null_ThrowsArgumentNullException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            EventManager.Instance.RemoveListener<TestEventArgs>(null!));
    }

    [Fact]
    public void RemoveListener_NotRegistered_DoesNotThrow()
    {
        // Arrange & Act
        var ex = Record.Exception(() =>
            EventManager.Instance.RemoveListener<TestEventArgs>((s, e) => { }));

        // Assert
        Assert.Null(ex);
    }

    [Fact]
    public void Clear_RemovesAllListeners()
    {
        // Arrange
        var called = false;
        EventManager.Instance.AddListener<TestEventArgs>((s, e) => called = true);

        // Act
        EventManager.Instance.Clear();
        EventManager.Instance.Dispatch(this, new TestEventArgs());

        // Assert
        Assert.False(called);
    }

    [Fact]
    public void Instance_ReturnsSameSingleton()
    {
        // Arrange & Act
        var a = EventManager.Instance;
        var b = EventManager.Instance;

        // Assert
        Assert.Same(a, b);
    }

    [Fact]
    public void Dispatch_PassesCorrectEventArgs()
    {
        // Arrange
        TestEventArgs? received = null;
        EventManager.Instance.AddListener<TestEventArgs>((s, e) => received = e);
        var args = new TestEventArgs { Value = 42 };

        // Act
        EventManager.Instance.Dispatch(this, args);

        // Assert
        Assert.Same(args, received);
        Assert.Equal(42, received!.Value);
    }
}
