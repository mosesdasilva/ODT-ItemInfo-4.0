namespace ItemInfo.Recoloring;

public sealed class StaticRecolorPass
{
    private readonly RecolorSettings settings;
    private readonly RecolorThresholds thresholds;
    private readonly IReadOnlyList<ColorSpecification>? tierColors;
    private readonly HashSet<string> warnedUnknownWeaponIds = new(StringComparer.Ordinal);

    public StaticRecolorPass(
        RecolorSettings settings,
        RecolorThresholds thresholds,
        IReadOnlyList<ColorSpecification>? tierColors = null)
    {
        this.settings = settings;
        this.thresholds = thresholds;
        this.tierColors = tierColors;
    }

    public StaticRecolorPass(RecolorConfiguration configuration)
        : this(
            new()
            {
                UseTraderBuyPriceForRecolor = configuration.UseTraderBuyPriceForRecolor,
                UsePenetrationForAmmoRecolor = configuration.SpecializedClassifiers.Ammunition.Enabled,
                UseArmorClassForRecolor = configuration.SpecializedClassifiers.ProtectiveItems.Enabled,
                UseRigCapacityForRecolor = configuration.SpecializedClassifiers.UnarmoredRigs.Enabled,
                UseBackpackCapacityForRecolor = configuration.SpecializedClassifiers.Backpacks.Enabled,
                WeaponRecolorMode = configuration.SpecializedClassifiers.Weapons.Mode,
                WeaponCategoryColors = configuration.SpecializedClassifiers.Weapons.CategoryColors
            },
            new(
                configuration.TraderBuyValuePerSlotCutoffs,
                configuration.SpecializedClassifiers.Ammunition.PenetrationCutoffs,
                configuration.SpecializedClassifiers.UnarmoredRigs.CapacityCutoffs,
                configuration.SpecializedClassifiers.Backpacks.CapacityCutoffs),
            configuration.TierColors)
    {
    }

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
	    {
		    request.ApplyTier(result.Tier.Value);
		    if (request.ApplyPresentation is not null && tierColors is not null && result.Tier.Value is >= RecolorTier.Common and <= RecolorTier.Unobtainium)
		    {
			    var tierNumber = (int)result.Tier.Value;
			    var (label, translationKey) = result.ContextualLabelKind switch
			    {
				    RecolorContextualLabelKind.PenetrationTier => ("Penetration Tier", "RecolorPenetrationTier"),
				    RecolorContextualLabelKind.CapacityTier => ("Capacity Tier", "RecolorCapacityTier"),
				    RecolorContextualLabelKind.ArmorClass => ("Armor Class", "RecolorArmorClass"),
				    RecolorContextualLabelKind.TraderTier => ("Trader Tier", "RecolorTraderTier"),
				    _ when settings.UseTraderBuyPriceForRecolor => ("Value Tier", "RecolorValueTier"),
				    _ => ("Trader Tier", "RecolorTraderTier")
			    };
			    var presentationOverride = result.PresentationOverride;
			    var displayLabel = presentationOverride?.ContextualLabel ?? label;
			    var displayTranslationKey = presentationOverride?.ContextualLabelTranslationKey ?? translationKey;
			    var includeTierNumber = presentationOverride?.IncludeTierNumber ?? true;
			    var contextualLabel = includeTierNumber ? $"{displayLabel} {tierNumber}" : displayLabel;
			    request.ApplyPresentation(new(
				    presentationOverride?.Color ?? tierColors[tierNumber - 1],
				    contextualLabel,
				    displayTranslationKey,
				    includeTierNumber ? tierNumber : 0));
		    }
	    }
	    if (result.Warning is not null && ShouldEmitWarning(request.Item))
		    warn(result.Warning);
	    return result;
    }

    private bool ShouldEmitWarning(RecolorItem item) =>
        item.Kind != RecolorItemKind.Weapon ||
        settings.WeaponRecolorMode != WeaponRecolorMode.WeaponCategory ||
        item.WeaponCategory is not (null or WeaponCategory.Unknown) ||
        warnedUnknownWeaponIds.Add(item.Id);

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
            item.Penetration is >= 0 && double.IsFinite(item.Penetration.Value)
                ? new(true, TierByLowerBounds(item.Penetration.Value, thresholds.AmmoPenetration),
                    ContextualLabelKind: RecolorContextualLabelKind.PenetrationTier)
                : Fallback(item, "ammunition penetration"),
        RecolorItemKind.Armor or RecolorItemKind.ArmoredRig when settings.UseArmorClassForRecolor => ClassifyArmor(item),
        RecolorItemKind.Rig when settings.UseRigCapacityForRecolor => ClassifyCapacity(item, thresholds.RigCapacity, "rig capacity"),
        RecolorItemKind.Backpack when settings.UseBackpackCapacityForRecolor => ClassifyCapacity(item, thresholds.BackpackCapacity, "backpack capacity"),
        RecolorItemKind.Weapon when settings.WeaponRecolorMode == WeaponRecolorMode.TraderTier =>
            new(true, TraderTier(item), ContextualLabelKind: RecolorContextualLabelKind.TraderTier),
        RecolorItemKind.Weapon when settings.WeaponRecolorMode == WeaponRecolorMode.WeaponCategory =>
            ClassifyWeaponCategory(item),
        _ => null
    };

    private RecolorResult ClassifyWeaponCategory(RecolorItem item)
    {
        if (item.WeaponCategory == WeaponCategory.FlareSignal)
            return new(
                true,
                RecolorTier.Common,
                PresentationOverride: new(null, "Flare / Signal Weapon", "RecolorFlareSignalWeapon"));
        if (item.WeaponCategory is null or WeaponCategory.Unknown)
        {
            var normal = ClassifyNormal(item);
            var weaponClass = string.IsNullOrWhiteSpace(item.WeaponClass) ? "missing" : $"'{item.WeaponClass}'";
            return normal with
            {
                Warning = $"[ItemInfo] Weapon {item.Id} has unrecognized weapon class {weaponClass}; using the selected Background Recolor Basis."
            };
        }

        var category = item.WeaponCategory.Value;
        var tier = category switch
        {
            WeaponCategory.Pistol or WeaponCategory.Revolver => RecolorTier.Common,
            WeaponCategory.SubmachineGun or WeaponCategory.Shotgun => RecolorTier.Rare,
            WeaponCategory.AssaultCarbine or WeaponCategory.AssaultRifle or WeaponCategory.MachineGun => RecolorTier.Epic,
            WeaponCategory.MarksmanRifle => RecolorTier.Legendary,
            WeaponCategory.SniperRifle => RecolorTier.Uber,
            WeaponCategory.Launcher => RecolorTier.Unobtainium,
            _ => RecolorTier.Common
        };
        var color = settings.WeaponCategoryColors[category];
        var label = category switch
        {
            WeaponCategory.Pistol => "Pistol",
            WeaponCategory.Revolver => "Revolver",
            WeaponCategory.SubmachineGun => "Submachine Gun",
            WeaponCategory.Shotgun => "Shotgun",
            WeaponCategory.AssaultCarbine => "Assault Carbine",
            WeaponCategory.AssaultRifle => "Assault Rifle",
            WeaponCategory.MachineGun => "Machine Gun",
            WeaponCategory.MarksmanRifle => "Marksman Rifle",
            WeaponCategory.SniperRifle => "Sniper Rifle",
            WeaponCategory.Launcher => "Launcher",
            _ => "Weapon Category"
        };
        var translationKey = category == WeaponCategory.Launcher
            ? "RecolorLauncher"
            : $"RecolorWeapon{category}";
        return new(
            true,
            tier,
            PresentationOverride: new(color, label, translationKey));
    }

    private RecolorResult ClassifyArmor(RecolorItem item)
    {
        var usesDirectRootArmorClass = item.ProtectiveType is not null;
        var armorClass = usesDirectRootArmorClass
            ? item.ArmorClass
            : item.DefaultFrontPlateClass ?? item.SoftArmorClass ?? item.ArmorClass;
        if (armorClass is >= 1 and <= 6)
            return new(true, (RecolorTier)armorClass.Value, ContextualLabelKind: RecolorContextualLabelKind.ArmorClass);

        if (item.ProtectiveType is not null)
        {
            var normal = ClassifyNormal(item);
            var sourceFailure = item.ArmorClass is null
                ? "was missing"
                : $"was {item.ArmorClass.Value}, outside 1 through 6";
            var name = string.IsNullOrWhiteSpace(item.Name) ? "Unknown item" : item.Name;
            return normal with
            {
                Warning = $"[ItemInfo] Protective Item {name} ({item.Id}) recognized as {ProtectiveTypeLabel(item.ProtectiveType.Value)}; " +
                          $"direct root armor class {sourceFailure}; using the selected Background Recolor Basis."
            };
        }

        return Fallback(item, "armor class or default front plate");
    }

    private static string ProtectiveTypeLabel(ProtectiveItemType type) => type switch
    {
        ProtectiveItemType.FaceCover => "Armored Mask",
        ProtectiveItemType.Visor => "Face Shield",
        ProtectiveItemType.ArmorPlate => "Standalone Armor Plate",
        ProtectiveItemType.ArmoredEquipment => "Protective Attachment",
        _ => "Protective Item"
    };

    private RecolorResult ClassifyCapacity(RecolorItem item, double[] bounds, string field)
    {
        if (item.DirectGrids is null || item.DirectGrids.Count == 0 || item.DirectGrids.Any(g => g.Width <= 0 || g.Height <= 0))
            return Fallback(item, field);
        var capacity = item.DirectGrids.Sum(g => g.Width * g.Height);
        return new(true, TierByUpperBounds(capacity, bounds),
            ContextualLabelKind: RecolorContextualLabelKind.CapacityTier);
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
        item.TraderTier is >= 1 and <= 6 ? (RecolorTier)item.TraderTier : RecolorTier.Common;

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
    RecolorTier? Custom = null,
    Action<RecolorPresentation>? ApplyPresentation = null);

public sealed record RecolorPresentation(
    ColorSpecification Color,
    string ContextualLabel,
    string ContextualLabelTranslationKey,
    int TierNumber)
{
    public string Colorize(string text) => $"<color={Color.RichTextRgb}>{text}</color>";
}

public static class RecolorPresentationRenderer
{
    public static string AppendContextualLabel(
        string existingText,
        RecolorPresentation presentation,
        IReadOnlyDictionary<string, string> translations)
    {
        var contextualLabel = translations.TryGetValue(presentation.ContextualLabelTranslationKey, out var translatedLabel) &&
                              !string.IsNullOrWhiteSpace(translatedLabel)
            ? presentation.TierNumber > 0 ? $"{translatedLabel} {presentation.TierNumber}" : translatedLabel
            : presentation.ContextualLabel;
        return $"{existingText} | {presentation.Colorize(contextualLabel)}\n\n";
    }
}
