namespace ItemInfo.Recoloring;

public sealed record RecolorSettings
{
    public bool UseTraderBuyPriceForRecolor { get; init; }
    public bool MarkFleaMarketBannedItemsAsOverpowered { get; init; }
    public bool UsePenetrationForAmmoRecolor { get; init; } = true;
    public bool UseArmorClassForRecolor { get; init; } = true;
    public bool UseRigCapacityForRecolor { get; init; } = true;
    public bool UseBackpackCapacityForRecolor { get; init; } = true;
    public WeaponRecolorMode WeaponRecolorMode { get; init; } = WeaponRecolorMode.Inherit;
}

public sealed record RecolorThresholds(
    double[] TraderBuyValue,
    double[] AmmoPenetration,
    double[] RigCapacity,
    double[] BackpackCapacity)
{
    public static RecolorThresholds Defaults { get; } = new(
        [10000, 15000, 20000, 40000, 60000],
        [20, 30, 40, 50, 60],
        [8, 12, 16, 20, 24],
        [12, 20, 25, 30, 40]);
}
