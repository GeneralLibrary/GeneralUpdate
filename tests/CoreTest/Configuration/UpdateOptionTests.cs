using GeneralUpdate.Core.Configuration;

namespace CoreTest.Configuration;

public class OptionTests
{
    [Fact]
    public void ValueOf_NewOption_CreatesWithDefaultValue()
    {
        var option = Option.ValueOf("TEST_KEY_001", 42);
        Assert.Equal("TEST_KEY_001", option.Name);
        Assert.Equal(42, option.DefaultValue);
    }

    [Fact]
    public void ValueOf_SameName_ReturnsSameSingletonInstance()
    {
        var opt1 = Option.ValueOf("SINGLETON_KEY", 100);
        var opt2 = Option.ValueOf("SINGLETON_KEY", 200);
        Assert.Same(opt1, opt2);
        Assert.Equal(100, opt1.DefaultValue); // First created value preserved
    }

    [Fact]
    public void ValueOf_NameNull_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => Option.ValueOf<string>(null));
    }

    [Fact]
    public void Equals_SameName_ReturnsTrue()
    {
        var opt1 = Option.ValueOf("EQ_TEST", 1);
        var opt2 = Option.ValueOf("EQ_TEST", 2);
        Assert.True(opt1.Equals(opt2));
    }

    [Fact]
    public void Equals_DifferentName_ReturnsFalse()
    {
        var opt1 = Option.ValueOf("KEY_A", 1);
        var opt2 = Option.ValueOf("KEY_B", 1);
        Assert.False(opt1.Equals(opt2));
    }

    [Fact]
    public void Equals_Null_ReturnsFalse()
    {
        var opt = Option.ValueOf("KEY", 1);
        Assert.False(opt.Equals(null));
    }

    [Fact]
    public void Equals_DifferentType_ReturnsFalse()
    {
        var opt = Option.ValueOf("KEY", 1);
        Assert.False(opt.Equals("not an option"));
    }

    [Fact]
    public void GetHashCode_SameName_SameHash()
    {
        var opt1 = Option.ValueOf("HASH_TEST", 1);
        var opt2 = Option.ValueOf("HASH_TEST", 999);
        Assert.Equal(opt1.GetHashCode(), opt2.GetHashCode());
    }

    [Fact]
    public void ToString_ReturnsName()
    {
        var opt = Option.ValueOf("MY_OPTION", 42);
        Assert.Equal("MY_OPTION", opt.ToString());
    }

    [Fact]
    public void DefaultValue_NullableBool_DefaultNull()
    {
        var opt = Option.ValueOf<bool?>("NULLABLE_BOOL");
        Assert.Null(opt.DefaultValue);
    }

    [Fact]
    public void DefaultValue_String_DefaultNull()
    {
        var opt = Option.ValueOf<string>("STR_KEY");
        Assert.Null(opt.DefaultValue);
    }

    [Fact]
    public void ValueOf_DifferentTypes_SameName_PossibleConflict()
    {
        var intOpt = Option.ValueOf("MULTI_TYPE", 42);
        var strOpt = Option.ValueOf<string>("MULTI_TYPE", "hello");
        // Same name but different generic type — registry key collision test
        Assert.Equal("MULTI_TYPE", intOpt.Name);
    }
}
