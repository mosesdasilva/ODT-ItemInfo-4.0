using ItemInfo.Recoloring;
using System.Text.Json;
using Xunit;

namespace ItemInfo.Tests;

public class ProtectiveItemRecolorIntegrationTests
{
    [Theory]
    [InlineData("fixture-body-armor-descendant", ProtectiveItemType.BodyArmor)]
    [InlineData("fixture-helmet-descendant", ProtectiveItemType.Helmet)]
    [InlineData("fixture-armored-mask", ProtectiveItemType.FaceCover)]
    [InlineData("fixture-face-shield", ProtectiveItemType.Visor)]
    [InlineData("64afc71497cf3a403c01ff38", ProtectiveItemType.ArmorPlate)]
    [InlineData("68a9b601863d2a71fa0494ae", ProtectiveItemType.ArmoredEquipment)]
    [InlineData("6943c85be2f21398e70378cc", ProtectiveItemType.ArmorPlate)]
    public void Structural_recognition_covers_recursive_protective_base_class_ancestry(
        string itemId,
        ProtectiveItemType expectedType)
    {
        Assert.Equal(expectedType, LoadFixture().Recognize(itemId));
    }

    [Theory]
    [InlineData("64afc71497cf3a403c01ff38", RecolorTier.Unobtainium)]
    [InlineData("fixture-armored-mask", RecolorTier.Legendary)]
    [InlineData("fixture-face-shield", RecolorTier.Epic)]
    [InlineData("68a9b601863d2a71fa0494ae", RecolorTier.Legendary)]
    [InlineData("6943c85be2f21398e70378cc", RecolorTier.Common)]
    public void Direct_class_protective_descendants_visibly_use_armor_class(string itemId, RecolorTier expectedTier)
    {
        var fixture = LoadFixture();
        var item = fixture.CreateRecolorItem(itemId);
        RecolorTier? visibleTier = null;
        RecolorPresentation? visiblePresentation = null;
        var warnings = new List<string>();

        new StaticRecolorPass(Configuration()).Run(
            [new(item, tier => visibleTier = tier, ApplyPresentation: value => visiblePresentation = value)], warnings.Add);

        Assert.Equal(expectedTier, visibleTier);
        Assert.Equal("Armor Class " + (int)expectedTier, Assert.IsType<RecolorPresentation>(visiblePresentation).ContextualLabel);
        Assert.Equal("RecolorArmorClass", visiblePresentation!.ContextualLabelTranslationKey);
        Assert.Empty(warnings);
    }

    [Fact]
    public void Helmet_with_a_valid_direct_root_class_visibly_uses_that_class()
    {
        var item = LoadFixture().CreateRecolorItem("fixture-direct-class-helmet");
        RecolorTier? visibleTier = null;
        RecolorPresentation? visiblePresentation = null;
        var warnings = new List<string>();

        var result = Assert.Single(new StaticRecolorPass(Configuration()).Run(
            [new(item, tier => visibleTier = tier, ApplyPresentation: value => visiblePresentation = value)],
            warnings.Add));

        Assert.Equal(RecolorItemKind.Armor, item.Kind);
        Assert.Equal(RecolorTier.Legendary, result.Tier);
        Assert.Equal(RecolorTier.Legendary, visibleTier);
        Assert.Equal("Armor Class 4", Assert.IsType<RecolorPresentation>(visiblePresentation).ContextualLabel);
        Assert.Empty(warnings);
    }

    [Fact]
    public void Helmet_uses_the_highest_required_default_component_and_excludes_optional_attachments()
    {
        var item = LoadFixture().CreateRecolorItem("fixture-component-class-helmet");
        RecolorTier? visibleTier = null;
        RecolorPresentation? visiblePresentation = null;
        var warnings = new List<string>();

        var result = Assert.Single(new StaticRecolorPass(Configuration()).Run(
            [new(item, tier => visibleTier = tier, ApplyPresentation: value => visiblePresentation = value)],
            warnings.Add));

        Assert.Equal(RecolorTier.Legendary, result.Tier);
        Assert.Equal(RecolorTier.Legendary, visibleTier);
        Assert.Equal("Armor Class 4", Assert.IsType<RecolorPresentation>(visiblePresentation).ContextualLabel);
        Assert.Empty(warnings);
    }

