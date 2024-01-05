using Xunit;

namespace WpfDemo.Test;

public class UnitTest1
{
    [Theory]
    [InlineData("a", "A")]
    [InlineData("xYz", "XYZ")]
    [InlineData("1d3", "1D3")]
    [InlineData("", "")]
    [InlineData(null, null)]
    public void Test1(string? text, string? expected)
    {
        Assert.Equal(expected, Model.Helper.ToUpper(text));
    }
}