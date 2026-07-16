using ItemInfo.Recoloring;
using System.Text.Json;
using Xunit;

namespace ItemInfo.Tests;

public class CrossFeatureRegressionIntegrationTests
{
    private sealed class VisibleTemplate
    {
        public RecolorTier? Tier { get; set; }
        public string? Background { get; set; }
        public string? ContextualLabel { get; set; }
        public string Name { get; set; } = "Fixture name";
        public string Description { get; } = "Fixture description";
    }

    [Fact]
    public void Trader_buy_value_fixture_proves_offer_selection_per_slot_boundaries_and_base_weapon_value()
    {
        var fixture = LoadFixture();
        var configuration = RecolorConfiguration.Defaults with { Basis = BackgroundRecolorBasis.TraderBuyValue };

        foreach (var fixtureCase in fixture.TraderBuyValueCases)
        {
            var bestOffer = fixtureCase.IsWeapon
                ? TraderBuyValueCalculator.SelectBaseWeaponValue(
                    new(fixtureCase.Offers, fixture.ExcludedDefaultPresetOffers))
                : TraderBuyValueCalculator.SelectHighest(fixtureCase.Offers);
            var visible = Apply(
                configuration,
                new RecolorItem(
                    fixtureCase.Id,
                    "fixture-parent",
                    fixtureCase.IsWeapon ? RecolorItemKind.Weapon : RecolorItemKind.Normal,
                    fixtureCase.TraderTier,
                    BestTraderBuyValue: bestOffer.Roubles,
                    Width: fixtureCase.Width,
                    Height: fixtureCase.Height),
                out var warnings);

            Assert.Equal(fixtureCase.ExpectedRoubles, bestOffer.Roubles);
            Assert.Equal(fixtureCase.ExpectedTraderName, bestOffer.TraderName);
            Assert.Equal((RecolorTier)fixtureCase.ExpectedTier, visible.Tier);
            Assert.Equal(configuration.TierColors[fixtureCase.ExpectedTier - 1].BackgroundValue, visible.Background);
            Assert.Equal($"Value Tier {fixtureCase.ExpectedTier}", visible.ContextualLabel);
            Assert.Empty(warnings);
        }

        Assert.True(
            fixture.ExcludedDefaultPresetOffers.Max(offer => offer.OfferInTraderCurrency * offer.CurrencyRoubleValue) >
            fixture.TraderBuyValueCases.Single(fixtureCase => fixtureCase.IsWeapon).ExpectedRoubles,
            "The Base Weapon Value fixture must contain a more valuable preset attachment offer that is excluded from the template value.");
    }

    [Fact]
    public void Background_recolor_basis_fixture_visibly_switches_between_trader_tier_and_trader_buy_value()
    {
        var fixtureCase = LoadFixture().BasisCase;
        var item = new RecolorItem(
            fixtureCase.Id,
            "fixture-parent",
            RecolorItemKind.Normal,
            fixtureCase.TraderTier,
            BestTraderBuyValue: fixtureCase.BestTraderBuyValue,
            Width: fixtureCase.Width,
            Height: fixtureCase.Height);

        var traderTier = Apply(RecolorConfiguration.Defaults, item, out var traderTierWarnings);
        var traderBuyValue = Apply(
            RecolorConfiguration.Defaults with { Basis = BackgroundRecolorBasis.TraderBuyValue },
            item,
            out var traderBuyValueWarnings);

        Assert.Equal((RecolorTier)fixtureCase.ExpectedTraderTier, traderTier.Tier);
        Assert.Equal($"Trader Tier {fixtureCase.ExpectedTraderTier}", traderTier.ContextualLabel);
        Assert.Equal((RecolorTier)fixtureCase.ExpectedValueTier, traderBuyValue.Tier);
        Assert.Equal($"Value Tier {fixtureCase.ExpectedValueTier}", traderBuyValue.ContextualLabel);
        Assert.Empty(traderTierWarnings);
        Assert.Empty(traderBuyValueWarnings);
    }

