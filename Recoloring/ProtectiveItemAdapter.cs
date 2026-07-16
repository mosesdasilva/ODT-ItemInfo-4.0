namespace ItemInfo.Recoloring;

public enum ProtectiveItemType { BodyArmor, Helmet, FaceCover, Visor, ArmorPlate, ArmoredEquipment, ArmoredRig }

public sealed record ProtectiveArmorComponentData(string SlotId, int? ArmorClass);

public sealed record ProtectiveTemplateData(
    string Id,
    string Name,
    string ParentId,
    int? DirectArmorClass,
    IReadOnlyList<(int Width, int Height)>? DirectGrids = null,
    IReadOnlyList<ProtectiveArmorComponentData>? DefaultPresetComponents = null,
    IReadOnlyList<ProtectiveArmorComponentData>? RootSlotDefaults = null);

public static class ProtectiveItemAdapter
{
    private static readonly IReadOnlyDictionary<string, ProtectiveItemType> ProtectiveBaseClasses =
        new Dictionary<string, ProtectiveItemType>(StringComparer.Ordinal)
        {
            ["5448e54d4bdc2dcc718b4568"] = ProtectiveItemType.BodyArmor,
            ["5a341c4086f77401f2541505"] = ProtectiveItemType.Helmet,
            ["5a341c4686f77469e155819e"] = ProtectiveItemType.FaceCover,
            ["5448e5724bdc2ddf718b4568"] = ProtectiveItemType.Visor,
            ["644120aa86ffbe10ee032b6f"] = ProtectiveItemType.ArmorPlate,
            ["57bef4c42459772e8d35a53b"] = ProtectiveItemType.ArmoredEquipment
        };
    private const string VestBaseClass = "5448e5284bdc2dcb718b4567";

    public static RecolorItem Create(ProtectiveTemplateData template, Func<string, string?> getParentId, int traderTier)
    {
        var recognizedType = Recognize(template, getParentId);
        var defaultFrontPlateClass = SelectArmorClassObservation(template.DefaultPresetComponents, IsFrontPlateSlot);
        var rootSlotDefaultFrontPlateClass = SelectArmorClassObservation(template.RootSlotDefaults, IsFrontPlateSlot);
        var defaultSoftArmorClass = SelectArmorClassObservation(AllDefaultArmorComponents(template), IsSoftArmorFrontSlot);
        var softArmorClass = IsValidArmorClass(defaultSoftArmorClass)
            ? defaultSoftArmorClass
            : IsValidArmorClass(template.DirectArmorClass)
                ? template.DirectArmorClass
                : defaultSoftArmorClass ?? template.DirectArmorClass;
        var directProtectiveType = UsesDirectRootArmorClass(recognizedType) ? recognizedType : null;
        var kind = recognizedType switch
        {
            ProtectiveItemType.ArmoredRig => RecolorItemKind.ArmoredRig,
            ProtectiveItemType.BodyArmor => RecolorItemKind.Armor,
            _ when directProtectiveType is not null => RecolorItemKind.Armor,
            _ when DescendsFrom(template.ParentId, VestBaseClass, getParentId) => RecolorItemKind.Rig,
            _ => RecolorItemKind.Normal
        };
        return new(
            template.Id,
            template.ParentId,
            kind,
            traderTier,
            Width: 1,
            Height: 1,
            ArmorClass: template.DirectArmorClass,
            SoftArmorClass: softArmorClass,
            DefaultFrontPlateClass: defaultFrontPlateClass,
            DirectGrids: template.DirectGrids,
            Name: template.Name,
            ProtectiveType: recognizedType,
            RootSlotDefaultFrontPlateClass: rootSlotDefaultFrontPlateClass);
    }

    public static ProtectiveItemType? Recognize(ProtectiveTemplateData template, Func<string, string?> getParentId)
    {
        foreach (var current in EnumerateAncestry(template.ParentId, getParentId))
        {
            if (current == VestBaseClass)
                return HasValidDefaultArmorData(template) ? ProtectiveItemType.ArmoredRig : null;
            if (ProtectiveBaseClasses.TryGetValue(current, out var type))
                return type;
        }
        return null;
    }

    private static bool DescendsFrom(string parentId, string baseClassId, Func<string, string?> getParentId)
    {
        return EnumerateAncestry(parentId, getParentId).Contains(baseClassId, StringComparer.Ordinal);
    }

    private static IEnumerable<string> EnumerateAncestry(string parentId, Func<string, string?> getParentId)
    {
        var current = parentId;
        var visited = new HashSet<string>(StringComparer.Ordinal);
        while (!string.IsNullOrEmpty(current) && visited.Add(current))
        {
            yield return current;
            current = getParentId(current);
        }
    }

    private static bool UsesDirectRootArmorClass(ProtectiveItemType? type) => type is
        ProtectiveItemType.FaceCover or
        ProtectiveItemType.Visor or
        ProtectiveItemType.ArmorPlate or
        ProtectiveItemType.ArmoredEquipment;

    private static int? SelectArmorClassObservation(
        IReadOnlyList<ProtectiveArmorComponentData>? components,
        Func<string, bool> slotMatches)
    {
        var matches = components?.Where(component => slotMatches(component.SlotId)).ToArray() ?? [];
        return matches.FirstOrDefault(component => IsValidArmorClass(component.ArmorClass))?.ArmorClass
            ?? matches.FirstOrDefault()?.ArmorClass;
    }

    private static bool IsFrontPlateSlot(string slotId) =>
        slotId.Equals("front_plate", StringComparison.OrdinalIgnoreCase) ||
        slotId.Equals("mod_slot_plate_front", StringComparison.OrdinalIgnoreCase);

    private static bool IsSoftArmorFrontSlot(string slotId) =>
        slotId.Equals("soft_armor_front", StringComparison.OrdinalIgnoreCase);

    private static bool IsValidArmorClass(int? armorClass) => armorClass is >= 1 and <= 6;

    private static bool HasValidDefaultArmorData(ProtectiveTemplateData template) =>
        IsValidArmorClass(SelectArmorClassObservation(template.DefaultPresetComponents, IsFrontPlateSlot)) ||
        IsValidArmorClass(SelectArmorClassObservation(template.RootSlotDefaults, IsFrontPlateSlot)) ||
        IsValidArmorClass(SelectArmorClassObservation(AllDefaultArmorComponents(template), IsSoftArmorFrontSlot));

    private static IReadOnlyList<ProtectiveArmorComponentData> AllDefaultArmorComponents(
        ProtectiveTemplateData template) =>
        (template.DefaultPresetComponents ?? []).Concat(template.RootSlotDefaults ?? []).ToArray();
}
