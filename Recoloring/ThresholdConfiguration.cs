using System.Text.Json;

namespace ItemInfo.Recoloring;

public static class ThresholdConfiguration
{
    public static double[] ReadAscending(JsonElement section, int count, double[] fallback, string name, Action<string> warn)
    {
        var values = new List<double>();
        if (section.ValueKind == JsonValueKind.Array)
            foreach (var value in section.EnumerateArray())
                if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number) && double.IsFinite(number))
                    values.Add(number);
                else
                    return Invalid(name, fallback, warn);
        if (values.Count != count || values.Zip(values.Skip(1)).Any(pair => pair.First >= pair.Second))
            return Invalid(name, fallback, warn);
        return values.ToArray();
    }

    public static RecolorThresholds Load(string json, Action<string> warn)
    {
		var defaults = RecolorThresholds.Defaults;
		try
		{
			using var document = JsonDocument.Parse(json);
			return new(
				Read(document.RootElement, "TRADER_BUY_VALUE", 5, defaults.TraderBuyValue, warn),
				Read(document.RootElement, "AMMO_PENETRATION", 6, defaults.AmmoPenetration, warn),
				Read(document.RootElement, "RIG_CAPACITY", 5, defaults.RigCapacity, warn),
				Read(document.RootElement, "BACKPACK_CAPACITY", 5, defaults.BackpackCapacity, warn));
		}
		catch (JsonException)
		{
			warn("[ItemInfo] tiers.json is malformed; using built-in recolor defaults for all threshold sections.");
			return defaults;
		}
    }

    private static double[] Read(JsonElement root, string name, int count, double[] fallback, Action<string> warn)
    {
        if (!root.TryGetProperty(name, out var section) || section.ValueKind != JsonValueKind.Array)
            return Invalid(name, fallback, warn);
        return ReadAscending(section, count, fallback, name, warn);
    }

    private static double[] Invalid(string name, double[] fallback, Action<string> warn)
    {
        warn($"[ItemInfo] Invalid or missing tiers.json section {name}; using built-in defaults for this section.");
        return [.. fallback];
    }
}
