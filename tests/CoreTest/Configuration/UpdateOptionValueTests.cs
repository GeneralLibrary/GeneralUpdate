using GeneralUpdate.Core.Configuration;

namespace CoreTest.Configuration;

/// <summary>
/// AAAT unit tests for <see cref="OptionValue{T}"/> — the strongly-typed option value wrapper.
/// Covers: construction, Option property, GetValue, ToString (normal/edge/null), value semantics.
/// </summary>
public class OptionValueTests
{
    #region Construction & Basic Properties

    [Fact]
    public void Ctor_IntValue_StoresCorrectly()
    {
        var option = Option.ValueOf("INT_KEY_UV", 0);
        var value = new OptionValue<int>(option, 42);

        Assert.Same(option, value.Option);
        Assert.Equal(42, value.GetValue());
    }

    [Fact]
    public void Ctor_StringValue_StoresCorrectly()
    {
        var option = Option.ValueOf<string>("STR_KEY");
        var value = new OptionValue<string>(option, "hello");

        Assert.Same(option, value.Option);
        Assert.Equal("hello", value.GetValue());
    }

    [Fact]
    public void Ctor_NullableBool_StoresCorrectly()
    {
        var option = Option.ValueOf<bool?>("BOOL_KEY");
        var value = new OptionValue<bool?>(option, true);

        Assert.Equal(true, value.GetValue());
    }

    [Fact]
    public void Ctor_NullReferenceType_StoresCorrectly()
    {
        var option = Option.ValueOf<string>("NULL_KEY");
        var value = new OptionValue<string>(option, null!);

        Assert.Null(value.GetValue());
    }

    [Fact]
    public void Ctor_DefaultValueType_StoresCorrectly()
    {
        var option = Option.ValueOf<int>("DEFAULT_INT");
        var value = new OptionValue<int>(option, default);

        Assert.Equal(0, value.GetValue());
    }

    #endregion

    #region GetValue returns correct type

    [Fact]
    public void GetValue_Int_ReturnsBoxedInt()
    {
        var option = Option.ValueOf("BOXED_INT", 0);
        var value = new OptionValue<int>(option, 99);

        var result = value.GetValue();

        Assert.IsType<int>(result);
        Assert.Equal(99, result);
    }

    [Fact]
    public void GetValue_BaseClass_ReturnsBoxedValue()
    {
        var option = Option.ValueOf("BASE_CLASS", 0);
        var value = new OptionValue<int>(option, 7);
        OptionValue baseRef = value;

        var result = baseRef.GetValue();

        Assert.IsType<int>(result);
        Assert.Equal(7, result);
    }

    #endregion

    #region ToString

    [Fact]
    public void ToString_NonNullValue_ReturnsValueToString()
    {
        var option = Option.ValueOf("TOSTR1", 0);
        var value = new OptionValue<int>(option, 42);

        Assert.Equal("42", value.ToString());
    }

    [Fact]
    public void ToString_NullValue_ReturnsEmptyString()
    {
        var option = Option.ValueOf<string>("TOSTR2");
        var value = new OptionValue<string>(option, null!);

        Assert.Equal(string.Empty, value.ToString());
    }

    [Fact]
    public void ToString_EmptyStringValue_ReturnsEmptyString()
    {
        var option = Option.ValueOf<string>("TOSTR3");
        var value = new OptionValue<string>(option, string.Empty);

        Assert.Equal(string.Empty, value.ToString());
    }

    [Fact]
    public void ToString_DateTimeValue_ReturnsFormattedDateTime()
    {
        var option = Option.ValueOf<DateTime>("TOSTR_DT");
        var dt = new DateTime(2024, 6, 15, 0, 0, 0, DateTimeKind.Utc);
        var value = new OptionValue<DateTime>(option, dt);

        var result = value.ToString();

        Assert.Contains("2024", result);
    }

    [Fact]
    public void ToString_BooleanValue_ReturnsTrueOrFalse()
    {
        var option = Option.ValueOf<bool>("TOSTR_BOOL");
        var trueVal = new OptionValue<bool>(option, true);
        var falseVal = new OptionValue<bool>(option, false);

        Assert.Equal("True", trueVal.ToString());
        Assert.Equal("False", falseVal.ToString());
    }

    #endregion

    #region Option property validation

    [Fact]
    public void Option_ReturnsTheSameOptionInstance()
    {
        var option1 = Option.ValueOf("OPT_CHECK_1", 1);
        var option2 = Option.ValueOf("OPT_CHECK_2", 2);
        var value1 = new OptionValue<int>(option1, 100);
        var value2 = new OptionValue<int>(option2, 200);

        Assert.Same(option1, value1.Option);
        Assert.Same(option2, value2.Option);
        Assert.NotSame(value1.Option, value2.Option);
    }

    #endregion

    #region Edge cases

    [Fact]
    public void Ctor_MaxInt_StoresCorrectly()
    {
        var option = Option.ValueOf("MAX_INT", 0);
        var value = new OptionValue<int>(option, int.MaxValue);

        Assert.Equal(int.MaxValue, value.GetValue());
    }

    [Fact]
    public void Ctor_MinInt_StoresCorrectly()
    {
        var option = Option.ValueOf("MIN_INT", 0);
        var value = new OptionValue<int>(option, int.MinValue);

        Assert.Equal(int.MinValue, value.GetValue());
    }

    [Fact]
    public void Ctor_LongString_StoresCorrectly()
    {
        var option = Option.ValueOf<string>("LONG_STR");
        var longStr = new string('x', 10000);
        var value = new OptionValue<string>(option, longStr);

        Assert.Equal(longStr, value.GetValue());
    }

    [Fact]
    public void Ctor_DoubleNaN_StoresCorrectly()
    {
        var option = Option.ValueOf<double>("NAN_KEY");
        var value = new OptionValue<double>(option, double.NaN);

        Assert.True(double.IsNaN((double)value.GetValue()));
    }

    #endregion
}
