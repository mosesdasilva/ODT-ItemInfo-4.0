using ItemInfo.Recoloring;
using Xunit;

namespace ItemInfo.Tests;

public class TraderBuyValueCalculatorTests
{
    [Fact]
    public void Highest_normalized_eligible_offer_wins_across_built_in_and_modded_traders()
    {
        TraderBuyOffer[] offers =
        [
            new("prapor", "Prapor", false, true, false, 20_000, 1),
            new("peacekeeper", "Peacekeeper", false, true, false, 250, 100),
            new("modded", "Lotus", false, true, false, 30, 1_000)
        ];
        var result = TraderBuyValueCalculator.SelectHighest(offers);
        Assert.Equal(30_000, result.Roubles);
        Assert.Equal("Lotus", result.TraderName);
    }

    [Fact]
    public void Fence_prohibited_and_nonaccepting_high_offers_are_excluded()
    {
        TraderBuyOffer[] offers =
        [
            new("fence", "Fence", true, true, false, 100_000, 1),
            new("blocked", "Blocked", false, true, true, 90_000, 1),
            new("wrong-category", "Wrong", false, false, false, 80_000, 1),
            new("valid", "Valid", false, true, false, 10_000, 1)
        ];
        Assert.Equal(10_000, TraderBuyValueCalculator.SelectHighest(offers).Roubles);
    }

    [Fact]
    public void No_eligible_offer_is_unsellable_zero()
    {
        Assert.Equal(BestTraderBuyValue.Unsellable, TraderBuyValueCalculator.SelectHighest([]));
    }
}
