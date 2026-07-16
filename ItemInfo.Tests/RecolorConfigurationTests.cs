using ItemInfo.Recoloring;
using System.Text.Json;
using Xunit;

namespace ItemInfo.Tests;

public class RecolorConfigurationTests
{
    [Fact]
    public void Shipped_configuration_loads_without_migration_warnings_and_preserves_unrelated_settings()
    {
        var json = File.ReadAllText(FindRepositoryFile("config", "config.json"));
        var warnings = new List<string>();

        var recolor = RecolorConfiguration.Load(json, [], warnings.Add);
        using var wholeConfig = JsonDocument.Parse(json);

        Assert.True(recolor.Enabled);
        Assert.Equal(6, recolor.TierColors.Count);
        Assert.Empty(warnings);
        Assert.Equal("en", wholeConfig.RootElement.GetProperty("UserLocale").GetString());
        Assert.True(wholeConfig.RootElement.GetProperty("PricesInfo").GetProperty("enabled").GetBoolean());
    }

    [Fact]
    public void Complete_vnext_configuration_loads_six_tiers_without_warnings()
    {
        var warnings = new List<string>();
        var configuration = RecolorConfiguration.Load(
            """
            {"RarityRecolor":{
              "enabled":true,
              "basis":"TraderBuyValue",
              "display":{"addColorToName":false,"addContextualLabelToPricesInfo":true},
              "tiers":{"colors":["default","green","blue","violet","orange","#F00"],"traderBuyValuePerSlotCutoffs":[1,2,3,4,5]},
              "specializedClassifiers":{
                "ammunition":{"enabled":true,"penetrationCutoffs":[20,30,40,50,60]},
                "protectiveItems":{"enabled":true},
                "unarmoredRigs":{"enabled":true,"capacityCutoffs":[8,12,16,20,24]},
                "backpacks":{"enabled":true,"capacityCutoffs":[12,20,25,30,40]},
                "weapons":{"mode":"Inherit"}
              },
              "customOverrides":{"itemIdToTier":{"fixture":4}},
              "blacklist":{"itemOrParentIds":["blocked"]}
            }}
            """,
            [],
            warnings.Add);

        Assert.True(configuration.Enabled);
        Assert.Equal(BackgroundRecolorBasis.TraderBuyValue, configuration.Basis);
        Assert.Equal(6, configuration.TierColors.Count);
        Assert.Equal("#FF0000", configuration.TierColors[5].BackgroundValue);
        Assert.Equal([1d, 2, 3, 4, 5], configuration.TraderBuyValuePerSlotCutoffs);
        Assert.Empty(warnings);
    }

    [Fact]
    public void Capacity_classifiers_load_independent_toggles_and_cutoffs()
    {
        var warnings = new List<string>();
        var configuration = RecolorConfiguration.Load(
            """
            {"RarityRecolor":{
              "enabled":true,
              "basis":"TraderTier",
              "display":{"addColorToName":true,"addContextualLabelToPricesInfo":true},
              "tiers":{"colors":["default","green","blue","violet","orange","red"],"traderBuyValuePerSlotCutoffs":[10000,15000,20000,40000,60000]},
              "specializedClassifiers":{
                "ammunition":{"enabled":true,"penetrationCutoffs":[20,30,40,50,60]},
                "protectiveItems":{"enabled":true},
                "unarmoredRigs":{"enabled":false,"capacityCutoffs":[7,11,15,19,23]},
                "backpacks":{"enabled":true,"capacityCutoffs":[13,21,26,31,41]},
                "weapons":{"mode":"Inherit"}
              },
              "customOverrides":{"itemIdToTier":{}},
              "blacklist":{"itemOrParentIds":[]}
            }}
            """,
            [],
            warnings.Add);

        Assert.False(configuration.SpecializedClassifiers.UnarmoredRigs.Enabled);
        Assert.Equal([7d, 11, 15, 19, 23], configuration.SpecializedClassifiers.UnarmoredRigs.CapacityCutoffs);
        Assert.True(configuration.SpecializedClassifiers.Backpacks.Enabled);
        Assert.Equal([13d, 21, 26, 31, 41], configuration.SpecializedClassifiers.Backpacks.CapacityCutoffs);
        Assert.Empty(warnings);
    }

    [Fact]
    public void Invalid_rig_cutoffs_fall_back_without_replacing_valid_backpack_settings()
    {
        var warnings = new List<string>();
        var json = ValidJson().Replace(
            "\"unarmoredRigs\":{\"enabled\":true,\"capacityCutoffs\":[8,12,16,20,24]}",
            "\"unarmoredRigs\":{\"enabled\":false,\"capacityCutoffs\":[8,12,12,20,24]}")
            .Replace(
                "\"backpacks\":{\"enabled\":true,\"capacityCutoffs\":[12,20,25,30,40]}",
                "\"backpacks\":{\"enabled\":false,\"capacityCutoffs\":[13,21,26,31,41]}");

        var configuration = RecolorConfiguration.Load(json, [], warnings.Add);

        Assert.False(configuration.SpecializedClassifiers.UnarmoredRigs.Enabled);
        Assert.Equal([8d, 12, 16, 20, 24], configuration.SpecializedClassifiers.UnarmoredRigs.CapacityCutoffs);
        Assert.False(configuration.SpecializedClassifiers.Backpacks.Enabled);
        Assert.Equal([13d, 21, 26, 31, 41], configuration.SpecializedClassifiers.Backpacks.CapacityCutoffs);
        Assert.Single(warnings, warning => warning.Contains("unarmoredRigs.capacityCutoffs"));
    }

