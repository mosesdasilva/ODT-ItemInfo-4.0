namespace ItemInfo.Recoloring;

public sealed class StaticRecolorPass(RecolorSettings settings, RecolorThresholds thresholds)
{
    public IReadOnlyList<RecolorResult> Run(IEnumerable<StaticRecolorRequest> requests, Action<string> warn)
    {
        var results = new List<RecolorResult>();
        foreach (var request in requests)
		    results.Add(Apply(request, warn));
        return results;
    }

    public RecolorResult Apply(StaticRecolorRequest request, Action<string> warn)
    {
	    var result = Classify(request.Item, request.Blacklisted, request.Custom);
	    if (result.Tier is not null)
		    request.ApplyTier(result.Tier.Value);
	    if (result.Warning is not null)
		    warn(result.Warning);
	    return result;
    }

    public RecolorResult Classify(RecolorItem item, bool blacklisted = false, RecolorTier? custom = null)
    {
        if (blacklisted) return new(false, null);
        if (custom is not null) return new(true, custom);
        if (settings.MarkFleaMarketBannedItemsAsOverpowered && item.FleaBanned)
            return new(true, RecolorTier.Overpowered);

        var specialized = ClassifySpecialized(item);
        return specialized ?? ClassifyNormal(item);
    }

    private RecolorResult? ClassifySpecialized(RecolorItem item) => item.Kind switch
    {
        RecolorItemKind.Ammo when settings.UsePenetrationForAmmoRecolor =>
            item.Penetration is >= 0 ? new(true, TierByUpperBounds(item.Penetration.Value, thresholds.AmmoPenetration, true)) : Fallback(item, "ammunition penetration"),
        RecolorItemKind.Armor or RecolorItemKind.ArmoredRig when settings.UseArmorClassForRecolor => ClassifyArmor(item),
        RecolorItemKind.Rig when settings.UseRigCapacityForRecolor => ClassifyCapacity(item, thresholds.RigCapacity, "rig capacity"),
        RecolorItemKind.Backpack when settings.UseBackpackCapacityForRecolor => ClassifyCapacity(item, thresholds.BackpackCapacity, "backpack capacity"),
        _ => null
    };

    private RecolorResult ClassifyArmor(RecolorItem item)
    {
        var armorClass = item.DefaultFrontPlateClass ?? item.SoftArmorClass ?? item.ArmorClass;
        return armorClass is >= 1 and <= 6
            ? new(true, (RecolorTier)armorClass.Value)
            : Fallback(item, "armor class or default front plate");
    }

    private RecolorResult ClassifyCapacity(RecolorItem item, double[] bounds, string field)
    {
        if (item.DirectGrids is null || item.DirectGrids.Count == 0 || item.DirectGrids.Any(g => g.Width <= 0 || g.Height <= 0))
            return Fallback(item, field);
        var capacity = item.DirectGrids.Sum(g => g.Width * g.Height);
        return new(true, TierByUpperBounds(capacity, bounds));
    }

    private RecolorResult ClassifyNormal(RecolorItem item)
    {
        if (!settings.UseTraderBuyPriceForRecolor)
		    return new(true, TraderTier(item));
        if (item.Width is not > 0 || item.Height is not > 0)
            return new(true, TraderTier(item), $"[ItemInfo] Item {item.Id} has invalid Inventory Footprint; using Trader Tier.");
        var perSlot = Math.Max(0, item.BestTraderBuyValue ?? 0) / (item.Width.Value * item.Height.Value);
        return new(true, TierByLowerBounds(perSlot, thresholds.TraderBuyValue));
    }

    private RecolorResult Fallback(RecolorItem item, string missing)
    {
        var normal = ClassifyNormal(item);
        return normal with { Warning = $"[ItemInfo] Item {item.Id} has missing or invalid {missing}; using the selected Background Recolor Basis." };
    }

    private static RecolorTier TraderTier(RecolorItem item) =>
        item.TraderTier is >= 1 and <= 6 ? (RecolorTier)item.TraderTier : RecolorTier.Custom2;

    private static RecolorTier TierByLowerBounds(double value, double[] bounds)
    {
        var index = Array.FindIndex(bounds, cutoff => value < cutoff);
        return (RecolorTier)(index < 0 ? 6 : index + 1);
    }

    private static RecolorTier TierByUpperBounds(double value, double[] bounds, bool overpoweredAboveLast = false)
    {
        var index = Array.FindIndex(bounds, cutoff => value <= cutoff);
        if (index >= 0) return (RecolorTier)(index + 1);
        return overpoweredAboveLast ? RecolorTier.Overpowered : RecolorTier.Unobtainium;
    }
}

public sealed record StaticRecolorRequest(
    RecolorItem Item,
    Action<RecolorTier> ApplyTier,
    bool Blacklisted = false,
    RecolorTier? Custom = null);
