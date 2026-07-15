using ItemInfo.Recoloring;
using Xunit;

namespace ItemInfo.Tests;

public class ColorSpecificationTests
{
    [Fact]
    public void Native_name_is_canonicalized_and_projects_independently()
    {
        Assert.True(ColorSpecification.TryParse("TRACERgreen", out var color));
        Assert.Equal("tracerGreen", color.BackgroundValue);
        Assert.Equal("#75FF81", color.RichTextRgb);
    }

    [Theory]
    [InlineData("blue", "blue", "#1C4156")]
    [InlineData("YELLOW", "yellow", "#686628")]
    [InlineData("green", "green", "#152D00")]
    [InlineData("red", "red", "#6D2418")]
    [InlineData("black", "black", "#000000")]
    [InlineData("grey", "grey", "#1D1D1D")]
    [InlineData("violet", "violet", "#4C2A55")]
    [InlineData("orange", "orange", "#3C1900")]
    [InlineData("tracerYellow", "tracerYellow", "#FFFF92")]
    [InlineData("tracerGreen", "tracerGreen", "#75FF81")]
    [InlineData("tracerRed", "tracerRed", "#FF3C3C")]
    [InlineData("default", "default", "#7F7F7F")]
    [InlineData("#F90", "#FF9900", "#FF9900")]
    [InlineData("#a1b2c3", "#A1B2C3", "#A1B2C3")]
    public void Supported_values_normalize_to_background_and_rich_text(string input, string background, string richText)
    {
        Assert.True(ColorSpecification.TryParse(input, out var color));
        Assert.Equal(background, color.BackgroundValue);
        Assert.Equal(richText, color.RichTextRgb);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" blue")]
    [InlineData("blue ")]
    [InlineData("gray")]
    [InlineData("purple")]
    [InlineData("F90")]
    [InlineData("#RGBA")]
    [InlineData("#11223344")]
    [InlineData("#12")]
    [InlineData("#GGG")]
    public void Unsupported_values_are_rejected(string? input)
    {
        Assert.False(ColorSpecification.TryParse(input, out _));
    }
}