    [Fact]
    public void Exhausted_helmet_sources_warn_once_then_visibly_use_the_selected_basis()
    {
        var item = LoadFixture().CreateRecolorItem("fixture-exhausted-helmet");
        RecolorTier? visibleTier = null;
        var warnings = new List<string>();

        var result = Assert.Single(new StaticRecolorPass(Configuration()).Run(
            [new(item, tier => visibleTier = tier)], warnings.Add));

        Assert.Equal(RecolorTier.Rare, result.Tier);
        Assert.Equal(RecolorTier.Rare, visibleTier);
        var warning = Assert.Single(warnings);
        Assert.Contains("Broken intrinsic helmet", warning);
        Assert.Contains("fixture-exhausted-helmet", warning);
        Assert.Contains("Helmet", warning);
        Assert.Contains("direct root armor class was 0, outside 1 through 6", warning);
        Assert.Contains("required default component Helmet_top was 0, outside 1 through 6", warning);
        Assert.Contains("required default component Helmet_back was 7, outside 1 through 6", warning);
        Assert.Contains("selected Background Recolor Basis", warning);
        Assert.DoesNotContain("mod_equipment_000", warning);
    }

    [Fact]
    public void Built_in_modular_armor_uses_the_default_front_plate_across_real_slot_casing()
    {
        var fixture = LoadFixture();
        var item = fixture.CreateRecolorItem("545cdb794bdc2d3a198b456a");
        var warnings = new List<string>();

        var result = Assert.Single(new StaticRecolorPass(Configuration()).Run([new(item, _ => { })], warnings.Add));

        Assert.Equal(RecolorItemKind.Armor, item.Kind);
        Assert.Equal((RecolorTier)6, result.Tier);
        Assert.Equal(RecolorContextualLabelKind.ArmorClass, result.ContextualLabelKind);
        Assert.Empty(warnings);
    }

    [Fact]
    public void First_valid_modular_source_skips_invalid_preset_then_uses_root_slot_front_plate()
    {
        var item = LoadFixture().CreateRecolorItem("fixture-root-slot-fallback");
        var warnings = new List<string>();

        var result = Assert.Single(new StaticRecolorPass(Configuration()).Run([new(item, _ => { })], warnings.Add));

        Assert.Equal((RecolorTier)5, result.Tier);
        Assert.Equal(RecolorContextualLabelKind.ArmorClass, result.ContextualLabelKind);
        Assert.Empty(warnings);
    }

    [Fact]
    public void Modular_armor_without_a_preset_uses_its_root_slot_default_front_plate()
    {
        var item = LoadFixture().CreateRecolorItem("674d91ce6e862d5a95059ed6");
        var warnings = new List<string>();

        var result = Assert.Single(new StaticRecolorPass(Configuration()).Run([new(item, _ => { })], warnings.Add));

        Assert.Equal((RecolorTier)5, result.Tier);
        Assert.Equal(RecolorContextualLabelKind.ArmorClass, result.ContextualLabelKind);
        Assert.Empty(warnings);
    }

    [Theory]
    [InlineData("5648a7494bdc2d9d488b4583", 2)]
    [InlineData("fixture-modded-soft-armor", 3)]
    public void Soft_only_built_in_and_modded_armor_use_the_soft_armor_fallback_class(
        string itemId,
        int expectedTierNumber)
    {
        var item = LoadFixture().CreateRecolorItem(itemId);
        var warnings = new List<string>();

        var result = Assert.Single(new StaticRecolorPass(Configuration()).Run([new(item, _ => { })], warnings.Add));

        Assert.Equal((RecolorTier)expectedTierNumber, result.Tier);
        Assert.Equal(RecolorContextualLabelKind.ArmorClass, result.ContextualLabelKind);
        Assert.Empty(warnings);
    }

