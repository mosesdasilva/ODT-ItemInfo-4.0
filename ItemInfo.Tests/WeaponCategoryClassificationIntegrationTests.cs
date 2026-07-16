using ItemInfo.Recoloring;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;

namespace ItemInfo.Tests;

public class WeaponCategoryClassificationIntegrationTests
{
    [Fact]
    public void Weapon_Category_mode_applies_the_accepted_color_map_to_every_category()
    {
        var fixture = LoadFixture();
        var configuration = LoadConfiguration();

        foreach (var expected in fixture.Categories)
        {
            var warnings = new List<string>();
            var visible = Apply(configuration, Create(expected, fixture), warnings.Add);

            Assert.Equal(Enum.Parse<WeaponCategory>(expected.ExpectedCategory), visible.Item.WeaponCategory);
            Assert.Equal(expected.ExpectedColor, visible.Background);
            Assert.Equal(expected.ExpectedLabel, Assert.IsType<RecolorPresentation>(visible.Presentation).ContextualLabel);
            Assert.Empty(warnings);
        }
    }

    [Fact]
    public void Ordered_rules_resolve_conflicting_vanilla_shapes()
    {
        var fixture = LoadFixture();

        foreach (var expected in fixture.Conflicts)
        {
            var item = Create(expected, fixture);

            Assert.Equal(Enum.Parse<WeaponCategory>(expected.ExpectedCategory), item.WeaponCategory);
        }
    }

    [Fact]
    public void Recursive_modded_ancestry_is_in_scope_but_non_weapons_are_not()
    {
        var fixture = LoadFixture();

        Assert.Equal(WeaponCategory.AssaultRifle, Create(fixture.Modded, fixture).WeaponCategory);
        Assert.Null(TryCreate(fixture.OutsideScope, fixture));
    }

    [Fact]
    public void Flare_signal_weapons_use_Tier_1_without_warning()
    {
        var fixture = LoadFixture();
        var warnings = new List<string>();

        var visible = Apply(LoadConfiguration(), Create(fixture.Flare, fixture), warnings.Add);

        Assert.Equal(RecolorTier.Common, visible.Tier);
        Assert.Equal("default", visible.Background);
        Assert.Empty(warnings);
    }

    [Fact]
    public void Unknown_in_scope_category_warns_once_and_inherits_the_selected_basis()
    {
        var fixture = LoadFixture();
        var item = Create(fixture.Unknown, fixture) with
        {
            TraderTier = 2,
            BestTraderBuyValue = 50_000,
            Width = 1,
            Height = 1
        };
        var configuration = LoadConfiguration(basis: "TraderBuyValue");
        var warnings = new List<string>();
        var first = new VisibleTemplate();
        var second = new VisibleTemplate();
        var pass = new StaticRecolorPass(configuration);

        pass.Run([Request(item, first), Request(item, second)], warnings.Add);

        Assert.Equal(RecolorTier.Uber, first.Tier);
        Assert.Equal("orange", first.Background);
        Assert.Equal(RecolorTier.Uber, second.Tier);
        Assert.Single(warnings);
        Assert.Contains("unknown-weapon", warnings[0]);
        Assert.Contains("futureWeaponClass", warnings[0]);
        Assert.Contains("Background Recolor Basis", warnings[0]);
    }

    [Fact]
    public void Missing_weapon_class_warns_and_inherits_the_selected_basis()
    {
        var fixture = LoadFixture();
        var missingClass = fixture.Unknown with { Id = "missing-class-weapon", WeapClass = null };
        var warnings = new List<string>();

        var visible = Apply(LoadConfiguration(), Create(missingClass, fixture), warnings.Add);

        Assert.Equal(RecolorTier.Rare, visible.Tier);
        Assert.Contains("missing-class-weapon", Assert.Single(warnings));
        Assert.Contains("weapon class missing", warnings[0]);
    }

