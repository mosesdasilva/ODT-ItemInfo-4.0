using ItemInfo.Recoloring;
using Xunit;

namespace ItemInfo.Tests;

public class StaticRecolorPassTests
{
    private static RecolorItem Item(RecolorItemKind kind = RecolorItemKind.Normal, int traderTier = 2) =>
        new("item", "parent", kind, traderTier, Width: 1, Height: 1);

    [Theory]
    [InlineData(9999, RecolorTier.Common)] [InlineData(10000, RecolorTier.Rare)]
    [InlineData(10001, RecolorTier.Rare)] [InlineData(14999, RecolorTier.Rare)]
    [InlineData(15000, RecolorTier.Epic)] [InlineData(20000, RecolorTier.Legendary)]
    [InlineData(15001, RecolorTier.Epic)] [InlineData(19999, RecolorTier.Epic)]
    [InlineData(20001, RecolorTier.Legendary)] [InlineData(39999, RecolorTier.Legendary)]
    [InlineData(40000, RecolorTier.Uber)] [InlineData(60000, RecolorTier.Unobtainium)]
    [InlineData(40001, RecolorTier.Uber)] [InlineData(59999, RecolorTier.Uber)]
    [InlineData(60001, RecolorTier.Unobtainium)]
    public void Trader_buy_value_exact_boundaries_enter_higher_tier(double value, RecolorTier expected)
    {
        var pass = new StaticRecolorPass(new() { UseTraderBuyPriceForRecolor = true }, RecolorThresholds.Defaults);
        Assert.Equal(expected, pass.Classify(Item() with { BestTraderBuyValue = value }).Tier);
    }

    [Fact]
    public void Trader_buy_value_is_normalized_by_inventory_footprint()
    {
        var pass = new StaticRecolorPass(new() { UseTraderBuyPriceForRecolor = true }, RecolorThresholds.Defaults);
        Assert.Equal(RecolorTier.Rare, pass.Classify(Item() with { BestTraderBuyValue = 40000, Width = 2, Height = 2 }).Tier);
    }

    [Theory]
    [InlineData(0, RecolorTier.Common)] [InlineData(10, RecolorTier.Common)]
    [InlineData(11, RecolorTier.Rare)] [InlineData(20, RecolorTier.Rare)]
    [InlineData(21, RecolorTier.Epic)] [InlineData(30, RecolorTier.Epic)]
    [InlineData(31, RecolorTier.Legendary)] [InlineData(40, RecolorTier.Legendary)]
    [InlineData(41, RecolorTier.Uber)] [InlineData(50, RecolorTier.Uber)]
    [InlineData(51, RecolorTier.Unobtainium)] [InlineData(60, RecolorTier.Unobtainium)]
    [InlineData(61, RecolorTier.Overpowered)]
    public void Ammunition_uses_penetration(double penetration, RecolorTier expected)
    {
        var pass = new StaticRecolorPass(new(), RecolorThresholds.Defaults);
        Assert.Equal(expected, pass.Classify(Item(RecolorItemKind.Ammo) with { Penetration = penetration }).Tier);
    }

    [Theory]
    [InlineData(1, RecolorTier.Common)] [InlineData(2, RecolorTier.Rare)]
    [InlineData(3, RecolorTier.Epic)] [InlineData(4, RecolorTier.Legendary)]
    [InlineData(5, RecolorTier.Uber)] [InlineData(6, RecolorTier.Unobtainium)]
    public void Armor_class_maps_directly(int armorClass, RecolorTier expected)
    {
        var pass = new StaticRecolorPass(new(), RecolorThresholds.Defaults);
        Assert.Equal(expected, pass.Classify(Item(RecolorItemKind.Armor) with { ArmorClass = armorClass }).Tier);
    }

    [Fact]
    public void Default_front_plate_wins_over_other_armor_data()
    {
        var pass = new StaticRecolorPass(new(), RecolorThresholds.Defaults);
        Assert.Equal(RecolorTier.Epic, pass.Classify(Item(RecolorItemKind.ArmoredRig) with { DefaultFrontPlateClass = 3, ArmorClass = 6 }).Tier);
    }

    [Fact]
    public void Soft_armor_class_is_used_when_no_default_front_plate_exists()
    {
        var result = new StaticRecolorPass(new(), RecolorThresholds.Defaults)
            .Classify(Item(RecolorItemKind.Armor) with { SoftArmorClass = 2 });
        Assert.Equal(RecolorTier.Rare, result.Tier);
    }

    [Fact]
    public void Missing_armor_data_warns_and_falls_back_to_basis()
    {
        var result = new StaticRecolorPass(new(), RecolorThresholds.Defaults)
            .Classify(Item(RecolorItemKind.Armor) with { ArmorClass = null });
        Assert.Equal(RecolorTier.Rare, result.Tier);
        Assert.Contains("armor", result.Warning);
    }

    [Fact]
    public void Rig_capacity_sums_every_direct_grid_and_ignores_shape_metadata()
    {
        var pass = new StaticRecolorPass(new(), RecolorThresholds.Defaults);
        Assert.Equal(RecolorTier.Epic, pass.Classify(Item(RecolorItemKind.Rig) with { DirectGrids = [(2, 4), (2, 4)] }).Tier);
    }

