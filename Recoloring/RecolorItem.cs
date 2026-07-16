namespace ItemInfo.Recoloring;

public enum RecolorItemKind { Normal, Ammo, Armor, ArmoredRig, Rig, Backpack, Weapon }

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
    IReadOnlyList<(int Width, int Height)>? DirectGrids = null,
    string? Name = null,
    ProtectiveItemType? ProtectiveType = null);

public sealed record AmmunitionRecolorTemplate(
    string Id,
    string ParentId,
    int TraderTier,
    bool FleaBanned,
    double? BestTraderBuyValue,
    int? Width,
    int? Height,
    double? PenetrationPower);

public sealed record ContainerGridTemplate(int? CellsH, int? CellsV);

public sealed record ContainerRecolorTemplate(
    string Id,
    string ParentId,
    RecolorItemKind Kind,
    int TraderTier,
    bool FleaBanned,
    double? BestTraderBuyValue,
    int? Width,
    int? Height,
    IReadOnlyList<ContainerGridTemplate>? DirectGrids,
    bool HasDefaultArmorData = false);

public sealed record WeaponRecolorTemplate(
    string Id,
    string ParentId,
    int TraderTier,
    bool FleaBanned,
    double? BestTraderBuyValue,
    int? Width,
    int? Height);

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

    public static RecolorItem FromContainer(ContainerRecolorTemplate template) =>
        new(
            template.Id,
            template.ParentId,
            template.Kind == RecolorItemKind.Rig && template.HasDefaultArmorData
                ? RecolorItemKind.ArmoredRig
                : template.Kind,
            template.TraderTier,
            template.FleaBanned,
            template.BestTraderBuyValue,
            template.Width,
            template.Height,
            DirectGrids: template.DirectGrids?
                .Select(grid => (grid.CellsH ?? 0, grid.CellsV ?? 0))
                .ToArray());

    public static RecolorItem FromWeapon(WeaponRecolorTemplate template) =>
        new(
            template.Id,
            template.ParentId,
            RecolorItemKind.Weapon,
            template.TraderTier,
            template.FleaBanned,
            template.BestTraderBuyValue,
            template.Width,
            template.Height);
}

public enum RecolorContextualLabelKind { BackgroundBasis, TraderTier, PenetrationTier, CapacityTier, ArmorClass }

public sealed record RecolorResult(
    bool Recolored,
    RecolorTier? Tier,
    string? Warning = null,
    RecolorContextualLabelKind ContextualLabelKind = RecolorContextualLabelKind.BackgroundBasis);
