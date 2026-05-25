using GeneralUpdate.Core.Event;

namespace CoreTest.Event;

public class EventManagerTests
{
    public class TestEventArgs : EventArgs
    {
        public int Value { get; set; }
    }

    [Fact]
    public void AddListener_NullListener_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            EventManager.Instance.AddListener<TestEventArgs>(null));
    }

    [Fact]
    public void AddListener_SingleListener_Registered()
    {
        var called = false;
        Action<object, TestEventArgs> handler = (s, e) => called = true;
        EventManager.Instance.AddListener(handler);
        EventManager.Instance.Dispatch(this, new TestEventArgs());
        Assert.True(called);
        EventManager.Instance.Clear();
    }

    [Fact]
    public void Dispatch_NoListeners_DoesNotThrow()
    {
        var ex = Record.Exception(() =>
            EventManager.Instance.Dispatch(this, new TestEventArgs()));
        Assert.Null(ex);
    }

    [Fact]
    public void Dispatch_SenderNull_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            EventManager.Instance.Dispatch<TestEventArgs>(null, new TestEventArgs()));
    }

    [Fact]
    public void Dispatch_EventArgsNull_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            EventManager.Instance.Dispatch<TestEventArgs>(this, null));
    }

    [Fact]
    public void Dispatch_MultipleListeners_AllCalled()
    {
        var count = 0;
        Action<object, TestEventArgs> h1 = (s, e) => count++;
        Action<object, TestEventArgs> h2 = (s, e) => count++;
        EventManager.Instance.AddListener(h1);
        EventManager.Instance.AddListener(h2);
        EventManager.Instance.Dispatch(this, new TestEventArgs());
        Assert.Equal(2, count);
        EventManager.Instance.Clear();
    }

    [Fact]
    public void Dispatch_OneHandlerThrows_OtherStillCalled()
    {
        var secondCalled = false;
        Action<object, TestEventArgs> h1 = (s, e) => throw new InvalidOperationException("boom");
        Action<object, TestEventArgs> h2 = (s, e) => secondCalled = true;
        EventManager.Instance.AddListener(h1);
        EventManager.Instance.AddListener(h2);
        EventManager.Instance.Dispatch(this, new TestEventArgs());
        Assert.True(secondCalled);
        EventManager.Instance.Clear();
    }

    [Fact]
    public void RemoveListener_ExistingListener_Removed()
    {
        var called = false;
        Action<object, TestEventArgs> handler = (s, e) => called = true;
        EventManager.Instance.AddListener(handler);
        EventManager.Instance.RemoveListener(handler);
        EventManager.Instance.Dispatch(this, new TestEventArgs());
        Assert.False(called);
        EventManager.Instance.Clear();
    }

    [Fact]
    public void RemoveListener_Null_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            EventManager.Instance.RemoveListener<TestEventArgs>(null));
    }

    [Fact]
    public void RemoveListener_NotRegistered_DoesNotThrow()
    {
        var ex = Record.Exception(() =>
            EventManager.Instance.RemoveListener<TestEventArgs>((s, e) => { }));
        Assert.Null(ex);
    }

    [Fact]
    public void Clear_RemovesAllListeners()
    {
        var called = false;
        EventManager.Instance.AddListener<TestEventArgs>((s, e) => called = true);
        EventManager.Instance.Clear();
        EventManager.Instance.Dispatch(this, new TestEventArgs());
        Assert.False(called);
    }

    [Fact]
    public void Instance_ReturnsSameSingleton()
    {
        var a = EventManager.Instance;
        var b = EventManager.Instance;
        Assert.Same(a, b);
    }

    [Fact]
    public void Dispatch_PassesCorrectEventArgs()
    {
        TestEventArgs received = null;
        EventManager.Instance.AddListener<TestEventArgs>((s, e) => received = e);
        var args = new TestEventArgs { Value = 42 };
        EventManager.Instance.Dispatch(this, args);
        Assert.Same(args, received);
        Assert.Equal(42, received.Value);
        EventManager.Instance.Clear();
    }
}
