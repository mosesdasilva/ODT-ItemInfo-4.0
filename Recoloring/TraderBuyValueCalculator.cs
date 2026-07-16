namespace ItemInfo.Recoloring;

public sealed record TraderBuyOffer(
    string TraderId,
    string TraderName,
    bool IsFence,
    bool AcceptsItem,
    bool ProhibitsItem,
    double OfferInTraderCurrency,
    double CurrencyRoubleValue);

public sealed record BestTraderBuyValue(double Roubles, string TraderName)
{
    public static BestTraderBuyValue Unsellable { get; } = new(0, "None");
}

public sealed record BaseWeaponTraderOffers(
    IReadOnlyList<TraderBuyOffer> BaseTemplateOffers,
    IReadOnlyList<TraderBuyOffer> DefaultPresetComponentOffers);

public static class TraderBuyValueCalculator
{
    public static BestTraderBuyValue SelectBaseWeaponValue(BaseWeaponTraderOffers offers) =>
        SelectHighest(offers.BaseTemplateOffers);

    public static BestTraderBuyValue SelectHighest(IEnumerable<TraderBuyOffer> offers)
    {
        return offers
            .Where(offer => !offer.IsFence && offer.AcceptsItem && !offer.ProhibitsItem)
            .Where(offer => offer.OfferInTraderCurrency >= 0 && offer.CurrencyRoubleValue > 0)
            .Select(offer => new BestTraderBuyValue(
                offer.OfferInTraderCurrency * offer.CurrencyRoubleValue,
                offer.TraderName))
            .DefaultIfEmpty(BestTraderBuyValue.Unsellable)
            .MaxBy(offer => offer.Roubles)!;
    }
}