    [Fact]
    public void Rig_with_valid_default_armor_data_is_armored_and_armor_class_wins_over_capacity()
    {
        var item = LoadFixture().CreateRecolorItem("fixture-modded-armored-rig");
        var warnings = new List<string>();

        var result = Assert.Single(new StaticRecolorPass(Configuration()).Run([new(item, _ => { })], warnings.Add));

        Assert.Equal(RecolorItemKind.ArmoredRig, item.Kind);
        Assert.Equal((RecolorTier)4, result.Tier);
        Assert.Equal(RecolorContextualLabelKind.ArmorClass, result.ContextualLabelKind);
        Assert.Empty(warnings);
    }

    [Fact]
    public void Exhausted_modular_sources_warn_once_with_each_failure_then_use_the_selected_basis()
    {
        var item = LoadFixture().CreateRecolorItem("fixture-exhausted-modular-armor");
        var warnings = new List<string>();

        var result = Assert.Single(new StaticRecolorPass(Configuration()).Run([new(item, _ => { })], warnings.Add));

        Assert.Equal((RecolorTier)2, result.Tier);
        var warning = Assert.Single(warnings);
        Assert.Contains("Broken modular armor", warning);
        Assert.Contains("fixture-exhausted-modular-armor", warning);
        Assert.Contains("Body Armor", warning);
        Assert.Contains("Default Front Plate Class was 0, outside 1 through 6", warning);
        Assert.Contains("Root-Slot Default Front Plate Class was 7, outside 1 through 6", warning);
        Assert.Contains("Soft-Armor Fallback Class was 9, outside 1 through 6", warning);
        Assert.Contains("selected Background Recolor Basis", warning);
    }

    [Fact]
    public void Rig_without_default_armor_data_remains_a_rig_even_when_its_root_class_is_valid()
    {
        var fixture = LoadFixture();
        var item = fixture.CreateRecolorItem("fixture-unarmored-rig");
        var warnings = new List<string>();

        var result = Assert.Single(new StaticRecolorPass(Configuration()).Run([new(item, _ => { })], warnings.Add));

        Assert.Equal(RecolorItemKind.Rig, item.Kind);
        Assert.NotNull(result.Tier);
        Assert.Empty(warnings);
    }

    [Fact]
    public void Exhausted_direct_class_source_warns_once_and_inherits_the_selected_basis()
    {
        var fixture = LoadFixture();
        var item = fixture.CreateRecolorItem("fixture-broken-visor");
        var warnings = new List<string>();

        var result = Assert.Single(new StaticRecolorPass(Configuration()).Run([new(item, _ => { })], warnings.Add));

        Assert.Equal(RecolorTier.Rare, result.Tier);
        var warning = Assert.Single(warnings);
        Assert.Contains("Broken protective visor", warning);
        Assert.Contains("fixture-broken-visor", warning);
        Assert.Contains("Face Shield", warning);
        Assert.Contains("direct root armor class was 0", warning);
        Assert.Contains("selected Background Recolor Basis", warning);
    }

    [Fact]
    public void Protective_item_toggle_is_functional_and_defaults_enabled()
    {
        var warnings = new List<string>();
        var disabled = RecolorConfiguration.Load(ConfigurationJson(false), [], warnings.Add);

        Assert.True(RecolorConfiguration.Defaults.SpecializedClassifiers.ProtectiveItems.Enabled);
        Assert.False(disabled.SpecializedClassifiers.ProtectiveItems.Enabled);
        Assert.Empty(warnings);
    }

    [Fact]
    public void Disabled_protective_classifier_inherits_without_a_protective_warning()
    {
        var fixture = LoadFixture();
        var item = fixture.CreateRecolorItem("fixture-broken-visor");
        var warnings = new List<string>();

        var result = Assert.Single(new StaticRecolorPass(
            RecolorConfiguration.Load(ConfigurationJson(false), [], _ => { }))
            .Run([new(item, _ => { })], warnings.Add));

        Assert.Equal(RecolorTier.Rare, result.Tier);
        Assert.Empty(warnings);
    }

    [Fact]
    public void Armor_class_contextual_label_is_available_in_complete_translation_sets()
    {
        using var translations = JsonDocument.Parse(
            File.ReadAllText(FindRepositoryFile("config", "translations.json")));

        foreach (var language in translations.RootElement.EnumerateObject()
                     .Where(language => language.Value.ValueKind == JsonValueKind.Object &&
                                        language.Value.TryGetProperty("Distortion", out _)))
            Assert.True(language.Value.TryGetProperty("RecolorArmorClass", out _),
                $"{language.Name} is missing RecolorArmorClass");
    }

