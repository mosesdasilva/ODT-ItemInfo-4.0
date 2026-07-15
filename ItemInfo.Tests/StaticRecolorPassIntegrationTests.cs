using ItemInfo.Recoloring;
using System.Text.Json;
using Xunit;

namespace ItemInfo.Tests;

public class StaticRecolorPassIntegrationTests
{
    private sealed class FixtureTemplate
    {
        public RecolorTier? Tier { get; set; }
        public string? Background { get; set; }
        public string Name { get; set; } = "Fixture name";
        public string Description { get; init; } = "Fixture description";
        public string? ContextualLabel { get; set; }
    }

    [Fact]
    public void Pass_updates_visible_background_preserves_text_and_emits_item_warning()
    {
        var template = new FixtureTemplate();
        var warnings = new List<string>();
        var request = new StaticRecolorRequest(
            new RecolorItem("broken-pack", "parent", RecolorItemKind.Backpack, 2),
            tier => template.Tier = tier);

        var results = new StaticRecolorPass(new(), RecolorThresholds.Defaults).Run([request], warnings.Add);

        Assert.Equal(RecolorTier.Rare, template.Tier);
        Assert.Equal("Fixture name", template.Name);
        Assert.Equal("Fixture description", template.Description);
        Assert.Contains("broken-pack", Assert.Single(warnings));
        Assert.Single(results);
    }

    [Fact]
    public void Blacklisted_template_is_not_modified_even_with_custom_override()
    {
        var template = new FixtureTemplate { Tier = RecolorTier.Legendary };
        var request = new StaticRecolorRequest(
            new RecolorItem("blacklisted", "parent", RecolorItemKind.Ammo, 1, Penetration: 61),
            tier => template.Tier = tier,
            Blacklisted: true,
            Custom: RecolorTier.Custom);

        new StaticRecolorPass(new(), RecolorThresholds.Defaults).Run([request], _ => { });
        Assert.Equal(RecolorTier.Legendary, template.Tier);
    }

    [Fact]
    public void Pass_projects_one_semantic_color_to_visible_template_name_and_label()
    {
        var template = new FixtureTemplate();
        var colors = RecolorConfiguration.Defaults.TierColors.ToArray();
        colors[1] = ColorSpecification.ParseDefault("#1a2b3c");
        var pass = new StaticRecolorPass(
            new() { UseTraderBuyPriceForRecolor = true },
            RecolorThresholds.Defaults,
            colors);
        var request = new StaticRecolorRequest(
            new RecolorItem("fixture", "parent", RecolorItemKind.Normal, 1, BestTraderBuyValue: 10_000, Width: 1, Height: 1),
            tier => template.Tier = tier,
            ApplyPresentation: presentation =>
            {
                template.Background = presentation.Color.BackgroundValue;
                template.Name = presentation.Colorize(template.Name);
                template.ContextualLabel = presentation.ContextualLabel;
            });

        pass.Run([request], _ => { });

        Assert.Equal(RecolorTier.Rare, template.Tier);
        Assert.Equal("#1A2B3C", template.Background);
        Assert.Equal("<color=#1A2B3C>Fixture name</color>", template.Name);
        Assert.Equal("Value Tier 2", template.ContextualLabel);
    }

    [Fact]
    public void Production_presentation_renderer_uses_the_canonical_translation_backed_contextual_label()
    {
        RecolorPresentation? capturedPresentation = null;
        var pass = new StaticRecolorPass(
            new() { UseTraderBuyPriceForRecolor = true },
            RecolorThresholds.Defaults,
            RecolorConfiguration.Defaults.TierColors);
        var request = new StaticRecolorRequest(
            new RecolorItem("fixture", "parent", RecolorItemKind.Normal, 1, BestTraderBuyValue: 10_000, Width: 1, Height: 1),
            _ => { },
            ApplyPresentation: presentation => capturedPresentation = presentation);

        pass.Run([request], _ => { });
        using var translations = JsonDocument.Parse(File.ReadAllText(FindRepositoryFile("config", "translations.json")));
        var english = translations.RootElement.GetProperty("en").EnumerateObject()
            .ToDictionary(property => property.Name, property => property.Value.GetString()!);
        english["RecolorValueTier"] = "Localized Value Tier";

        var rendered = RecolorPresentationRenderer.AppendContextualLabel(
            "Price information",
            Assert.IsType<RecolorPresentation>(capturedPresentation),
            english);

        Assert.Contains("Localized Value Tier 2", rendered);
        Assert.DoesNotContain(">Value Tier 2</color>", rendered);
        Assert.DoesNotContain("Common", rendered, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Rare", rendered, StringComparison.OrdinalIgnoreCase);

        foreach (var language in translations.RootElement.EnumerateObject()
                     .Where(language => language.Value.ValueKind == JsonValueKind.Object &&
                                        language.Value.TryGetProperty("Distortion", out _)))
        {
            Assert.True(language.Value.TryGetProperty("RecolorTraderTier", out _), $"{language.Name} is missing RecolorTraderTier");
            Assert.True(language.Value.TryGetProperty("RecolorValueTier", out _), $"{language.Name} is missing RecolorValueTier");
        }
    }

    [Fact]
    public void No_offer_trader_tier_stays_inside_the_six_tier_visible_presentation()
    {
        RecolorTier? visibleTier = null;
        RecolorPresentation? visiblePresentation = null;
        var request = new StaticRecolorRequest(
            new RecolorItem("no-offer", "parent", RecolorItemKind.Normal, 0, Width: 1, Height: 1),
            tier => visibleTier = tier,
            ApplyPresentation: presentation => visiblePresentation = presentation);

        new StaticRecolorPass(new(), RecolorThresholds.Defaults, RecolorConfiguration.Defaults.TierColors)
            .Run([request], _ => { });

        Assert.Equal(RecolorTier.Common, visibleTier);
        Assert.Equal("Trader Tier 1", Assert.IsType<RecolorPresentation>(visiblePresentation).ContextualLabel);
    }

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
