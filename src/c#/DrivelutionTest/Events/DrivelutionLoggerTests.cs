using GeneralUpdate.Drivelution.Abstractions.Events;

namespace DrivelutionTest.Events;

/// <summary>
/// DrivelutionLogger 测试
/// 分支覆盖点:
/// - Debug 方法触发事件
/// - Information 方法触发事件
/// - Warning 方法触发事件
/// - Error 方法触发事件
/// - Fatal 方法触发事件
/// - 带 args 的格式化消息
/// - 带 Exception 参数的日志
/// - LogMessage 事件为 null 时不抛异常
/// - RaiseLogEvent 内部异常处理不抛出自
/// 触发条件：订阅事件并调用日志方法
/// 预期结果：事件正确触发，包含正确数据
/// </summary>
public class DrivelutionLoggerTests
{
    [Fact(DisplayName = "DrivelutionLogger_Debug_触发LogMessage事件")]
    public void DrivelutionLogger_Debug_RaisesLogMessageEvent()
    {
        var logger = new DrivelutionLogger();
        LogEventArgs? captured = null;

        logger.LogMessage += (_, args) => captured = args;
        logger.Debug("debug message");

        Assert.NotNull(captured);
        Assert.Equal(LogLevel.Debug, captured!.Level);
        Assert.Equal("debug message", captured.Message);
    }

    [Fact(DisplayName = "DrivelutionLogger_Information_触发LogMessage事件")]
    public void DrivelutionLogger_Information_RaisesLogMessageEvent()
    {
        var logger = new DrivelutionLogger();
        LogEventArgs? captured = null;

        logger.LogMessage += (_, args) => captured = args;
        logger.Information("info message");

        Assert.NotNull(captured);
        Assert.Equal(LogLevel.Information, captured!.Level);
        Assert.Equal("info message", captured.Message);
    }

    [Fact(DisplayName = "DrivelutionLogger_Warning_触发LogMessage事件")]
    public void DrivelutionLogger_Warning_RaisesLogMessageEvent()
    {
        var logger = new DrivelutionLogger();
        LogEventArgs? captured = null;

        logger.LogMessage += (_, args) => captured = args;
        logger.Warning("warning message");

        Assert.NotNull(captured);
        Assert.Equal(LogLevel.Warning, captured!.Level);
    }

    [Fact(DisplayName = "DrivelutionLogger_Error_触发LogMessage事件")]
    public void DrivelutionLogger_Error_RaisesLogMessageEvent()
    {
        var logger = new DrivelutionLogger();
        LogEventArgs? captured = null;

        logger.LogMessage += (_, args) => captured = args;
        logger.Error("error message");

        Assert.NotNull(captured);
        Assert.Equal(LogLevel.Error, captured!.Level);
    }

    [Fact(DisplayName = "DrivelutionLogger_Fatal_触发LogMessage事件")]
    public void DrivelutionLogger_Fatal_RaisesLogMessageEvent()
    {
        var logger = new DrivelutionLogger();
        LogEventArgs? captured = null;

        logger.LogMessage += (_, args) => captured = args;
        logger.Fatal("fatal message");

        Assert.NotNull(captured);
        Assert.Equal(LogLevel.Fatal, captured!.Level);
    }

    [Fact(DisplayName = "DrivelutionLogger_带args参数_格式化消息")]
    public void DrivelutionLogger_WithArgs_FormatsMessage()
    {
        var logger = new DrivelutionLogger();
        LogEventArgs? captured = null;

        logger.LogMessage += (_, args) => captured = args;
        logger.Information("Hello {0}, you have {1} messages", null, "Juster", 5);

        Assert.NotNull(captured);
        Assert.Equal("Hello Juster, you have 5 messages", captured!.Message);
    }

    [Fact(DisplayName = "DrivelutionLogger_不带args_消息原样传递")]
    public void DrivelutionLogger_NoArgs_MessagePassedAsIs()
    {
        var logger = new DrivelutionLogger();
        LogEventArgs? captured = null;

        logger.LogMessage += (_, args) => captured = args;
        logger.Information("plain message");

        Assert.Equal("plain message", captured!.Message);
    }

    [Fact(DisplayName = "DrivelutionLogger_带Exception参数_传递异常")]
    public void DrivelutionLogger_WithException_PassesException()
    {
        var logger = new DrivelutionLogger();
        LogEventArgs? captured = null;
        var ex = new InvalidOperationException("test exception");

        logger.LogMessage += (_, args) => captured = args;
        logger.Error("error with exception", ex);

        Assert.NotNull(captured);
        Assert.Same(ex, captured!.Exception);
    }

