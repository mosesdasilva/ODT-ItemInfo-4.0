namespace ItemInfo.Recoloring;

public enum RecolorItemKind { Normal, Ammo, Armor, ArmoredRig, Rig, Backpack }

public sealed record RecolorItem(
    string Id,
    string ParentId,
    RecolorItemKind Kind,
    int TraderTier,
    bool FleaBanned = false,
    double? BestTraderBuyValue = null,
    int? Width = null,
    int? Height = null,
    double? Penetration = null,
    int? ArmorClass = null,
    int? SoftArmorClass = null,
    int? DefaultFrontPlateClass = null,
    IReadOnlyList<(int Width, int Height)>? DirectGrids = null);

public sealed record AmmunitionRecolorTemplate(
    string Id,
    string ParentId,
    int TraderTier,
    bool FleaBanned,
    double? BestTraderBuyValue,
    int? Width,
    int? Height,
    double? PenetrationPower);

public static class RecolorItemAdapter
{
    public static RecolorItem FromAmmunition(AmmunitionRecolorTemplate template) =>
        new(
            template.Id,
            template.ParentId,
            RecolorItemKind.Ammo,
            template.TraderTier,
            template.FleaBanned,
            template.BestTraderBuyValue,
            template.Width,
            template.Height,
            template.PenetrationPower);

}

public enum RecolorContextualLabelKind { BackgroundBasis, PenetrationTier }

public sealed record RecolorResult(
    bool Recolored,
    RecolorTier? Tier,
    string? Warning = null,
    RecolorContextualLabelKind ContextualLabelKind = RecolorContextualLabelKind.BackgroundBasis);
