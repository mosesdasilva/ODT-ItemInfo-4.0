namespace ItemInfo.Recoloring;

public readonly record struct ColorSpecification
{
    private static readonly IReadOnlyDictionary<string, (string Canonical, string Rgb)> NativeColors =
        new Dictionary<string, (string, string)>(StringComparer.OrdinalIgnoreCase)
        {
            ["blue"] = ("blue", "#1C4156"),
            ["yellow"] = ("yellow", "#686628"),
            ["green"] = ("green", "#152D00"),
            ["red"] = ("red", "#6D2418"),
            ["black"] = ("black", "#000000"),
            ["grey"] = ("grey", "#1D1D1D"),
            ["violet"] = ("violet", "#4C2A55"),
            ["orange"] = ("orange", "#3C1900"),
            ["tracerYellow"] = ("tracerYellow", "#FFFF92"),
            ["tracerGreen"] = ("tracerGreen", "#75FF81"),
            ["tracerRed"] = ("tracerRed", "#FF3C3C"),
            ["default"] = ("default", "#7F7F7F")
        };

    private ColorSpecification(string backgroundValue, string richTextRgb)
    {
        BackgroundValue = backgroundValue;
        RichTextRgb = richTextRgb;
    }

    public string BackgroundValue { get; }
    public string RichTextRgb { get; }

    public static bool TryParse(string? value, out ColorSpecification color)
    {
        color = default;
        if (value is null || value.Length == 0)
            return false;

        if (NativeColors.TryGetValue(value, out var native))
        {
            color = new(native.Canonical, native.Rgb);
            return true;
        }

        if (value[0] != '#' || value.Length is not (4 or 7) ||
            value.AsSpan(1).IndexOfAnyExcept("0123456789abcdefABCDEF") >= 0)
            return false;

        var normalized = value.Length == 4
            ? $"#{value[1]}{value[1]}{value[2]}{value[2]}{value[3]}{value[3]}"
            : value;
        normalized = normalized.ToUpperInvariant();
        color = new(normalized, normalized);
        return true;
    }

    public static ColorSpecification ParseDefault(string value) =>
        TryParse(value, out var color) ? color : throw new ArgumentException("Built-in Color Specification is invalid.", nameof(value));
}
