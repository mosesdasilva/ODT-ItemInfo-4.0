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

public sealed record RecolorResult(bool Recolored, RecolorTier? Tier, string? Warning = null);
