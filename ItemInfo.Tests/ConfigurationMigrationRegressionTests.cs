using ItemInfo.Recoloring;
using System.Text.Json;
using Xunit;

namespace ItemInfo.Tests;

public class ConfigurationMigrationRegressionTests
{
    [Fact]
    public void Fully_migrated_user_fixture_preserves_overrides_blacklist_and_unrelated_preferences()
    {
        var json = File.ReadAllText(TestRepository.FindFile("ItemInfo.Tests", "Fixtures", "migrated-user-config.json"));
        var warnings = new List<string>();

        var configuration = RecolorConfiguration.Load(json, [], warnings.Add);
        using var wholeConfiguration = JsonDocument.Parse(json);

        Assert.True(configuration.Enabled);
        Assert.Equal(BackgroundRecolorBasis.TraderBuyValue, configuration.Basis);
        Assert.False(configuration.Display.AddColorToName);
        Assert.Equal(4, configuration.CustomOverrides["migrated-override"]);
        Assert.Equal(["migrated-item", "migrated-parent"], configuration.Blacklist);
        Assert.Empty(warnings);
        Assert.Equal("fr", wholeConfiguration.RootElement.GetProperty("UserLocale").GetString());
        Assert.False(wholeConfiguration.RootElement.GetProperty("PricesInfo").GetProperty("enabled").GetBoolean());
        Assert.Equal("preserved", wholeConfiguration.RootElement.GetProperty("UnrelatedFixtureSetting").GetString());
    }

    [Fact]
    public void Clean_package_configuration_inputs_contain_only_vnext_runtime_files()
    {
        var configDirectory = Path.GetDirectoryName(TestRepository.FindFile("config", "config.json"))!;
        var runtimeFiles = Directory.GetFiles(configDirectory)
            .Select(file => Path.GetFileName(file)!)
            .OrderBy(fileName => fileName, StringComparer.Ordinal)
            .ToArray();
        var warnings = new List<string>();

        var configuration = RecolorConfiguration.Load(
            File.ReadAllText(Path.Combine(configDirectory, "config.json")),
            runtimeFiles.Where(fileName => fileName is "tiers.json" or "tiers_hex.json").ToArray(),
            warnings.Add);

        Assert.Equal(["bsgblacklist.json", "config.json", "translations.json"], runtimeFiles);
        Assert.True(configuration.Enabled);
        Assert.Empty(warnings);
    }

}
