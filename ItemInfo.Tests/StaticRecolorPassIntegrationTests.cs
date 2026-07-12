using ItemInfo.Recoloring;
using Xunit;

namespace ItemInfo.Tests;

public class StaticRecolorPassIntegrationTests
{
    private sealed class FixtureTemplate
    {
        public RecolorTier? Background { get; set; }
        public string Name { get; init; } = "Fixture name";
        public string Description { get; init; } = "Fixture description";
    }

    [Fact]
    public void Pass_updates_visible_background_preserves_text_and_emits_item_warning()
    {
        var template = new FixtureTemplate();
        var warnings = new List<string>();
        var request = new StaticRecolorRequest(
            new RecolorItem("broken-pack", "parent", RecolorItemKind.Backpack, 2),
            tier => template.Background = tier);

        var results = new StaticRecolorPass(new(), RecolorThresholds.Defaults).Run([request], warnings.Add);

        Assert.Equal(RecolorTier.Rare, template.Background);
        Assert.Equal("Fixture name", template.Name);
        Assert.Equal("Fixture description", template.Description);
        Assert.Contains("broken-pack", Assert.Single(warnings));
        Assert.Single(results);
    }

    [Fact]
    public void Blacklisted_template_is_not_modified_even_with_custom_override()
    {
        var template = new FixtureTemplate { Background = RecolorTier.Legendary };
        var request = new StaticRecolorRequest(
            new RecolorItem("blacklisted", "parent", RecolorItemKind.Ammo, 1, Penetration: 61),
            tier => template.Background = tier,
            Blacklisted: true,
            Custom: RecolorTier.Custom);

        new StaticRecolorPass(new(), RecolorThresholds.Defaults).Run([request], _ => { });
        Assert.Equal(RecolorTier.Legendary, template.Background);
    }
}