    [Fact]
    public void Invalid_color_falls_back_without_discarding_valid_siblings()
    {
        var warnings = new List<string>();
        var configuration = LoadWithTiers("[\"#ABC\",\"gray\",\"blue\",\"violet\",\"orange\",\"red\"]", "[1,2,3,4,5]", warnings);

        Assert.Equal("#AABBCC", configuration.TierColors[0].BackgroundValue);
        Assert.Equal("green", configuration.TierColors[1].BackgroundValue);
        Assert.Equal("blue", configuration.TierColors[2].BackgroundValue);
        Assert.Single(warnings, warning => warning.Contains("colors[1]"));
    }

    [Fact]
    public void Invalid_cutoffs_fall_back_without_discarding_valid_colors()
    {
        var warnings = new List<string>();
        var configuration = LoadWithTiers("[\"#ABC\",\"green\",\"blue\",\"violet\",\"orange\",\"red\"]", "[1,2,2,4,5]", warnings);

        Assert.Equal("#AABBCC", configuration.TierColors[0].BackgroundValue);
        Assert.Equal(RecolorConfiguration.Defaults.TraderBuyValuePerSlotCutoffs, configuration.TraderBuyValuePerSlotCutoffs);
        Assert.Single(warnings, warning => warning.Contains("traderBuyValuePerSlotCutoffs"));
    }

    [Fact]
    public void Legacy_only_configuration_disables_only_recoloring_with_one_actionable_warning()
    {
        var warnings = new List<string>();
        var configuration = RecolorConfiguration.Load("""{"RarityRecolor":{"enabled":true,"useTraderBuyPriceForRecolor":true,"customRarity":{}}}""", [], warnings.Add);

        Assert.False(configuration.Enabled);
        Assert.Single(warnings, warning => warning.Contains("Legacy-only") && warning.Contains("disabled"));
    }

    [Fact]
    public void Partial_vnext_with_legacy_keys_does_not_bypass_legacy_only_shutdown()
    {
        var warnings = new List<string>();
        var configuration = RecolorConfiguration.Load(
            """{"RarityRecolor":{"enabled":true,"basis":"TraderBuyValue","useTraderBuyPriceForRecolor":true}}""",
            [],
            warnings.Add);

        Assert.False(configuration.Enabled);
        Assert.Single(warnings, warning => warning.Contains("Legacy-only") && warning.Contains("disabled"));
    }

    [Fact]
    public void Complete_vnext_with_stale_keys_uses_vnext_and_warns_once()
    {
        var warnings = new List<string>();
        var json = ValidJson().Replace("\"enabled\":true", "\"enabled\":true,\"useTraderBuyPriceForRecolor\":false");
        var configuration = RecolorConfiguration.Load(json, [], warnings.Add);

        Assert.Equal(BackgroundRecolorBasis.TraderBuyValue, configuration.Basis);
        Assert.Single(warnings, warning => warning.Contains("authoritative") && warning.Contains("ignored"));
    }

    [Fact]
    public void Stale_legacy_files_are_ignored_with_one_cleanup_warning()
    {
        var warnings = new List<string>();
        var configuration = RecolorConfiguration.Load(ValidJson(), ["tiers.json", "tiers_hex.json"], warnings.Add);

        Assert.Equal(BackgroundRecolorBasis.TraderBuyValue, configuration.Basis);
        Assert.Single(warnings, warning => warning.Contains("tiers.json") && warning.Contains("ignored"));
    }

    private static RecolorConfiguration LoadWithTiers(string colors, string cutoffs, List<string> warnings) =>
        RecolorConfiguration.Load(ValidJson(colors, cutoffs), [], warnings.Add);

    private static string ValidJson(
        string colors = "[\"default\",\"green\",\"blue\",\"violet\",\"orange\",\"red\"]",
        string cutoffs = "[1,2,3,4,5]") =>
        "{\"RarityRecolor\":{" +
        "\"enabled\":true,\"basis\":\"TraderBuyValue\"," +
        "\"display\":{\"addColorToName\":false,\"addContextualLabelToPricesInfo\":true}," +
        "\"tiers\":{\"colors\":" + colors + ",\"traderBuyValuePerSlotCutoffs\":" + cutoffs + "}," +
        "\"specializedClassifiers\":{" +
        "\"ammunition\":{\"enabled\":true,\"penetrationCutoffs\":[20,30,40,50,60]}," +
        "\"protectiveItems\":{\"enabled\":true}," +
        "\"unarmoredRigs\":{\"enabled\":true,\"capacityCutoffs\":[8,12,16,20,24]}," +
        "\"backpacks\":{\"enabled\":true,\"capacityCutoffs\":[12,20,25,30,40]},\"weapons\":{\"mode\":\"Inherit\"}}," +
        "\"customOverrides\":{\"itemIdToTier\":{}},\"blacklist\":{\"itemOrParentIds\":[]}}}";

    private static string FindRepositoryFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine([directory.FullName, .. parts]);
            if (File.Exists(candidate))
                return candidate;
            directory = directory.Parent;
        }
        throw new FileNotFoundException($"Could not find repository file {Path.Combine(parts)}.");
    }
}
