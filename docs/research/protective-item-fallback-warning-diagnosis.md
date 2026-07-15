# Protective Item fallback warning diagnosis

Research for [Explain every Protective Item fallback warning](https://github.com/mosesdasilva/ODT-ItemInfo-4.0/issues/17), completed 2026-07-12 against the installed SPT 4.0.13 / EFT 0.16.9.0.40087 database snapshot and representative WTT-ContentBackport data.

## Conclusion

The 37 captured warnings do not identify 37 items that need a new armor-value database. Every warning is produced by an adapter defect between SPT's item data and `StaticRecolorPass`:

- 28 modular body armors have a valid Default Front Plate Class, but their default presets use the case-sensitive slot name `Front_plate`; the adapter accepts only `front_plate` or `mod_slot_plate_front`.
- 8 soft-only body armors have a valid class 2 or class 3 `Soft_armor_front` child in their default preset; the adapter never reads that child and instead passes the root item's class `0` as the soft-armor class.
- 1 Christmas body-armor variant has no globals preset, but its root slot metadata still declares a default class 5 front plate and class 3 soft insert; the adapter has no slot-metadata path.

The visible warnings expose only part of the defect. Armored rigs, helmets, armored masks and face shields, visor or helmet armor attachments, and standalone plates normally bypass armor classification before a warning can be produced. They are recolored by rig capacity or the selected normal Background Recolor Basis even though their SPT data contains usable protection classes.

The follow-up is therefore an extraction-contract decision, not a hand-maintained ID lookup. That decision belongs to [Resolve the Protective Item armor-class extraction contract](https://github.com/mosesdasilva/ODT-ItemInfo-4.0/issues/18).

## Captured warning inventory

All 37 IDs in the [captured warning evidence](protective-item-warning-capture.txt) resolve to built-in SPT 4.0.13 body armor. Their root `armorClass` is `0`; the effective class is held by an installed component or default slot declaration.

| Item ID | SPT item | Root class | Default front | Soft front | Why the adapter warns |
| --- | --- | ---: | ---: | ---: | --- |
| `545cdb794bdc2d3a198b456a` | 6B43 Zabralo-Sh body armor (EMR) | 0 | 6 | 3 | Misses `Front_plate` casing |
| `5648a7494bdc2d9d488b4583` | PACA Soft Armor | 0 | - | 2 | Ignores soft-armor child |
| `59e7635f86f7742cbf2c1095` | BNTI Module-3M body armor | 0 | - | 2 | Ignores soft-armor child |
| `5ab8e4ed86f7742d8e50c7fa` | MF-UNTAR body armor | 0 | - | 3 | Ignores soft-armor child |
| `5ab8e79e86f7742d8b372e78` | BNTI Gzhel-K body armor | 0 | 5 | 3 | Misses `Front_plate` casing |
| `5b44cd8b86f774503d30cba2` | IOTV Gen4 body armor (Full Protection Kit, MultiCam) | 0 | 5 | 3 | Misses `Front_plate` casing |
| `5b44cf1486f77431723e3d05` | IOTV Gen4 body armor (Assault Kit, MultiCam) | 0 | 5 | 3 | Misses `Front_plate` casing |
| `5b44d0de86f774503d30cba8` | IOTV Gen4 body armor (High Mobility Kit, MultiCam) | 0 | 5 | 3 | Misses `Front_plate` casing |
| `5b44d22286f774172b0c9de8` | BNTI Kirasa-N body armor | 0 | 3 | 2 | Misses `Front_plate` casing |
| `5c0e51be86f774598e797894` | 6B13 assault armor (Flora) | 0 | 4 | 2 | Misses `Front_plate` casing |
| `5c0e541586f7747fa54205c9` | 6B13 M assault armor (Killa Edition) | 0 | 5 | 3 | Misses `Front_plate` casing |
| `5c0e57ba86f7747fa141986d` | 6B23-2 body armor (Mountain Flora) | 0 | 4 | 2 | Misses `Front_plate` casing |
| `5c0e5bab86f77461f55ed1f3` | 6B23-1 body armor (EMR) | 0 | 3 | 2 | Misses `Front_plate` casing |
| `5c0e5edb86f77461f55ed1f7` | BNTI Zhuk body armor (Press) | 0 | 3 | 2 | Misses `Front_plate` casing |
| `5c0e625a86f7742d77340f62` | BNTI Zhuk body armor (EMR) | 0 | 6 | 3 | Misses `Front_plate` casing |
| `5c0e655586f774045612eeb2` | HighCom Trooper TFO body armor (MultiCam) | 0 | 4 | 3 | Misses `Front_plate` casing |
| `5ca2151486f774244a3b8d30` | FORT Redut-M body armor | 0 | 5 | 3 | Misses `Front_plate` casing |
| `5ca21c6986f77479963115a7` | FORT Redut-T5 body armor (Smog) | 0 | 5 | 3 | Misses `Front_plate` casing |
| `5df8a2ca86f7740bfe6df777` | 6B2 body armor (Flora) | 0 | - | 2 | Ignores soft-armor child |
| `5e4abb5086f77406975c9342` | LBT-6094A Slick Plate Carrier (Black) | 0 | 6 | 3 | Misses `Front_plate` casing |
| `5e9dacf986f774054d6b89f4` | FORT Defender-2 body armor | 0 | 5 | 3 | Misses `Front_plate` casing |
| `5f5f41476bdad616ad46d631` | NPP KlASS Korund-VM body armor (Black) | 0 | 5 | 2 | Misses `Front_plate` casing |
| `6038b4b292ec1c3103795a0b` | LBT-6094A Slick Plate Carrier (Coyote Tan) | 0 | 6 | 3 | Misses `Front_plate` casing |
| `6038b4ca92ec1c3103795a0d` | LBT-6094A Slick Plate Carrier (Olive Drab) | 0 | 6 | 3 | Misses `Front_plate` casing |
| `607f20859ee58b18e41ecd90` | PACA Soft Armor (Rivals Edition) | 0 | - | 2 | Ignores soft-armor child |
| `609e8540d5c319764c2bc2e9` | NFM THOR Concealable Reinforced Vest body armor | 0 | 4 | 3 | Misses `Front_plate` casing |
| `60a283193cb70855c43a381d` | NFM THOR Integrated Carrier body armor | 0 | 6 | 3 | Misses `Front_plate` casing |
| `62a09d79de7ac81993580530` | DRD body armor | 0 | - | 3 | Ignores soft-armor child |
| `63737f448b28897f2802b874` | Hexatac HPC Plate Carrier (MultiCam Black) | 0 | 5 | - | Misses `Front_plate` casing |
| `64abd93857958b4249003418` | Interceptor OTV body armor (UCP) | 0 | 4 | 2 | Misses `Front_plate` casing |
| `64be79c487d1510151095552` | NPP KlASS Kora-Kulon body armor (Black) | 0 | - | 3 | Ignores soft-armor child |
| `64be79e2bf8412471d0d9bcc` | NPP KlASS Kora-Kulon body armor (EMR) | 0 | - | 3 | Ignores soft-armor child |
| `674d91ce6e862d5a95059ed6` | 6B13 M assault armor (Christmas Edition) | 0 | 5 | 3 | No globals preset; defaults exist in root slots |
| `67ab2eecfe82855dcc0f2af6` | Hexatac HPC Plate Carrier (MultiCam) | 0 | 5 | - | Misses `Front_plate` casing |
| `67ab2f28dafe3b22670c9116` | BNTI Kirasa-N body armor (Green) | 0 | 3 | 2 | Misses `Front_plate` casing |
| `67ab2f5adafe3b22670c911f` | FORT Redut-M body armor (SK Woodland) | 0 | 5 | 3 | Misses `Front_plate` casing |
| `67ab2f94dafe3b22670c912c` | HighCom Trooper TFO body armor (Coyote) | 0 | 4 | 3 | Misses `Front_plate` casing |

The contrast item `5c0e53c886f7747fa54205c7` (6B13 assault armor, EMR) uses lowercase `front_plate`, so the current literal happens to find it. That confirms the 28 failures are a casing defect rather than absent presets or plates.

## Exact adapter failure

### Protective Item detection is too narrow

[`ItemInfo.cs`](../../ItemInfo.cs#L798) calculates `isArmor` only with `BaseClasses.ARMOR`, calculates `isRig` independently with `BaseClasses.VEST`, and creates `ArmoredRig` only when both are true. SPT's official base-class constants define `ARMOR`, `VEST`, `HEADWEAR`, `FACE_COVER`, `VISORS`, `ARMOR_PLATE`, and `ARMORED_EQUIPMENT` as separate nodes. ([SPT 4.0.13 `BaseClasses.cs`](https://github.com/sp-tarkov/server-csharp/blob/4.0.13/Libraries/SPTarkov.Server.Core/Models/Enums/BaseClasses.cs))

The item hierarchy has one parent chain, so a VEST descendant is not also an ARMOR descendant. The `isArmor && isRig` branch is therefore unreachable for normal SPT data. An armored rig such as the WARTECH TV-110 is classified as `Rig`, and its storage capacity wins before its class 4 default front plate is considered.

The same gate excludes headwear, face covers, visors, direct Armored Equipment descendants, and Armor Plates. Those items do not generate the captured armor warning because `ClassifyArmor` is never called.

### Default preset lookup uses the wrong slot spelling

SPT's `PresetHelper.GetDefaultPreset` returns the cached default preset for a template, or `null` when none exists. ([SPT 4.0.13 `PresetHelper.cs`](https://github.com/sp-tarkov/server-csharp/blob/4.0.13/Libraries/SPTarkov.Server.Core/Helpers/PresetHelper.cs)) The adapter then scans the preset with this case-sensitive pattern:

```csharp
item.SlotId is "mod_slot_plate_front" or "front_plate"
```

The affected built-in presets use `Front_plate`. SPT slot IDs are data identifiers, not normalized enum values, and the installed snapshot contains both casing forms. The adapter must not assume the lowercase variant is universal.

### Soft armor is never adapted

The adapter constructs `RecolorItem` with both `ArmorClass` and `SoftArmorClass` set to `itemProperties.ArmorClass` ([`ItemInfo.cs`](../../ItemInfo.cs#L820)). For modular armor roots that value is `0`; the class belongs to the installed `Soft_armor_front` template in the default preset or root slot declaration.

[`StaticRecolorPass.ClassifyArmor`](../../Recoloring/StaticRecolorPass.cs#L44) correctly prefers `DefaultFrontPlateClass`, then `SoftArmorClass`, then `ArmorClass`, but it can only classify the values supplied to it. Because `0` is non-null, it also stops the null-coalescing chain before any later value, then fails the valid range check and emits the generic fallback warning.

### The no-preset case still has usable template data

The Christmas 6B13 M variant has no matching entry in `Globals.ItemPresets`, so `GetDefaultPreset` correctly returns `null`. Its root `Slots` nevertheless declare:

- `Front_plate` -> `SlotFilter.Plate` `656f611f94b480b8a500c0db`, class 5;
- `Soft_armor_front` -> `SlotFilter.Plate` `6575ea3060703324250610da`, class 3.

SPT exposes `Slots`, `SlotFilter.Plate`, and `armorClass` as typed template data. ([SPT 4.0.13 `TemplateItem.cs`](https://github.com/sp-tarkov/server-csharp/blob/4.0.13/Libraries/SPTarkov.Server.Core/Models/Eft/Common/Tables/TemplateItem.cs)) The current adapter reads none of those slot defaults when the preset is absent.

## Protective Item data shapes outside the warning list

| Protective Item type | Representative SPT shape | Current result |
| --- | --- | --- |
| Modular body armor | ARMOR root class `0`; preset/root `Front_plate` child has class 3-6; soft children may also exist | Warns for the 28 uppercase-slot presets |
| Soft-only body armor | ARMOR root class `0`; preset/root `Soft_armor_front` child has class 2-3; no front plate | Warns because the child is ignored |
| Armored rig | VEST root class `0`; default preset has `Front_plate` or `Soft_armor_front` children | Classified as ordinary Rig; armor failure is silent |
| Helmet | HEADWEAR root class `0`; default preset contains children such as `Helmet_top`, `Helmet_back`, and `Helmet_ears` with armor classes | Classified by normal basis; armor failure is silent |
| Armored mask | FACE_COVER item commonly has a direct root `armorClass` such as 1, 4, 5, or 6 | Classified by normal basis; armor failure is silent |
| Face shield or helmet applique | VISORS or another direct ARMORED_EQUIPMENT branch with a direct root `armorClass` | Classified by normal basis; armor failure is silent |
| Standalone plate or built-in insert | ARMOR_PLATE descendant with a direct root `armorClass` | Classified by normal basis; armor failure is silent |

Examples from the installed SPT snapshot include the FORT Kiver-M helmet, whose root class is `0` while its required `Helmet_top`, `Helmet_back`, and `Helmet_ears` preset children are class 3; the Shattered armored mask with root class 1; the Atomic Defense CQCM mask with root class 4; and Granit Br5 standalone plate `64afc71497cf3a403c01ff38` with root class 6.

## Representative modded data

WTT-ContentBackport proves that compatibility cannot be expressed as a built-in ID table:

- `68a9b601863d2a71fa0494ae`, Galvion Caiman Hybrid Ballistic Applique (MultiCam), is cloned under `ARMORED_EQUIPMENT` and declares root `armorClass: "4"`. The current gate treats it as normal despite complete protection data.
- `6943c85be2f21398e70378cc`, Tac-Kek SAPI Level III+ ballistic plate (Replica), is created under `ARMOR_PLATE` and declares root `armorClass: "1"`. Its compatible slot list deliberately contains `Front_plate`, `Back_plate`, `front_plate`, and `back_plate`, demonstrating that real mod data uses both casing conventions.

These are the mod's own source records, not inferred runtime behavior. They live in the installed `WTT-ContentBackport/db/CustomItems/ArmoredEquipment_config.json` and `WTT-ContentBackport/db/CustomItems/ArmorPlate_config_2.json` respectively.

## Warning and test implications

The existing warning is accurate only at the classifier boundary: it says no valid class reached `StaticRecolorPass`. It does not distinguish absent SPT data from adapter failure and includes only the ID. A later implementation should make the adapter observable enough to distinguish the attempted data sources, while retaining the agreed Specialized Classification Fallback.

The current unit test for missing armor data constructs a `RecolorItem` directly. It proves fallback behavior but bypasses every failing SPT adaptation step. The eventual compatibility matrix needs fixture-backed adapter coverage for:

1. both `Front_plate` and `front_plate` casing;
2. a soft-only preset;
3. root-slot defaults when no globals preset exists;
4. an armored VEST;
5. a modular helmet preset;
6. direct-class masks, face shields, and standalone plates;
7. equivalent modded descendants and direct ARMORED_EQUIPMENT children;
8. genuinely missing or out-of-range data that warns and inherits the selected Background Recolor Basis.

This research does not choose the ordered source precedence or the final helmet rule. Those are the human-facing decisions in [Resolve the Protective Item armor-class extraction contract](https://github.com/mosesdasilva/ODT-ItemInfo-4.0/issues/18).

## Primary evidence

- Captured server warnings: [`protective-item-warning-capture.txt`](protective-item-warning-capture.txt).
- Current SPT adapter: [`ItemInfo.cs`](../../ItemInfo.cs#L798).
- Classifier and fallback: [`Recoloring/StaticRecolorPass.cs`](../../Recoloring/StaticRecolorPass.cs#L34).
- Domain definitions: [`CONTEXT.md`](../../CONTEXT.md#L75).
- Official SPT 4.0.13 source: [`BaseClasses.cs`](https://github.com/sp-tarkov/server-csharp/blob/4.0.13/Libraries/SPTarkov.Server.Core/Models/Enums/BaseClasses.cs), [`PresetHelper.cs`](https://github.com/sp-tarkov/server-csharp/blob/4.0.13/Libraries/SPTarkov.Server.Core/Helpers/PresetHelper.cs), and [`TemplateItem.cs`](https://github.com/sp-tarkov/server-csharp/blob/4.0.13/Libraries/SPTarkov.Server.Core/Models/Eft/Common/Tables/TemplateItem.cs).
- Installed SPT 4.0.13 primary data: `SPT_Data/database/templates/items.json`, `SPT_Data/database/globals.json`, and `SPT_Data/database/locales/global/en.json` under the tested server.
- Representative mod source data: `WTT-ContentBackport/db/CustomItems/ArmoredEquipment_config.json` and `WTT-ContentBackport/db/CustomItems/ArmorPlate_config_2.json` under the tested server's `SPT/user/mods` directory.
- Tested SPT release identity: [SPT 4.0.13 release](https://github.com/sp-tarkov/build/releases/tag/4.0.13).