    [Fact]
    public void Non_flare_special_weapon_warns_and_inherits_the_selected_basis()
    {
        var fixture = LoadFixture();
        var specialWeapon = fixture.Unknown with { Id = "unknown-special-weapon", WeapClass = "specialWeapon" };
        var warnings = new List<string>();

        var visible = Apply(LoadConfiguration(), Create(specialWeapon, fixture), warnings.Add);

        Assert.Equal(RecolorTier.Rare, visible.Tier);
        Assert.Contains("unknown-special-weapon", Assert.Single(warnings));
        Assert.Contains("'specialWeapon'", warnings[0]);
    }

    [Fact]
    public void Launcher_uses_its_independently_configured_color_and_contextual_label()
    {
        var fixture = LoadFixture();
        var launcher = fixture.Categories.Single(item => item.ExpectedCategory == "Launcher");

        var visible = Apply(LoadConfiguration(launcherColor: "#123456"), Create(launcher, fixture), _ => { });

        Assert.Equal("#123456", visible.Background);
        Assert.Equal("Launcher", Assert.IsType<RecolorPresentation>(visible.Presentation).ContextualLabel);
    }

    private static RecolorItem Create(WeaponFixtureItem fixtureItem, WeaponCategoryFixture fixture) =>
        Assert.IsType<RecolorItem>(TryCreate(fixtureItem, fixture));

    private static RecolorItem? TryCreate(WeaponFixtureItem fixtureItem, WeaponCategoryFixture fixture) =>
        WeaponRecolorItemAdapter.Create(
            new(
                fixtureItem.Id,
                fixtureItem.ParentId,
                fixtureItem.WeapClass,
                TraderTier: 2,
                FleaBanned: false,
                BestTraderBuyValue: 50_000,
                Width: 1,
                Height: 1),
            id => fixture.Parents.TryGetValue(id, out var parent) ? parent : null);

    private static VisibleTemplate Apply(RecolorConfiguration configuration, RecolorItem item, Action<string> warn)
    {
        var visible = new VisibleTemplate { Item = item };
        new StaticRecolorPass(configuration).Apply(Request(item, visible), warn);
        return visible;
    }

    private static StaticRecolorRequest Request(RecolorItem item, VisibleTemplate visible) =>
        new(
            item,
            tier => visible.Tier = tier,
            ApplyPresentation: presentation =>
            {
                visible.Background = presentation.Color.BackgroundValue;
                visible.Presentation = presentation;
            });

    private static RecolorConfiguration LoadConfiguration(
        string basis = "TraderTier",
        string launcherColor = "red")
    {
        var json = File.ReadAllText(FindRepositoryFile("config", "config.json"))
            .Replace("\"basis\": \"TraderTier\"", $"\"basis\": \"{basis}\"")
            .Replace("\"mode\": \"Inherit\"", "\"mode\": \"WeaponCategory\"")
            .Replace("\"launcher\": \"red\"", $"\"launcher\": \"{launcherColor}\"");
        var warnings = new List<string>();
        var configuration = RecolorConfiguration.Load(json, [], warnings.Add);
        Assert.Empty(warnings);
        return configuration;
    }

    private static WeaponCategoryFixture LoadFixture() =>
        JsonSerializer.Deserialize<WeaponCategoryFixture>(
            File.ReadAllText(FindRepositoryFile("ItemInfo.Tests", "Fixtures", "weapon-category-items.json")),
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                Converters = { new JsonStringEnumConverter() }
            })!;

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

    private sealed class VisibleTemplate
    {
        public RecolorItem Item { get; init; } = null!;
        public RecolorTier? Tier { get; set; }
        public string? Background { get; set; }
        public RecolorPresentation? Presentation { get; set; }
    }

    private sealed record WeaponCategoryFixture(
        IReadOnlyDictionary<string, string> Parents,
        IReadOnlyList<WeaponFixtureItem> Categories,
        IReadOnlyList<WeaponFixtureItem> Conflicts,
        WeaponFixtureItem Modded,
        WeaponFixtureItem Flare,
        WeaponFixtureItem Unknown,
        WeaponFixtureItem OutsideScope);

    private sealed record WeaponFixtureItem(
        string Id,
        string ParentId,
        string? WeapClass,
        string ExpectedCategory = "",
        string ExpectedColor = "",
        string ExpectedLabel = "");
}