    private static RecolorConfiguration Configuration() => RecolorConfiguration.Load(ConfigurationJson(true), [], _ => { });

    private static string ConfigurationJson(bool protectiveItemsEnabled) =>
        "{\"RarityRecolor\":{" +
        "\"enabled\":true,\"basis\":\"TraderTier\"," +
        "\"display\":{\"addColorToName\":true,\"addContextualLabelToPricesInfo\":true}," +
        "\"tiers\":{\"colors\":[\"default\",\"green\",\"blue\",\"violet\",\"orange\",\"red\"],\"traderBuyValuePerSlotCutoffs\":[10000,15000,20000,40000,60000]}," +
        "\"specializedClassifiers\":{" +
        "\"ammunition\":{\"enabled\":true,\"penetrationCutoffs\":[20,30,40,50,60]}," +
        "\"protectiveItems\":{\"enabled\":" + protectiveItemsEnabled.ToString().ToLowerInvariant() + "}," +
        "\"unarmoredRigs\":{\"enabled\":true,\"capacityCutoffs\":[8,12,16,20,24]}," +
        "\"backpacks\":{\"enabled\":true,\"capacityCutoffs\":[12,20,25,30,40]}," +
        "\"weapons\":{\"mode\":\"Inherit\"}}," +
        "\"customOverrides\":{\"itemIdToTier\":{}},\"blacklist\":{\"itemOrParentIds\":[]}}}";

    private static ProtectiveFixture LoadFixture() =>
        JsonSerializer.Deserialize<ProtectiveFixture>(
            File.ReadAllText(FindRepositoryFile("ItemInfo.Tests", "Fixtures", "protective-items.json")),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

    private static string FindRepositoryFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine([directory.FullName, .. parts]);
            if (File.Exists(candidate)) return candidate;
            directory = directory.Parent;
        }
        throw new FileNotFoundException($"Could not find repository file {Path.Combine(parts)}.");
    }

    private sealed record ProtectiveFixture(ProtectiveTemplateFixture[] Templates)
    {
        public ProtectiveItemType? Recognize(string itemId)
        {
            var templates = Templates.ToDictionary(template => template.Id);
            var template = templates[itemId];
            return ProtectiveItemAdapter.Recognize(
                new(template.Id, template.Name, template.ParentId, template.ArmorClass),
                id => templates.TryGetValue(id, out var parent) ? parent.ParentId : null);
        }

        public RecolorItem CreateRecolorItem(string itemId)
        {
            var templates = Templates.ToDictionary(template => template.Id);
            var template = templates[itemId];
            return ProtectiveItemAdapter.Create(
                new(template.Id, template.Name, template.ParentId, template.ArmorClass,
                    template.Grids?.Select(grid => (grid.CellsH, grid.CellsV)).ToArray(),
                    DefaultPresetComponents: template.DefaultPresetComponents?.Select(component =>
                        new ProtectiveArmorComponentData(component.SlotId, component.ArmorClass)).ToArray(),
                    RootSlotDefaults: template.RootSlotDefaults?.Select(component =>
                        new ProtectiveArmorComponentData(component.SlotId, component.ArmorClass)).ToArray(),
                    RequiredSlotIds: template.Slots?
                        .Where(slot => slot.Required)
                        .Select(slot => slot.Id)
                        .ToArray()),
                id => templates.TryGetValue(id, out var parent) ? parent.ParentId : null,
                traderTier: 2);
        }
    }

    private sealed record ProtectiveTemplateFixture(
        string Id,
        string Name,
        string ParentId,
        int? ArmorClass,
        ProtectiveGridFixture[]? Grids,
        ProtectiveArmorComponentFixture[]? DefaultPresetComponents,
        ProtectiveArmorComponentFixture[]? RootSlotDefaults,
        ProtectiveSlotFixture[]? Slots);
    private sealed record ProtectiveGridFixture(int CellsH, int CellsV);
    private sealed record ProtectiveArmorComponentFixture(string SlotId, int? ArmorClass);
    private sealed record ProtectiveSlotFixture(string Id, bool Required);
}
