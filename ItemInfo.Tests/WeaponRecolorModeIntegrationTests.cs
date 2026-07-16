using ItemInfo.Recoloring;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;

namespace ItemInfo.Tests;

public class WeaponRecolorModeIntegrationTests
{
    private sealed class FixtureTemplate
    {
        public RecolorTier? Tier { get; set; }
        public string? Background { get; set; }
        public RecolorPresentation? Presentation { get; set; }
    }

    [Theory]
    [InlineData("Inherit", WeaponRecolorMode.Inherit)]
    [InlineData("TraderTier", WeaponRecolorMode.TraderTier)]
    [InlineData("WeaponCategory", WeaponRecolorMode.WeaponCategory)]
    public void Consolidated_configuration_accepts_exactly_the_weapon_recolor_modes(
        string configuredMode,
        WeaponRecolorMode expected)
    {
        var warnings = new List<string>();

        var configuration = RecolorConfiguration.Load(ConfigurationJson(configuredMode), [], warnings.Add);

        Assert.Equal(expected, configuration.SpecializedClassifiers.Weapons.Mode);
        Assert.Empty(warnings);
    }

    [Theory]
    [InlineData("Unknown")]
    [InlineData("1")]
    public void Invalid_weapon_mode_warns_and_falls_back_without_changing_valid_siblings(string configuredMode)
    {
        var warnings = new List<string>();

        var configuration = RecolorConfiguration.Load(
            ConfigurationJson(configuredMode, "TraderBuyValue", ammunitionEnabled: false),
            [],
            warnings.Add);

        Assert.Equal(WeaponRecolorMode.Inherit, configuration.SpecializedClassifiers.Weapons.Mode);
        Assert.Equal(BackgroundRecolorBasis.TraderBuyValue, configuration.Basis);
        Assert.False(configuration.SpecializedClassifiers.Ammunition.Enabled);
        Assert.Single(warnings, warning => warning.Contains("specializedClassifiers.weapons.mode"));
    }

    [Theory]
    [InlineData("Inherit", "TraderTier", RecolorTier.Rare, "Trader Tier 2")]
    [InlineData("Inherit", "TraderBuyValue", RecolorTier.Uber, "Value Tier 5")]
    [InlineData("TraderTier", "TraderTier", RecolorTier.Rare, "Trader Tier 2")]
    [InlineData("TraderTier", "TraderBuyValue", RecolorTier.Rare, "Trader Tier 2")]
    public void Weapon_modes_visibly_mutate_fixture_templates_for_both_bases(
        string weaponMode,
        string basis,
        RecolorTier expectedTier,
        string expectedLabel)
    {
        var configuration = RecolorConfiguration.Load(ConfigurationJson(weaponMode, basis), [], _ => { });

        var visible = Apply(configuration, RecolorItemAdapter.FromWeapon(LoadFixture().Weapon));

        Assert.Equal(expectedTier, visible.Tier);
        Assert.Equal(configuration.TierColors[(int)expectedTier - 1].BackgroundValue, visible.Background);
        Assert.Equal(expectedLabel, Assert.IsType<RecolorPresentation>(visible.Presentation).ContextualLabel);
    }

    [Fact]
    public void Trader_tier_weapon_mode_preserves_non_weapon_background_basis_behavior()
    {
        var configuration = RecolorConfiguration.Load(ConfigurationJson("TraderTier", "TraderBuyValue"), [], _ => { });
        var fixture = LoadFixture().NonWeapon;
        var item = new RecolorItem(
            fixture.Id,
            fixture.ParentId,
            fixture.Kind,
            fixture.TraderTier,
            fixture.FleaBanned,
            fixture.BestTraderBuyValue,
            fixture.Width,
            fixture.Height);

        var visible = Apply(configuration, item);

        Assert.Equal(RecolorTier.Uber, visible.Tier);
        Assert.Equal("orange", visible.Background);
        Assert.Equal("Value Tier 5", Assert.IsType<RecolorPresentation>(visible.Presentation).ContextualLabel);
    }

    private static FixtureTemplate Apply(RecolorConfiguration configuration, RecolorItem item)
    {
        var fixture = new FixtureTemplate();
        new StaticRecolorPass(configuration).Run(
            [new(
                item,
                tier => fixture.Tier = tier,
                ApplyPresentation: presentation =>
                {
                    fixture.Background = presentation.Color.BackgroundValue;
                    fixture.Presentation = presentation;
                })],
            _ => { });
        return fixture;
    }

    private static WeaponModeFixture LoadFixture() =>
        JsonSerializer.Deserialize<WeaponModeFixture>(
            File.ReadAllText(FindRepositoryFile("ItemInfo.Tests", "Fixtures", "weapon-mode-items.json")),
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                Converters = { new JsonStringEnumConverter() }
            })!;

    private static string ConfigurationJson(
        string weaponMode,
        string basis = "TraderTier",
        bool ammunitionEnabled = true) =>
        "{\"RarityRecolor\":{" +
        "\"enabled\":true,\"basis\":\"" + basis + "\"," +
        "\"display\":{\"addColorToName\":true,\"addContextualLabelToPricesInfo\":true}," +
        "\"tiers\":{\"colors\":[\"default\",\"green\",\"blue\",\"violet\",\"orange\",\"red\"],\"traderBuyValuePerSlotCutoffs\":[10000,15000,20000,40000,60000]}," +
        "\"specializedClassifiers\":{\"ammunition\":{\"enabled\":" + ammunitionEnabled.ToString().ToLowerInvariant() +
        ",\"penetrationCutoffs\":[20,30,40,50,60]}," +
        "\"protectiveItems\":{\"enabled\":true}," +
        "\"unarmoredRigs\":{\"enabled\":true,\"capacityCutoffs\":[8,12,16,20,24]}," +
        "\"backpacks\":{\"enabled\":true,\"capacityCutoffs\":[12,20,25,30,40]}," +
        "\"weapons\":{\"mode\":\"" + weaponMode + "\"}}," +
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

    private sealed record WeaponModeFixture(
        WeaponRecolorTemplate Weapon,
        NonWeaponFixture NonWeapon);

    private sealed record NonWeaponFixture(
        string Id,
        string ParentId,
        RecolorItemKind Kind,
        int TraderTier,
        bool FleaBanned,
        double? BestTraderBuyValue,
        int? Width,
        int? Height);
}
