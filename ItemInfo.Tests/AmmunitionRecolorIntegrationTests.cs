using ItemInfo.Recoloring;
using System.Text.Json;
using Xunit;

namespace ItemInfo.Tests;

public class AmmunitionRecolorIntegrationTests
{
    private sealed class FixtureTemplate
    {
        public RecolorTier? Tier { get; set; }
        public string? Background { get; set; }
        public RecolorPresentation? Presentation { get; set; }
    }

    [Fact]
    public void Consolidated_configuration_loads_the_functional_ammunition_settings()
    {
        var warnings = new List<string>();

        var configuration = RecolorConfiguration.Load(ConfigurationJson(false, [19, 29, 39, 49, 59]), [], warnings.Add);

        Assert.False(configuration.SpecializedClassifiers.Ammunition.Enabled);
        Assert.Equal([19d, 29, 39, 49, 59], configuration.SpecializedClassifiers.Ammunition.PenetrationCutoffs);
        Assert.Empty(warnings);
    }

    [Fact]
    public void Penetration_boundaries_and_overflow_visibly_mutate_fixture_templates()
    {
        var configuration = RecolorConfiguration.Load(ConfigurationJson(), [], _ => { });
        var fixtureData = LoadFixture();

        foreach (var fixtureCase in fixtureData.BoundaryCases)
        {
            var fixture = Apply(configuration, fixtureCase.Template, out var warnings);
            var expected = (RecolorTier)fixtureCase.ExpectedTier;

            Assert.Equal(expected, fixture.Tier);
            Assert.Equal(configuration.TierColors[fixtureCase.ExpectedTier - 1].BackgroundValue, fixture.Background);
            Assert.Empty(warnings);
        }
    }

    [Theory]
    [InlineData(false, RecolorTier.Rare, "Trader Tier 2")]
    [InlineData(true, RecolorTier.Uber, "Value Tier 5")]
    public void Disabled_ammunition_classifier_returns_to_the_selected_basis(
        bool traderBuyValueBasis,
        RecolorTier expected,
        string expectedLabel)
    {
        var configuration = RecolorConfiguration.Load(ConfigurationJson(false, basis: traderBuyValueBasis ? "TraderBuyValue" : "TraderTier"), [], _ => { });
        var fixture = Apply(configuration, LoadFixture().DisabledCase.Template, out var warnings);

        Assert.Equal(expected, fixture.Tier);
        Assert.Equal(expectedLabel, Assert.IsType<RecolorPresentation>(fixture.Presentation).ContextualLabel);
        Assert.Empty(warnings);
    }

    [Fact]
    public void Malformed_fixture_penetration_warns_once_and_visibly_inherits_the_selected_basis()
    {
        var configuration = RecolorConfiguration.Load(ConfigurationJson(), [], _ => { });

        foreach (var fixtureCase in LoadFixture().MalformedCases)
        {
            var fixture = Apply(configuration, fixtureCase.Template, out var warnings);

            Assert.Equal((RecolorTier)fixtureCase.ExpectedTier, fixture.Tier);
            var warning = Assert.Single(warnings);
            Assert.Contains(fixtureCase.Template.Id, warning);
            Assert.Contains("penetration", warning, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("selected Background Recolor Basis", warning);
        }
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Non_finite_penetration_warns_once_and_inherits_the_selected_basis(double penetration)
    {
        var configuration = RecolorConfiguration.Load(ConfigurationJson(), [], _ => { });
        var template = LoadFixture().MalformedCases[0].Template with { Id = "pen-non-finite", PenetrationPower = penetration };
        var fixture = Apply(configuration, template, out var warnings);

        Assert.Equal(RecolorTier.Rare, fixture.Tier);
        Assert.Single(warnings);
    }

    [Fact]
    public void Visible_contextual_label_uses_the_penetration_translation_key()
    {
        var configuration = RecolorConfiguration.Load(ConfigurationJson(), [], _ => { });
        var template = LoadFixture().BoundaryCases.Single(item => item.Template.PenetrationPower == 30).Template;
        var fixture = Apply(configuration, template, out _);
        using var translations = JsonDocument.Parse(File.ReadAllText(FindRepositoryFile("config", "translations.json")));
        var english = translations.RootElement.GetProperty("en").EnumerateObject()
            .ToDictionary(property => property.Name, property => property.Value.GetString()!);
        english["RecolorPenetrationTier"] = "Localized Penetration Tier";

        var rendered = RecolorPresentationRenderer.AppendContextualLabel(
            "Price information",
            Assert.IsType<RecolorPresentation>(fixture.Presentation),
            english);

        Assert.Contains("Localized Penetration Tier 3", rendered);
        Assert.Equal("RecolorPenetrationTier", fixture.Presentation!.ContextualLabelTranslationKey);
        Assert.Equal("Penetration Tier 3", fixture.Presentation.ContextualLabel);
    }

    private static FixtureTemplate Apply(
        RecolorConfiguration configuration,
        AmmunitionRecolorTemplate ammunitionTemplate,
        out List<string> warnings)
    {
        var fixture = new FixtureTemplate();
        warnings = [];
        var request = new StaticRecolorRequest(
            RecolorItemAdapter.FromAmmunition(ammunitionTemplate),
            tier => fixture.Tier = tier,
            ApplyPresentation: presentation =>
            {
                fixture.Background = presentation.Color.BackgroundValue;
                fixture.Presentation = presentation;
            });

        new StaticRecolorPass(configuration).Run([request], warnings.Add);
        return fixture;
    }

    private static AmmunitionFixture LoadFixture() =>
        JsonSerializer.Deserialize<AmmunitionFixture>(
            File.ReadAllText(FindRepositoryFile("ItemInfo.Tests", "Fixtures", "ammunition-items.json")),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

    private static string ConfigurationJson(
        bool ammunitionEnabled = true,
        double[]? penetrationCutoffs = null,
        string basis = "TraderTier") =>
        "{\"RarityRecolor\":{" +
        "\"enabled\":true,\"basis\":\"" + basis + "\"," +
        "\"display\":{\"addColorToName\":true,\"addContextualLabelToPricesInfo\":true}," +
        "\"tiers\":{\"colors\":[\"default\",\"green\",\"blue\",\"violet\",\"orange\",\"red\"],\"traderBuyValuePerSlotCutoffs\":[10000,15000,20000,40000,60000]}," +
        "\"specializedClassifiers\":{\"ammunition\":{\"enabled\":" + ammunitionEnabled.ToString().ToLowerInvariant() +
        ",\"penetrationCutoffs\":[" + string.Join(',', penetrationCutoffs ?? [20, 30, 40, 50, 60]) + "]}," +
        "\"protectiveItems\":{\"enabled\":true}," +
        "\"unarmoredRigs\":{\"enabled\":true,\"capacityCutoffs\":[8,12,16,20,24]}," +
        "\"backpacks\":{\"enabled\":true,\"capacityCutoffs\":[12,20,25,30,40]}}," +
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

    private sealed record AmmunitionFixture(
        AmmunitionFixtureCase[] BoundaryCases,
        AmmunitionFixtureCase[] MalformedCases,
        AmmunitionFixtureCase DisabledCase);

    private sealed record AmmunitionFixtureCase(AmmunitionRecolorTemplate Template, int ExpectedTier);
}