    [Theory]
    [InlineData(RecolorItemKind.Rig, 8, RecolorTier.Common)]
    [InlineData(RecolorItemKind.Rig, 9, RecolorTier.Rare)]
    [InlineData(RecolorItemKind.Rig, 12, RecolorTier.Rare)]
    [InlineData(RecolorItemKind.Rig, 13, RecolorTier.Epic)]
    [InlineData(RecolorItemKind.Rig, 16, RecolorTier.Epic)]
    [InlineData(RecolorItemKind.Rig, 17, RecolorTier.Legendary)]
    [InlineData(RecolorItemKind.Rig, 20, RecolorTier.Legendary)]
    [InlineData(RecolorItemKind.Rig, 21, RecolorTier.Uber)]
    [InlineData(RecolorItemKind.Rig, 24, RecolorTier.Uber)]
    [InlineData(RecolorItemKind.Rig, 25, RecolorTier.Unobtainium)]
    [InlineData(RecolorItemKind.Backpack, 12, RecolorTier.Common)]
    [InlineData(RecolorItemKind.Backpack, 13, RecolorTier.Rare)]
    [InlineData(RecolorItemKind.Backpack, 20, RecolorTier.Rare)]
    [InlineData(RecolorItemKind.Backpack, 21, RecolorTier.Epic)]
    [InlineData(RecolorItemKind.Backpack, 25, RecolorTier.Epic)]
    [InlineData(RecolorItemKind.Backpack, 26, RecolorTier.Legendary)]
    [InlineData(RecolorItemKind.Backpack, 30, RecolorTier.Legendary)]
    [InlineData(RecolorItemKind.Backpack, 31, RecolorTier.Uber)]
    [InlineData(RecolorItemKind.Backpack, 40, RecolorTier.Uber)]
    [InlineData(RecolorItemKind.Backpack, 41, RecolorTier.Unobtainium)]
    public void Capacity_exact_boundaries_are_inclusive(RecolorItemKind kind, int capacity, RecolorTier expected)
    {
        var result = new StaticRecolorPass(new(), RecolorThresholds.Defaults)
            .Classify(Item(kind) with { DirectGrids = [(capacity, 1)] });
        Assert.Equal(expected, result.Tier);
    }

    [Theory]
    [InlineData(RecolorItemKind.Ammo)]
    [InlineData(RecolorItemKind.Armor)]
    [InlineData(RecolorItemKind.ArmoredRig)]
    [InlineData(RecolorItemKind.Rig)]
    [InlineData(RecolorItemKind.Backpack)]
    public void Disabled_specialized_classifier_returns_category_to_selected_basis(RecolorItemKind kind)
    {
        var settings = new RecolorSettings
        {
            UsePenetrationForAmmoRecolor = false,
            UseArmorClassForRecolor = false,
            UseRigCapacityForRecolor = false,
            UseBackpackCapacityForRecolor = false
        };
        var result = new StaticRecolorPass(settings, RecolorThresholds.Defaults).Classify(
            Item(kind) with { Penetration = 61, ArmorClass = 6, DirectGrids = [(50, 1)] });
        Assert.Equal(RecolorTier.Rare, result.Tier);
    }

    [Fact]
    public void Precedence_is_blacklist_then_custom_then_flea_then_specialized_then_basis()
    {
        var pass = new StaticRecolorPass(new() { MarkFleaMarketBannedItemsAsOverpowered = true, UseTraderBuyPriceForRecolor = true }, RecolorThresholds.Defaults);
        var ammo = Item(RecolorItemKind.Ammo) with { FleaBanned = true, Penetration = 5, BestTraderBuyValue = 60000 };
        Assert.False(pass.Classify(ammo, blacklisted: true, custom: RecolorTier.Custom).Recolored);
        Assert.Equal(RecolorTier.Custom, pass.Classify(ammo, custom: RecolorTier.Custom).Tier);
        Assert.Equal(RecolorTier.Overpowered, pass.Classify(ammo).Tier);
        Assert.Equal(RecolorTier.Common, new StaticRecolorPass(new(), RecolorThresholds.Defaults).Classify(ammo).Tier);
    }

    [Fact]
    public void Disabled_flea_warning_does_not_change_trader_tier_background()
    {
        var banned = Item() with { FleaBanned = true };
        var result = new StaticRecolorPass(new() { MarkFleaMarketBannedItemsAsOverpowered = false }, RecolorThresholds.Defaults)
            .Classify(banned);
        Assert.Equal(RecolorTier.Rare, result.Tier);
    }

    [Fact]
    public void Invalid_specialized_data_warns_and_falls_back_without_throwing()
    {
        var result = new StaticRecolorPass(new(), RecolorThresholds.Defaults).Classify(Item(RecolorItemKind.Backpack));
        Assert.Equal(RecolorTier.Rare, result.Tier);
        Assert.Contains("item", result.Warning);
    }
}
