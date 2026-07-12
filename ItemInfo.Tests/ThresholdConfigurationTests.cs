using ItemInfo.Recoloring;
using Xunit;

namespace ItemInfo.Tests;

public class ThresholdConfigurationTests
{
    [Fact]
    public void Invalid_section_uses_its_default_without_replacing_valid_sibling()
    {
        var warnings = new List<string>();
        var result = ThresholdConfiguration.Load("""{"TRADER_BUY_VALUE":[1,1,2,3,4],"AMMO_PENETRATION":[1,2,3,4,5,7],"RIG_CAPACITY":[1,2,3,4,5],"BACKPACK_CAPACITY":[1,2,3,4,5]}""", warnings.Add);
        Assert.Equal(RecolorThresholds.Defaults.TraderBuyValue, result.TraderBuyValue);
        Assert.Equal([1d,2,3,4,5,7], result.AmmoPenetration);
        Assert.Single(warnings, warning => warning.Contains("TRADER_BUY_VALUE"));
    }

	[Fact]
	public void Malformed_json_warns_and_uses_defaults_without_throwing()
	{
		var warnings = new List<string>();
		var result = ThresholdConfiguration.Load("{", warnings.Add);
		Assert.Same(RecolorThresholds.Defaults, result);
		Assert.Single(warnings);
	}

    [Theory]
    [InlineData("null")]
    [InlineData("[1,2]")]
    [InlineData("[1,2,\"bad\",4,5]")]
    [InlineData("[1,2,2,4,5]")]
    [InlineData("[5,4,3,2,1]")]
    public void Invalid_trader_threshold_section_falls_back_independently(string section)
    {
        var warnings = new List<string>();
        var json = $$"""{"TRADER_BUY_VALUE":{{section}},"AMMO_PENETRATION":[1,2,3,4,5,6],"RIG_CAPACITY":[1,2,3,4,5],"BACKPACK_CAPACITY":[1,2,3,4,5]}""";
        var result = ThresholdConfiguration.Load(json, warnings.Add);
        Assert.Equal(RecolorThresholds.Defaults.TraderBuyValue, result.TraderBuyValue);
        Assert.Equal([1d,2,3,4,5,6], result.AmmoPenetration);
        Assert.Single(warnings, warning => warning.Contains("TRADER_BUY_VALUE"));
    }

    [Theory]
    [InlineData("AMMO_PENETRATION")]
    [InlineData("RIG_CAPACITY")]
    [InlineData("BACKPACK_CAPACITY")]
    public void Every_specialized_section_falls_back_without_replacing_valid_siblings(string invalidSection)
    {
        var warnings = new List<string>();
        var trader = invalidSection == "TRADER_BUY_VALUE" ? "[2,1]" : "[1,2,3,4,5]";
        var ammo = invalidSection == "AMMO_PENETRATION" ? "[2,1]" : "[1,2,3,4,5,6]";
        var rig = invalidSection == "RIG_CAPACITY" ? "[2,1]" : "[1,2,3,4,5]";
        var backpack = invalidSection == "BACKPACK_CAPACITY" ? "[2,1]" : "[1,2,3,4,5]";
        var json = $$"""{"TRADER_BUY_VALUE":{{trader}},"AMMO_PENETRATION":{{ammo}},"RIG_CAPACITY":{{rig}},"BACKPACK_CAPACITY":{{backpack}}}""";

        var result = ThresholdConfiguration.Load(json, warnings.Add);

        Assert.Single(warnings, warning => warning.Contains(invalidSection));
        Assert.Equal([1d,2,3,4,5], result.TraderBuyValue);
        if (invalidSection != "AMMO_PENETRATION") Assert.Equal([1d,2,3,4,5,6], result.AmmoPenetration);
        if (invalidSection != "RIG_CAPACITY") Assert.Equal([1d,2,3,4,5], result.RigCapacity);
        if (invalidSection != "BACKPACK_CAPACITY") Assert.Equal([1d,2,3,4,5], result.BackpackCapacity);
    }
}
