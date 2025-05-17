using ES.FX.Primitives;

namespace ES.FX.Tests.Primitives;

public class DurationValueTests
{

    [Fact]
    public void DurationValue_CanBe_Equal()
    {
        var a = new DurationValue(1, DurationUnit.Day);
        var b = new DurationValue(1, DurationUnit.Day);

        Assert.Equal(a, b);
        Assert.True(a.Equals(b));
    }

    [Fact]
    public void DurationValue_CanBe_Different()
    {
        var a = new DurationValue(1, DurationUnit.Day);
        var b = new DurationValue(1, DurationUnit.Month);

        Assert.NotEqual(a, b);
    }


    [Fact]
    public void DurationValue_CanBe_Compared_SameUnit()
    {
        var a = new DurationValue(1, DurationUnit.Day);
        var b = new DurationValue(2, DurationUnit.Day);

        Assert.True(b > a);
    }


    [Fact]
    public void DurationValue_CannotBe_Compared_DifferentUnit()
    {
        var a = new DurationValue(1, DurationUnit.Day);
        var b = new DurationValue(2, DurationUnit.Month);

        try
        {
            Assert.True(b > a);
        }
        catch (Exception e)
        {
            Assert.True(e is InvalidOperationException);
        }
    }
}