    [Fact]
    public void Cross_feature_fixture_visibly_preserves_precedence_labels_colors_and_diagnostics()
    {
        var fixture = LoadFixture();
        var colors = RecolorConfiguration.Defaults.TierColors.ToArray();
        colors[1] = ColorSpecification.ParseDefault("#123456");
        var configuration = RecolorConfiguration.Defaults with
        {
            Basis = BackgroundRecolorBasis.TraderBuyValue,
            TierColors = colors,
            FleaBanWarning = new(true, ColorSpecification.ParseDefault("#abc")),
            SpecializedClassifiers = RecolorConfiguration.Defaults.SpecializedClassifiers with
            {
                Weapons = RecolorConfiguration.Defaults.SpecializedClassifiers.Weapons with
                {
                    Mode = WeaponRecolorMode.WeaponCategory
                }
            }
        };

        foreach (var fixtureCase in fixture.FeatureCases)
        {
            var visible = Apply(configuration, fixtureCase, out var warnings);

            Assert.Equal(fixtureCase.ExpectedTier is null ? null : (RecolorTier)fixtureCase.ExpectedTier, visible.Tier);
            Assert.Equal(fixtureCase.ExpectedBackground, visible.Background);
            Assert.Equal(fixtureCase.ExpectedContextualLabel, visible.ContextualLabel);
            Assert.Equal("Fixture description", visible.Description);
            if (fixtureCase.ExpectNameColor)
            {
                Assert.NotEqual("Fixture name", visible.Name);
                Assert.Contains("Fixture name", visible.Name);
                Assert.StartsWith("<color=", visible.Name);
            }
            else
            {
                Assert.Equal("Fixture name", visible.Name);
            }

            if (fixtureCase.ExpectedWarningFragment is null)
            {
                Assert.Empty(warnings);
            }
            else
            {
                Assert.Contains(fixtureCase.ExpectedWarningFragment, Assert.Single(warnings), StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    private static VisibleTemplate Apply(
        RecolorConfiguration configuration,
        RecolorItem item,
        out List<string> warnings)
    {
        var visible = new VisibleTemplate();
        warnings = [];
        new StaticRecolorPass(configuration).Run(
        [
            new(
                item,
                tier => visible.Tier = tier,
                ApplyPresentation: presentation =>
                {
                    visible.Background = presentation.Color.BackgroundValue;
                    visible.ContextualLabel = presentation.ContextualLabel;
                })
        ],
        warnings.Add);
        return visible;
    }

    private static VisibleTemplate Apply(
        RecolorConfiguration configuration,
        FeatureFixtureCase fixtureCase,
        out List<string> warnings)
    {
        var visible = new VisibleTemplate
        {
            Tier = fixtureCase.InitialTier is null ? null : (RecolorTier)fixtureCase.InitialTier,
            Background = fixtureCase.InitialBackground
        };
        var item = new RecolorItem(
            fixtureCase.Id,
            "fixture-parent",
            Enum.Parse<RecolorItemKind>(fixtureCase.Kind),
            fixtureCase.TraderTier,
            fixtureCase.FleaBanned,
            fixtureCase.BestTraderBuyValue,
            fixtureCase.Width,
            fixtureCase.Height,
            fixtureCase.Penetration,
            fixtureCase.ArmorClass,
            DirectGrids: fixtureCase.DirectGrids?.Select(grid => (grid[0], grid[1])).ToArray(),
            Name: fixtureCase.Id,
            ProtectiveType: fixtureCase.ProtectiveType is null
                ? null
                : Enum.Parse<ProtectiveItemType>(fixtureCase.ProtectiveType),
            WeaponCategory: fixtureCase.WeaponCategory is null
                ? null
                : Enum.Parse<WeaponCategory>(fixtureCase.WeaponCategory),
            WeaponClass: fixtureCase.WeaponClass);

        warnings = [];
        new StaticRecolorPass(configuration).Run(
        [
            new(
                item,
                tier => visible.Tier = tier,
                fixtureCase.Blacklisted,
                fixtureCase.CustomTier is null ? null : (RecolorTier)fixtureCase.CustomTier,
                presentation =>
                {
                    visible.Background = presentation.Color.BackgroundValue;
                    visible.ContextualLabel = presentation.ContextualLabel;
                    visible.Name = presentation.Colorize(visible.Name);
                })
        ],
        warnings.Add);
        return visible;
    }

    private static CrossFeatureFixture LoadFixture() =>
        JsonSerializer.Deserialize<CrossFeatureFixture>(
            File.ReadAllText(TestRepository.FindFile("ItemInfo.Tests", "Fixtures", "cross-feature-regression.json")),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

    private sealed record CrossFeatureFixture(
        TraderBuyValueFixtureCase[] TraderBuyValueCases,
        TraderBuyOffer[] ExcludedDefaultPresetOffers,
        BasisFixtureCase BasisCase,
        FeatureFixtureCase[] FeatureCases);

    private sealed record TraderBuyValueFixtureCase(
        string Id,
        int TraderTier,
        int Width,
        int Height,
        bool IsWeapon,
        TraderBuyOffer[] Offers,
        double ExpectedRoubles,
        string? ExpectedTraderName,
        int ExpectedTier);

    private sealed record BasisFixtureCase(
        string Id,
        int TraderTier,
        double BestTraderBuyValue,
        int Width,
        int Height,
        int ExpectedTraderTier,
        int ExpectedValueTier);

    private sealed record FeatureFixtureCase(
        string Id,
        string Kind,
        int TraderTier,
        bool FleaBanned,
        double? BestTraderBuyValue,
        int? Width,
        int? Height,
        double? Penetration,
        int? ArmorClass,
        int[][]? DirectGrids,
        string? ProtectiveType,
        string? WeaponCategory,
        string? WeaponClass,
        bool Blacklisted,
        int? CustomTier,
        int? InitialTier,
        string? InitialBackground,
        int? ExpectedTier,
        string? ExpectedBackground,
        string? ExpectedContextualLabel,
        bool ExpectNameColor,
        string? ExpectedWarningFragment);
}
