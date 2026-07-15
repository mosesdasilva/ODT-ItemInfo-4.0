using ItemInfo.Recoloring;
using System.Text.Json;
using Xunit;

namespace ItemInfo.Tests;

public class CapacityClassificationIntegrationTests
{
    [Fact]
    public void Multiple_direct_grids_ignore_shape_and_filters_in_visible_capacity_output()
    {
        var fixture = LoadFixture();
        RecolorTier? visibleTier = null;
        RecolorPresentation? visiblePresentation = null;
        var request = new StaticRecolorRequest(
            ToRecolorItem(fixture.MultiGridRig),
            tier => visibleTier = tier,
            ApplyPresentation: presentation => visiblePresentation = presentation);

        new StaticRecolorPass(RecolorConfiguration.Defaults)
            .Run([request], _ => { });

        Assert.Equal(RecolorTier.Epic, visibleTier);
        Assert.Equal("blue", Assert.IsType<RecolorPresentation>(visiblePresentation).Color.BackgroundValue);
        Assert.Equal("Capacity Tier 3", visiblePresentation.ContextualLabel);
        Assert.Equal("RecolorCapacityTier", visiblePresentation.ContextualLabelTranslationKey);
    }

    [Fact]
    public void Every_fixture_capacity_boundary_uses_inclusive_upper_cutoffs()
    {
        var fixture = LoadFixture();
        var pass = new StaticRecolorPass(RecolorConfiguration.Defaults);

        foreach (var boundary in fixture.Boundaries)
        {
            for (var index = 0; index < boundary.Capacities.Length; index++)
            {
                var item = new RecolorItem(
                    $"fixture-{boundary.Kind}-{boundary.Capacities[index]}",
                    "fixture-parent",
                    Enum.Parse<RecolorItemKind>(boundary.Kind),
                    2,
                    DirectGrids: [(boundary.Capacities[index], 1)]);

                Assert.Equal((RecolorTier)(index + 1), pass.Classify(item).Tier);
            }
        }
    }

    [Theory]
    [InlineData(false, true, RecolorTier.Rare, RecolorTier.Uber)]
    [InlineData(true, false, RecolorTier.Uber, RecolorTier.Rare)]
    public void Rig_and_backpack_toggles_and_cutoffs_are_independently_functional(
        bool rigEnabled,
        bool backpackEnabled,
        RecolorTier expectedRig,
        RecolorTier expectedBackpack)
    {
        var configuration = RecolorConfiguration.Load(
            ConfigurationJson(rigEnabled, backpackEnabled, [1, 2, 3, 4, 5], [1, 2, 3, 4, 5]),
            [],
            _ => { });
        var pass = new StaticRecolorPass(configuration);

        Assert.Equal(expectedRig, pass.Classify(
            new RecolorItem("fixture-rig", "parent", RecolorItemKind.Rig, 2, DirectGrids: [(5, 1)])).Tier);
        Assert.Equal(expectedBackpack, pass.Classify(
            new RecolorItem("fixture-backpack", "parent", RecolorItemKind.Backpack, 2, DirectGrids: [(5, 1)])).Tier);
    }

    [Fact]
    public void Invalid_or_absent_grid_data_warns_once_per_item_and_inherits_the_selected_basis()
    {
        var fixture = LoadFixture();
        var warnings = new List<string>();
        var requests = fixture.InvalidItems.Select(item =>
            new StaticRecolorRequest(ToRecolorItem(item), _ => { })).ToArray();

        var results = new StaticRecolorPass(RecolorConfiguration.Defaults).Run(requests, warnings.Add);

        Assert.All(results, result => Assert.Equal(RecolorTier.Rare, result.Tier));
        Assert.Equal(fixture.InvalidItems.Length, warnings.Count);
        foreach (var item in fixture.InvalidItems)
            Assert.Single(warnings, warning => warning.Contains(item.Id) &&
                warning.Contains("selected Background Recolor Basis"));
    }

    [Fact]
    public void Armored_rigs_never_enter_capacity_classification()
    {
        var fixture = LoadFixture();
        var result = new StaticRecolorPass(RecolorConfiguration.Defaults)
            .Classify(ToRecolorItem(fixture.ArmoredRig));

        Assert.Equal(RecolorTier.Rare, result.Tier);
        Assert.Equal(RecolorContextualLabelKind.BackgroundBasis, result.ContextualLabelKind);
        Assert.Null(result.Warning);
    }

    private static RecolorItem ToRecolorItem(CapacityItemFixture item) =>
        RecolorItemAdapter.FromContainer(new(
            item.Id,
            "fixture-parent",
            Enum.Parse<RecolorItemKind>(item.Kind),
            item.TraderTier,
            false,
            null,
            Width: 1,
            Height: 1,
            DirectGrids: item.Grids,
            HasDefaultArmorData: item.HasDefaultArmorData));

    private static CapacityFixture LoadFixture() =>
        JsonSerializer.Deserialize<CapacityFixture>(
            File.ReadAllText(FindRepositoryFile("ItemInfo.Tests", "Fixtures", "capacity-items.json")),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

    private static string ConfigurationJson(
        bool rigEnabled,
        bool backpackEnabled,
        int[] rigCutoffs,
        int[] backpackCutoffs) =>
        "{\"RarityRecolor\":{" +
        "\"enabled\":true,\"basis\":\"TraderTier\"," +
        "\"display\":{\"addColorToName\":true,\"addContextualLabelToPricesInfo\":true}," +
        "\"tiers\":{\"colors\":[\"default\",\"green\",\"blue\",\"violet\",\"orange\",\"red\"],\"traderBuyValuePerSlotCutoffs\":[10000,15000,20000,40000,60000]}," +
        "\"specializedClassifiers\":{" +
        "\"ammunition\":{\"enabled\":true,\"penetrationCutoffs\":[20,30,40,50,60]}," +
        "\"unarmoredRigs\":{\"enabled\":" + rigEnabled.ToString().ToLowerInvariant() +
        ",\"capacityCutoffs\":[" + string.Join(',', rigCutoffs) + "]}," +
        "\"backpacks\":{\"enabled\":" + backpackEnabled.ToString().ToLowerInvariant() +
        ",\"capacityCutoffs\":[" + string.Join(',', backpackCutoffs) + "]}}," +
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

    private sealed record CapacityFixture(
        CapacityItemFixture MultiGridRig,
        CapacityItemFixture ArmoredRig,
        CapacityBoundaryFixture[] Boundaries,
        CapacityItemFixture[] InvalidItems);
    private sealed record CapacityItemFixture(
        string Id,
        string Kind,
        int TraderTier,
        ContainerGridTemplate[]? Grids,
        bool HasDefaultArmorData = false);
    private sealed record CapacityBoundaryFixture(string Kind, int[] Capacities);
}
