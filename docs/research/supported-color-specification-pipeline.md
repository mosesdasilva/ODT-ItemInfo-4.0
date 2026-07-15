# Supported Color Specification pipeline

Research for [Determine the supported Color Specification pipeline](https://github.com/mosesdasilva/ODT-ItemInfo-4.0/issues/13), completed 2026-07-12 against SPT 4.0.13 / EFT 0.16.9.0.40087 and Color Converter API 1.1.1.

## Conclusion

The vNext `Color Specification` contract should accept either a native Tarkov `TaxonomyColor` name or an opaque hexadecimal color:

```text
ColorSpecification := NativeName | #RGB | #RRGGBB
```

Native names are accepted case-insensitively at configuration input and normalized to the exact EFT spelling. Hex input must include `#`, contain only ASCII hexadecimal digits, and be normalized to uppercase `#RRGGBB`; `#RGB` expands by duplicating each digit. Validation must reject surrounding whitespace rather than silently trim it.

The canonical native names are:

```text
blue
yellow
green
red
black
grey
violet
orange
tracerYellow
tracerGreen
tracerRed
default
```

Reject null, empty strings, JSON numbers, missing `#`, `#RGBA`, `#RRGGBBAA`, every other length, non-hex characters, CSS aliases such as `gray` or `purple`, and unknown names. On an invalid configured value, log a warning and substitute that field's built-in default before any item template is mutated; never send an invalid raw value to the client.

The contract intentionally excludes alpha. Color Converter API 1.1.1 advertises and syntactically accepts `#RRGGBBAA`, but its render patch does not preserve leading zeroes while reconstructing the encoded value. Some eight-digit colors therefore render as the wrong opaque RGB color and others produce no converted color. Transparent item backgrounds are not required by this project, so excluding all eight-digit values is the smallest stable contract.

## What SPT and EFT actually accept

SPT's server-side item model does not validate colors. `TemplateItemProperties.BackgroundColor` is a nullable `string`; its setter only interns the value, and System.Text.Json serializes it under `BackgroundColor`. The official source therefore accepts and forwards any string unchanged. ([SPT 4.0.13 `TemplateItem.cs`](https://github.com/sp-tarkov/server-csharp/blob/4.0.13/Libraries/SPTarkov.Server.Core/Models/Eft/Common/Tables/TemplateItem.cs))

The real constraint is the EFT client. The `BackgroundColor` JSON value is read as `JsonType.TaxonomyColor`. Inspection of the SPT 4.0.13 `Assembly-CSharp.dll` found exactly these enum members and values:

| Value | Canonical name | Vanilla background RGB |
| ---: | --- | --- |
| 0 | `blue` | `#1C4156` |
| 1 | `yellow` | `#686628` |
| 2 | `green` | `#152D00` |
| 3 | `red` | `#6D2418` |
| 4 | `black` | `#000000` |
| 5 | `grey` | `#1D1D1D` |
| 6 | `violet` | `#4C2A55` |
| 7 | `orange` | `#3C1900` |
| 8 | `tracerYellow` | `#FFFF92` after Color-to-Color32 clamping and rounding |
| 9 | `tracerGreen` | `#75FF81` after Color-to-Color32 clamping and rounding |
| 10 | `tracerRed` | `#FF3C3C` after Color-to-Color32 clamping and rounding |
| 11 | `default` | `#7F7F7F` |

The shipped SPT 4.0.13 item database uses nine of those names for `BackgroundColor`: `black`, `blue`, `default`, `green`, `grey`, `orange`, `red`, `violet`, and `yellow`. The three tracer names do not appear as vanilla item backgrounds, but they are defined `TaxonomyColor` values and pass the same client conversion path.

The first-party artifacts inspected were:

- `C:\Games\SPT 4.0\4.0.13\Single Player Tarkov\EscapeFromTarkov_Data\Managed\Assembly-CSharp.dll` (SHA-256 `FAEF6F0B9F142F9D047495EC3DCCFD5D6974AC048368DC7045955CF54B117982`)
- `C:\Games\SPT 4.0\4.0.13\Single Player Tarkov\EscapeFromTarkov_Data\Managed\UnityEngine.CoreModule.dll` (SHA-256 `F7CFB53EB5B6A652D6BEF329B33C154D8CCB0DE7448AC5BFA1318762E10F7241`)
- `C:\Games\SPT 4.0\4.0.13\Single Player Tarkov\SPT\SPT_Data\database\templates\items.json` (SHA-256 `3F35BD85BC19C6C224E0DCC0A05120B7CBA412EC885C4F73613C615092A2FE96`)
- SPTarkov.Server.Core 4.0.9, the package version referenced by this checkout's project file (SHA-256 `FD111AD229A3704B1EBBAB3F9735054B81A44F34CF021C6AD07CFAF3C06E55AE`)

## Color Converter API behavior

Color Converter API is a BepInEx client plugin, GUID `com.rairai.colorconverterapi.eft`. It prepends a custom Newtonsoft.Json converter for `TaxonomyColor` and patches EFT's `ToColor` method; it is not a server library. ([1.1.1 plugin registration](https://github.com/RaiRaiTheRaichu/ColorConverterAPI/blob/1.1.1/Plugin.cs), [1.1.1 usage documentation](https://github.com/RaiRaiTheRaichu/ColorConverterAPI/blob/1.1.1/README.md))

Its converter accepts:

- any `TaxonomyColor` name through case-insensitive `Enum.TryParse`;
- `#RGB`, expanded internally to `RRGGBB`;
- `#RRGGBB`;
- `#RRGGBBAA`, parsed as red, green, blue, alpha;
- a JSON integer token, cast directly to `TaxonomyColor`.

The exact string regex is `^#([A-Fa-f0-9]{6}|[A-Fa-f0-9]{8}|[A-Fa-f0-9]{3})$`. It does not trim. Empty/whitespace-only strings, null, malformed strings, and unexpected JSON token types throw `JsonSerializationException`. ([1.1.1 converter source](https://github.com/RaiRaiTheRaichu/ColorConverterAPI/blob/1.1.1/CustomColorConverter.cs))

Hex is not retained as a client-side string. The converter removes `#`, expands three-digit input, parses the digits into an `int`, adds the number of defined enum members to avoid collision, and casts the result to a pseudo-`TaxonomyColor`. The render patch subtracts that offset and turns the number back into hexadecimal before producing `UnityEngine.Color32`. ([1.1.1 render patch](https://github.com/RaiRaiTheRaichu/ColorConverterAPI/blob/1.1.1/ColorConverterPatch.cs))

That last reconstruction uses `ToString("X6")` and only handles strings of length six or eight. An eight-digit input beginning `00` collapses to six digits and is interpreted as opaque RGB; one beginning `01` through `0F` becomes seven digits and is not converted. Eight-digit values beginning `1` through `F` do reach the RGBA branch, but a format whose correctness depends on its red channel is not a supportable configuration contract. JSON integers are likewise implementation machinery, not a stable user format.

## Runtime storage and the two projections

The normalized configuration value should be stored once in memory as a semantic `Color Specification`, not as an unclassified raw string. It then needs two projections:

1. **Item background projection.** Write the canonical native name or canonical `#RRGGBB` to `TemplateItemProperties.BackgroundColor`. SPT stores and serializes that string unchanged. EFT handles native names itself; Color Converter API is required for the hex form.
2. **Rich-text projection.** Always write a canonical `#RRGGBB` into `<color=...>` tags. For a hex specification, use the normalized hex directly. For a native name, use the RGB mapping in the table above. Do not pass `default`, `tracerYellow`, or any other taxonomy name straight into Unity rich text and assume it shares EFT's taxonomy vocabulary.

This split is necessary in the current code. The same `TiersHex` value is assigned to `BackgroundColor` and appended to `tiersHexCode` in [`ItemInfo.cs`](../../ItemInfo.cs#L831), then used in rich-text tags in [`ItemInfo.cs`](../../ItemInfo.cs#L933) and [`Utils.cs`](../../Utils.cs#L385). A single normalized semantic value is correct; a single unprojected output string is not.

Normalization is in-memory only. The loader need not rewrite the user's JSON file. Background output preserves a native name when that is what the user selected, while rich-text output is always deterministic six-digit hex.

## Dependency decision

ODT Item Info has no compile-time dependency on Color Converter API. The project references only SPT packages in [`ODT-ItemInfo-4.0.csproj`](../../ODT-ItemInfo-4.0.csproj), and `ModMetadata.ModDependencies` is null in [`ItemInfo.cs`](../../ItemInfo.cs#L29). That metadata also would not load a BepInEx client plugin for EFT.

It does have a real client runtime dependency today. The default [`tiers_hex.json`](../../config/tiers_hex.json) contains hex for every tier, recoloring and name coloring are enabled by default in [`config.json`](../../config/config.json#L14), and the recolor path writes those values to every eligible item's `BackgroundColor`. The upstream 2.0.14 source does the same, and its Forge release correctly declares Color Converter API as required. ([upstream 2.0.14 loader and recolor source](https://github.com/thuynguyentrungdang/ODT-ItemInfo-4.0/blob/2.0.14/ItemInfo.cs), [upstream 2.0.14 colors](https://github.com/thuynguyentrungdang/ODT-ItemInfo-4.0/blob/2.0.14/config/tiers_hex.json), [Forge dependency metadata](https://forge.sp-tarkov.com/mod/2430/odts-item-info-spt-40))

The dependency is conditional in principle but required by the proposed feature contract:

- A configuration containing only native names can render item backgrounds without Color Converter API; rich text uses this mod's name-to-hex projection.
- Any configured hex background requires Color Converter API on every EFT client that consumes the modified templates.
- Because vNext explicitly promises arbitrary opaque hex and a server cannot reliably police every Fika client's plugin folder, the private release instructions must require Color Converter API 1.1.1 or newer on every client; any future Forge version should also declare it as a required dependency.
- Do not add Color Converter API as a NuGet/project reference, do not bundle its DLL, and do not represent it as an SPT server-mod dependency. A local preflight may detect `BepInEx/plugins/RaiRai.ColorConverterAPI.dll` and emit a precise warning, but distribution metadata remains the enforceable dependency declaration.

## Implementation acceptance contract

The later implementation is conformant when tests prove all of the following:

1. Every canonical native name above is accepted; casing variants normalize to the canonical spelling.
2. `#F90` normalizes to `#FF9900`; lowercase six-digit input normalizes to uppercase.
3. Whitespace, aliases, integers, missing `#`, wrong lengths, `#RGBA`, and `#RRGGBBAA` are rejected before item mutation.
4. Invalid fields warn and fall back independently to their built-in defaults.
5. Background projection preserves names or emits `#RRGGBB`; rich-text projection always emits mapped `#RRGGBB`.
6. No Color Converter assembly is linked or bundled; private release instructions declare the client dependency, and any future Forge metadata does the same.
