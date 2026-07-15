using System.Text.Json;

namespace ItemInfo.Recoloring;

public enum BackgroundRecolorBasis { TraderTier, TraderBuyValue }

public sealed record RecolorDisplay(bool AddColorToName, bool AddContextualLabelToPricesInfo);

public sealed record AmmunitionClassifierConfiguration(bool Enabled, double[] PenetrationCutoffs);

public sealed record CapacityClassifierConfiguration(bool Enabled, double[] CapacityCutoffs);

public sealed record ToggleClassifierConfiguration(bool Enabled);

public sealed record SpecializedClassifierConfiguration(
    AmmunitionClassifierConfiguration Ammunition,
    ToggleClassifierConfiguration ProtectiveItems,
    CapacityClassifierConfiguration UnarmoredRigs,
    CapacityClassifierConfiguration Backpacks);

public sealed record RecolorConfiguration(
    bool Enabled,
    BackgroundRecolorBasis Basis,
    RecolorDisplay Display,
    IReadOnlyList<ColorSpecification> TierColors,
    double[] TraderBuyValuePerSlotCutoffs,
    SpecializedClassifierConfiguration SpecializedClassifiers,
    IReadOnlyDictionary<string, int> CustomOverrides,
    IReadOnlyList<string> Blacklist)
{
    private static readonly string[] DefaultColorValues = ["default", "green", "blue", "violet", "orange", "red"];
    private static readonly double[] DefaultCutoffs = [10_000, 15_000, 20_000, 40_000, 60_000];
    private static readonly HashSet<string> LegacyKeys = new(StringComparer.Ordinal)
    {
        "useTraderBuyPriceForRecolor", "markFleaMarketBannedItemsAsOverpowered",
        "usePenetrationForAmmoRecolor", "useArmorClassForRecolor", "useRigCapacityForRecolor",
        "useBackpackCapacityForRecolor", "addColorToName", "addTierNameToPricesInfo",
        "fallbackValueBasedRecolor", "bypassAmmoRecolor", "bypassKeysRecolor", "customRarity"
    };
    private static readonly HashSet<string> RequiredVnextKeys = new(StringComparer.Ordinal)
    {
        "enabled", "basis", "display", "tiers", "customOverrides", "blacklist"
    };

    public static RecolorConfiguration Defaults { get; } = new(
        true,
        BackgroundRecolorBasis.TraderTier,
        new(true, true),
        DefaultColorValues.Select(ColorSpecification.ParseDefault).ToArray(),
        [.. DefaultCutoffs],
        new(
            new(true, [20, 30, 40, 50, 60]),
            new(true),
            new(true, [8, 12, 16, 20, 24]),
            new(true, [12, 20, 25, 30, 40])),
        new Dictionary<string, int>(),
        []);

    public bool UseTraderBuyPriceForRecolor => Basis == BackgroundRecolorBasis.TraderBuyValue;
    public bool AddColorToName => Display.AddColorToName;
    public bool AddContextualLabelToPricesInfo => Display.AddContextualLabelToPricesInfo;

    public static RecolorConfiguration Load(string configJson, IReadOnlyCollection<string> staleLegacyFiles, Action<string> warn)
    {
        try
        {
            using var document = JsonDocument.Parse(configJson);
            if (!document.RootElement.TryGetProperty("RarityRecolor", out var root) || root.ValueKind != JsonValueKind.Object)
            {
                warn("[ItemInfo] Missing or invalid RarityRecolor section; background recoloring is disabled for this startup.");
                return Defaults with { Enabled = false };
            }

            var properties = root.EnumerateObject().Select(property => property.Name).ToHashSet(StringComparer.Ordinal);
            var hasLegacyKeys = properties.Overlaps(LegacyKeys) || document.RootElement.TryGetProperty("RarityRecolorBlacklist", out _);
            var hasCompleteVnextSchema = RequiredVnextKeys.IsSubsetOf(properties);

            if (hasLegacyKeys && !hasCompleteVnextSchema)
            {
                warn("[ItemInfo] Legacy-only RarityRecolor configuration detected; background recoloring is disabled for this startup. Perform the documented clean vNext migration.");
                return Defaults with { Enabled = false };
            }

            if (hasLegacyKeys)
                warn("[ItemInfo] Complete vNext RarityRecolor configuration is authoritative; stale legacy recoloring keys were ignored. Remove them during cleanup.");
            if (staleLegacyFiles.Count > 0)
                warn("[ItemInfo] Stale tiers.json or tiers_hex.json files were ignored. Cleanly replace the mod directory and do not restore those files.");

            var enabled = ReadBoolean(root, "enabled", Defaults.Enabled, warn, "RarityRecolor.enabled");
            var basis = ReadBasis(root, warn);
            var display = ReadDisplay(root, warn);
            var (colors, cutoffs) = ReadTiers(root, warn);
            var specializedClassifiers = ReadSpecializedClassifiers(root, warn);
            var overrides = ReadOverrides(root, warn);
            var blacklist = ReadBlacklist(root, warn);
            return new(enabled, basis, display, colors, cutoffs, specializedClassifiers, overrides, blacklist);
        }
        catch (JsonException)
        {
            warn("[ItemInfo] config/config.json is malformed; background recoloring is disabled for this startup.");
            return Defaults with { Enabled = false };
        }
    }

    private static BackgroundRecolorBasis ReadBasis(JsonElement root, Action<string> warn)
    {
        if (root.TryGetProperty("basis", out var value) && value.ValueKind == JsonValueKind.String &&
            Enum.TryParse<BackgroundRecolorBasis>(value.GetString(), true, out var basis))
            return basis;
        warn("[ItemInfo] Invalid RarityRecolor.basis; using TraderTier.");
        return Defaults.Basis;
    }

    private static RecolorDisplay ReadDisplay(JsonElement root, Action<string> warn)
    {
        if (!root.TryGetProperty("display", out var display) || display.ValueKind != JsonValueKind.Object)
        {
            warn("[ItemInfo] Invalid RarityRecolor.display section; using built-in display defaults.");
            return Defaults.Display;
        }
        return new(
            ReadBoolean(display, "addColorToName", Defaults.Display.AddColorToName, warn, "RarityRecolor.display.addColorToName"),
            ReadBoolean(display, "addContextualLabelToPricesInfo", Defaults.Display.AddContextualLabelToPricesInfo, warn, "RarityRecolor.display.addContextualLabelToPricesInfo"));
    }

    private static (IReadOnlyList<ColorSpecification>, double[]) ReadTiers(JsonElement root, Action<string> warn)
    {
        if (!root.TryGetProperty("tiers", out var tiers) || tiers.ValueKind != JsonValueKind.Object)
        {
            warn("[ItemInfo] Invalid RarityRecolor.tiers section; using built-in tier defaults.");
            return (Defaults.TierColors, [.. Defaults.TraderBuyValuePerSlotCutoffs]);
        }

        var colors = ReadColors(tiers, warn);
        var cutoffs = tiers.TryGetProperty("traderBuyValuePerSlotCutoffs", out var cutoffElement)
            ? ThresholdConfiguration.ReadAscending(cutoffElement, 5, Defaults.TraderBuyValuePerSlotCutoffs,
                "RarityRecolor.tiers.traderBuyValuePerSlotCutoffs", warn)
            : InvalidCutoffs(warn);
        return (colors, cutoffs);
    }

    private static IReadOnlyList<ColorSpecification> ReadColors(JsonElement tiers, Action<string> warn)
    {
        if (!tiers.TryGetProperty("colors", out var colors) || colors.ValueKind != JsonValueKind.Array || colors.GetArrayLength() != 6)
        {
            warn("[ItemInfo] RarityRecolor.tiers.colors must contain exactly six Color Specifications; using built-in tier colors.");
            return Defaults.TierColors;
        }

        var result = new ColorSpecification[6];
        var index = 0;
        foreach (var value in colors.EnumerateArray())
        {
            if (value.ValueKind == JsonValueKind.String && ColorSpecification.TryParse(value.GetString(), out result[index]))
            {
                index++;
                continue;
            }
            warn($"[ItemInfo] Invalid Color Specification at RarityRecolor.tiers.colors[{index}]; using built-in value {DefaultColorValues[index]} for this field.");
            result[index] = Defaults.TierColors[index];
            index++;
        }
        return result;
    }

    private static SpecializedClassifierConfiguration ReadSpecializedClassifiers(JsonElement root, Action<string> warn)
    {
        if (!root.TryGetProperty("specializedClassifiers", out var section) || section.ValueKind != JsonValueKind.Object)
        {
            warn("[ItemInfo] Invalid RarityRecolor.specializedClassifiers section; using built-in specialized classifier defaults.");
            return Defaults.SpecializedClassifiers;
        }

        return new(
            ReadAmmunitionClassifier(section, warn),
            ReadProtectiveItemClassifier(section, warn),
            ReadCapacityClassifier(section, "unarmoredRigs", Defaults.SpecializedClassifiers.UnarmoredRigs, warn),
            ReadCapacityClassifier(section, "backpacks", Defaults.SpecializedClassifiers.Backpacks, warn));
    }

    private static AmmunitionClassifierConfiguration ReadAmmunitionClassifier(
        JsonElement specializedClassifiers,
        Action<string> warn)
    {
        const string path = "RarityRecolor.specializedClassifiers.ammunition";
        var defaults = Defaults.SpecializedClassifiers.Ammunition;
        if (!specializedClassifiers.TryGetProperty("ammunition", out var section) || section.ValueKind != JsonValueKind.Object)
        {
            warn($"[ItemInfo] Invalid {path} section; using built-in defaults for this classifier.");
            return defaults;
        }

        var enabled = ReadBoolean(section, "enabled", defaults.Enabled, warn, $"{path}.enabled");
        var cutoffs = section.TryGetProperty("penetrationCutoffs", out var cutoffElement)
            ? ThresholdConfiguration.ReadAscending(cutoffElement, 5, defaults.PenetrationCutoffs, $"{path}.penetrationCutoffs", warn)
            : MissingPenetrationCutoffs(defaults, warn);
        return new(enabled, cutoffs);
    }

    private static double[] MissingPenetrationCutoffs(AmmunitionClassifierConfiguration defaults, Action<string> warn)
    {
        warn("[ItemInfo] Invalid or missing RarityRecolor.specializedClassifiers.ammunition.penetrationCutoffs; using built-in defaults for this classifier.");
        return [.. defaults.PenetrationCutoffs];
    }

    private static CapacityClassifierConfiguration ReadCapacityClassifier(
        JsonElement specializedClassifiers,
        string propertyName,
        CapacityClassifierConfiguration defaults,
        Action<string> warn)
    {
        var path = $"RarityRecolor.specializedClassifiers.{propertyName}";
        if (!specializedClassifiers.TryGetProperty(propertyName, out var section) || section.ValueKind != JsonValueKind.Object)
        {
            warn($"[ItemInfo] Invalid {path} section; using built-in defaults for this classifier.");
            return defaults;
        }

        var enabled = ReadBoolean(section, "enabled", defaults.Enabled, warn, $"{path}.enabled");
        var cutoffs = section.TryGetProperty("capacityCutoffs", out var cutoffElement)
            ? ThresholdConfiguration.ReadAscending(cutoffElement, 5, defaults.CapacityCutoffs, $"{path}.capacityCutoffs", warn)
            : MissingCapacityCutoffs(defaults, path, warn);
        return new(enabled, cutoffs);
    }

    private static double[] MissingCapacityCutoffs(CapacityClassifierConfiguration defaults, string path, Action<string> warn)
    {
        warn($"[ItemInfo] Invalid or missing {path}.capacityCutoffs; using built-in defaults for this classifier.");
        return [.. defaults.CapacityCutoffs];
    }

    private static ToggleClassifierConfiguration ReadProtectiveItemClassifier(
        JsonElement specializedClassifiers,
        Action<string> warn)
    {
        const string path = "RarityRecolor.specializedClassifiers.protectiveItems";
        if (!specializedClassifiers.TryGetProperty("protectiveItems", out var protectiveItems) ||
            protectiveItems.ValueKind != JsonValueKind.Object)
        {
            warn($"[ItemInfo] Invalid {path} section; using built-in defaults for this classifier.");
            return Defaults.SpecializedClassifiers.ProtectiveItems;
        }

        return new(ReadBoolean(
            protectiveItems,
            "enabled",
            Defaults.SpecializedClassifiers.ProtectiveItems.Enabled,
            warn,
            $"{path}.enabled"));
    }

    private static double[] InvalidCutoffs(Action<string> warn)
    {
        warn("[ItemInfo] Invalid or missing RarityRecolor.tiers.traderBuyValuePerSlotCutoffs; using built-in defaults for this section.");
        return [.. Defaults.TraderBuyValuePerSlotCutoffs];
    }

    private static IReadOnlyDictionary<string, int> ReadOverrides(JsonElement root, Action<string> warn)
    {
        if (!root.TryGetProperty("customOverrides", out var section) || section.ValueKind != JsonValueKind.Object ||
            !section.TryGetProperty("itemIdToTier", out var map) || map.ValueKind != JsonValueKind.Object)
        {
            warn("[ItemInfo] Invalid RarityRecolor.customOverrides section; using no custom overrides.");
            return Defaults.CustomOverrides;
        }

        var result = new Dictionary<string, int>();
        foreach (var property in map.EnumerateObject())
            if (property.Value.TryGetInt32(out var tier) && tier is >= 1 and <= 6)
                result[property.Name] = tier;
            else
                warn($"[ItemInfo] Invalid Recolor Tier at RarityRecolor.customOverrides.itemIdToTier.{property.Name}; ignoring this field.");
        return result;
    }

    private static IReadOnlyList<string> ReadBlacklist(JsonElement root, Action<string> warn)
    {
        if (!root.TryGetProperty("blacklist", out var section) || section.ValueKind != JsonValueKind.Object ||
            !section.TryGetProperty("itemOrParentIds", out var ids) || ids.ValueKind != JsonValueKind.Array ||
            ids.EnumerateArray().Any(value => value.ValueKind != JsonValueKind.String))
        {
            warn("[ItemInfo] Invalid RarityRecolor.blacklist section; using an empty Recolor Blacklist.");
            return Defaults.Blacklist;
        }
        return ids.EnumerateArray().Select(value => value.GetString()!).ToArray();
    }

    private static bool ReadBoolean(JsonElement root, string propertyName, bool fallback, Action<string> warn, string path)
    {
        if (root.TryGetProperty(propertyName, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False)
            return value.GetBoolean();
        warn($"[ItemInfo] Invalid {path}; using built-in value {fallback.ToString().ToLowerInvariant()}.");
        return fallback;
    }
}
