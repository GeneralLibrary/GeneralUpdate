using GeneralUpdate.Bowl.Internal;

/// <summary>
/// 分支覆盖点：
/// EnvironmentProvider：
///   - GetVariable(name)：调用 Environments.GetEnvironmentVariable(name)
///   - SetVariable(name, value)：调用 Environments.SetEnvironmentVariable(name, value)
///   - GetVariable 返回 null（变量不存在）
///   - GetVariable 返回空字符串（变量存在但为空）
///   - SetVariable 后 GetVariable 能正确读取
///   - SetVariable 覆盖现有值
/// </summary>
public class EnvironmentProviderTests
{
    private const string TestVariableName = "BOWL_TEST_ENV_VAR";

    [Fact]
    public void GetVariable_变量不存在_返回null()
    {
        var provider = new EnvironmentProvider();
        // Ensure variable is not set
        Environment.SetEnvironmentVariable(TestVariableName, null, EnvironmentVariableTarget.Process);

        var value = provider.GetVariable(TestVariableName);
        Assert.Null(value);
    }

    [Fact]
    public void SetVariable_设置值后GetVariable正确返回()
    {
        var provider = new EnvironmentProvider();
        try
        {
            provider.SetVariable(TestVariableName, "test_value_123");
            var value = provider.GetVariable(TestVariableName);
            Assert.Equal("test_value_123", value);
        }
        finally
        {
            Environment.SetEnvironmentVariable(TestVariableName, null, EnvironmentVariableTarget.Process);
        }
    }

    [Fact]
    public void SetVariable_覆盖现有值_返回新值()
    {
        var provider = new EnvironmentProvider();
        try
        {
            provider.SetVariable(TestVariableName, "old_value");
            provider.SetVariable(TestVariableName, "new_value");
            var value = provider.GetVariable(TestVariableName);
            Assert.Equal("new_value", value);
        }
        finally
        {
            Environment.SetEnvironmentVariable(TestVariableName, null, EnvironmentVariableTarget.Process);
        }
    }

    [Fact]
    public void SetVariable_空字符串值_GetVariable返回空字符串()
    {
        var provider = new EnvironmentProvider();
        try
        {
            provider.SetVariable(TestVariableName, string.Empty);
            var value = provider.GetVariable(TestVariableName);
            Assert.Equal(string.Empty, value);
        }
        finally
        {
            Environment.SetEnvironmentVariable(TestVariableName, null, EnvironmentVariableTarget.Process);
        }
    }

    [Fact]
    public void SetVariable_长字符串值_正确返回()
    {
        var provider = new EnvironmentProvider();
        var longValue = new string('x', 1000);
        try
        {
            provider.SetVariable(TestVariableName, longValue);
            var value = provider.GetVariable(TestVariableName);
            Assert.Equal(longValue, value);
        }
        finally
        {
            Environment.SetEnvironmentVariable(TestVariableName, null, EnvironmentVariableTarget.Process);
        }
    }

    [Fact]
    public void SetVariable_含特殊字符_正确返回()
    {
        var provider = new EnvironmentProvider();
        var specialValue = "value with spaces and 中文 and !@#$%";
        try
        {
            provider.SetVariable(TestVariableName, specialValue);
            var value = provider.GetVariable(TestVariableName);
            Assert.Equal(specialValue, value);
        }
        finally
        {
            Environment.SetEnvironmentVariable(TestVariableName, null, EnvironmentVariableTarget.Process);
        }
    }

    [Fact]
    public void SetVariable_版本号字符串_正确返回()
    {
        var provider = new EnvironmentProvider();
        try
        {
            provider.SetVariable(TestVariableName, "10.0.0-preview.1");
            var value = provider.GetVariable(TestVariableName);
            Assert.Equal("10.0.0-preview.1", value);
        }
        finally
        {
            Environment.SetEnvironmentVariable(TestVariableName, null, EnvironmentVariableTarget.Process);
        }
    }
}