    [Fact(DisplayName = "DrivelutionLogger_Timestamp_设置为当前UTC时间")]
    public void DrivelutionLogger_Timestamp_SetToUtcNow()
    {
        var logger = new DrivelutionLogger();
        LogEventArgs? captured = null;
        var before = DateTime.UtcNow.AddSeconds(-1);

        logger.LogMessage += (_, args) => captured = args;
        logger.Information("test");

        var after = DateTime.UtcNow.AddSeconds(1);
        Assert.NotNull(captured);
        Assert.InRange(captured!.Timestamp, before, after);
    }

    [Fact(DisplayName = "DrivelutionLogger_无订阅者_不抛异常")]
    public void DrivelutionLogger_NoSubscribers_DoesNotThrow()
    {
        var logger = new DrivelutionLogger();

        // These should not throw
        logger.Debug("test");
        logger.Information("test");
        logger.Warning("test");
        logger.Error("test");
        logger.Fatal("test");
    }

    [Fact(DisplayName = "DrivelutionLogger_多个订阅者_全部收到通知")]
    public void DrivelutionLogger_MultipleSubscribers_AllReceiveNotification()
    {
        var logger = new DrivelutionLogger();
        int count1 = 0, count2 = 0;

        logger.LogMessage += (_, _) => count1++;
        logger.LogMessage += (_, _) => count2++;

        logger.Information("test");

        Assert.Equal(1, count1);
        Assert.Equal(1, count2);
    }
}

/// <summary>
/// LogEventArgs 测试
/// 分支覆盖点:
/// - 默认构造函数属性默认值
/// - Level 枚举所有值
/// - Message 字符串
/// - Exception 可空
/// - Timestamp 默认 UTC
/// - Context 字典可空
/// 触发条件：创建 LogEventArgs
/// 预期结果：属性正确
/// </summary>
public class LogEventArgsTests
{
    [Fact(DisplayName = "LogEventArgs_默认构造函数_所有属性为默认值")]
    public void LogEventArgs_DefaultConstructor_AllPropertiesHaveDefaultValues()
    {
        var args = new LogEventArgs();

        Assert.Equal(default(LogLevel), args.Level);
        Assert.Equal(string.Empty, args.Message);
        Assert.Null(args.Exception);
        Assert.Null(args.Context);
    }

    [Fact(DisplayName = "LogEventArgs_Level_可设置所有级别")]
    public void LogEventArgs_Level_CanSetAllLevels()
    {
        foreach (LogLevel level in Enum.GetValues<LogLevel>())
        {
            var args = new LogEventArgs { Level = level };
            Assert.Equal(level, args.Level);
        }
    }

    [Fact(DisplayName = "LogEventArgs_Context_可设置字典")]
    public void LogEventArgs_Context_CanSetDictionary()
    {
        var context = new Dictionary<string, object>
        {
            ["key1"] = "value1",
            ["key2"] = 42
        };

        var args = new LogEventArgs { Context = context };

        Assert.Same(context, args.Context);
        Assert.Equal(2, args.Context.Count);
    }

    [Fact(DisplayName = "LogEventArgs_Context为null_接受")]
    public void LogEventArgs_ContextIsNull_Accepted()
    {
        var args = new LogEventArgs { Context = null };
        Assert.Null(args.Context);
    }

    [Fact(DisplayName = "LogEventArgs_Timestamp_默认为UtcNow")]
    public void LogEventArgs_Timestamp_DefaultsToUtcNow()
    {
        var args = new LogEventArgs();
        var before = DateTime.UtcNow.AddSeconds(-1);
        var after = DateTime.UtcNow.AddSeconds(1);

        Assert.InRange(args.Timestamp, before, after);
    }
}

/// <summary>
/// LogLevel 枚举测试
/// 分支覆盖点:
/// - 枚举值定义: Debug=0, Information=1, Warning=2, Error=3, Fatal=4
/// 触发条件：转换为 int
/// 预期结果：值正确
/// </summary>
public class LogLevelTests
{
    [Fact(DisplayName = "LogLevel_Debug值为0")]
    public void LogLevel_Debug_IsZero() => Assert.Equal(0, (int)LogLevel.Debug);

    [Fact(DisplayName = "LogLevel_Information值为1")]
    public void LogLevel_Information_IsOne() => Assert.Equal(1, (int)LogLevel.Information);

    [Fact(DisplayName = "LogLevel_Warning值为2")]
    public void LogLevel_Warning_IsTwo() => Assert.Equal(2, (int)LogLevel.Warning);

    [Fact(DisplayName = "LogLevel_Error值为3")]
    public void LogLevel_Error_IsThree() => Assert.Equal(3, (int)LogLevel.Error);

    [Fact(DisplayName = "LogLevel_Fatal值为4")]
    public void LogLevel_Fatal_IsFour() => Assert.Equal(4, (int)LogLevel.Fatal);
}
