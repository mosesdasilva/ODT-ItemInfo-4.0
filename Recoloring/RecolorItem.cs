namespace ItemInfo.Recoloring;

public enum RecolorItemKind { Normal, Ammo, Armor, ArmoredRig, Rig, Backpack, Weapon }

public enum WeaponCategory
{
    Unknown,
    FlareSignal,
    Pistol,
    Revolver,
    SubmachineGun,
    Shotgun,
    AssaultCarbine,
    AssaultRifle,
    MachineGun,
    MarksmanRifle,
    SniperRifle,
    Launcher
}

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
    ProtectiveItemType? ProtectiveType = null,
    WeaponCategory? WeaponCategory = null,
    string? WeaponClass = null,
    int? RootSlotDefaultFrontPlateClass = null,
    HelmetArmorClassData? HelmetArmorClass = null);

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

public sealed record WeaponTemplateData(
    string Id,
    string ParentId,
    string? WeapClass,
    int TraderTier,
    bool FleaBanned,
    double? BestTraderBuyValue,
    int? Width,
    int? Height);

public static class WeaponRecolorItemAdapter
{
    private const string WeaponBaseClass = "5422acb9af1c889c16000029";
    private const string RocketLauncherBaseClass = "67446d4f04141c10630604e7";
    private const string RevolverBaseClass = "617f1ef5e8b54b0998387733";
    private const string GrenadeLauncherBaseClass = "5447bedf4bdc2d87278b4568";

    public static RecolorItem? Create(WeaponTemplateData template, Func<string, string?> getParentId)
    {
        var ancestry = EnumerateAncestry(template.ParentId, getParentId).ToHashSet(StringComparer.Ordinal);
        if (!ancestry.Contains(WeaponBaseClass))
            return null;

        var category = Classify(template.WeapClass, ancestry);
        return new(
            template.Id,
            template.ParentId,
            RecolorItemKind.Weapon,
            template.TraderTier,
            template.FleaBanned,
            template.BestTraderBuyValue,
            template.Width,
            template.Height,
            WeaponCategory: category,
            WeaponClass: template.WeapClass);
    }

    private static WeaponCategory Classify(string? weaponClass, IReadOnlySet<string> ancestry)
    {
        if (ancestry.Contains(RocketLauncherBaseClass))
            return WeaponCategory.Launcher;
        if (weaponClass == "grenadeLauncher")
            return WeaponCategory.Launcher;
        if (weaponClass == "pistol" && ancestry.Contains(RevolverBaseClass))
            return WeaponCategory.Revolver;

        var direct = weaponClass switch
        {
            "pistol" => WeaponCategory.Pistol,
            "smg" => WeaponCategory.SubmachineGun,
            "shotgun" => WeaponCategory.Shotgun,
            "assaultCarbine" => WeaponCategory.AssaultCarbine,
            "assaultRifle" => WeaponCategory.AssaultRifle,
            "machinegun" => WeaponCategory.MachineGun,
            "marksmanRifle" => WeaponCategory.MarksmanRifle,
            "sniperRifle" => WeaponCategory.SniperRifle,
            _ => WeaponCategory.Unknown
        };
        if (direct != WeaponCategory.Unknown)
            return direct;
        if (weaponClass == "specialWeapon" && ancestry.Contains(GrenadeLauncherBaseClass))
            return WeaponCategory.FlareSignal;
        return WeaponCategory.Unknown;
    }

    private static IEnumerable<string> EnumerateAncestry(string parentId, Func<string, string?> getParentId)
    {
        var current = parentId;
        var visited = new HashSet<string>(StringComparer.Ordinal);
        while (!string.IsNullOrEmpty(current) && visited.Add(current))
        {
            yield return current;
            current = getParentId(current);
        }
    }
}

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

public sealed record RecolorPresentationOverride(
    ColorSpecification? Color,
    string ContextualLabel,
    string ContextualLabelTranslationKey,
    bool IncludeTierNumber = false);

public sealed record RecolorResult(
    bool Recolored,
    RecolorTier? Tier,
    string? Warning = null,
    RecolorContextualLabelKind ContextualLabelKind = RecolorContextualLabelKind.BackgroundBasis,
    RecolorPresentationOverride? PresentationOverride = null);
