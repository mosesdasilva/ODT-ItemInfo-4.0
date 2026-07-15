# SPT weapon category mapping

## Conclusion

Weapon category selection must be driven primarily by `TemplateItem.Properties.WeapClass`, with narrowly ordered base-class ancestry checks for rocket launchers, revolvers, and signal/flare weapons. A template's immediate parent is not a reliable gameplay category.

The classifier's scope is every item template that descends from `BaseClasses.WEAPON` (`5422acb9af1c889c16000029`). Scope and ancestry checks must use the same recursive ancestry semantics as SPT's `ItemHelper`/`ItemBaseClassService`, so weapons introduced beneath modded intermediate base classes are included.

## Classification precedence

Apply these rules in order:

1. A descendant of `BaseClasses.ROCKET_LAUNCHER` (`67446d4f04141c10630604e7`) maps to config category `launcher`, regardless of `WeapClass`.
2. `WeapClass == "grenadeLauncher"` maps to `launcher`.
3. `WeapClass == "pistol"` plus ancestry from `BaseClasses.REVOLVER` (`617f1ef5e8b54b0998387733`) maps to `revolver`.
4. All other recognized `WeapClass` values use the direct mapping below.
5. `WeapClass == "specialWeapon"` plus ancestry from `BaseClasses.GRENADE_LAUNCHER` (`5447bedf4bdc2d87278b4568`) identifies a flare/signal weapon. It is deliberately excluded from the launcher category and uses Tier 1 without a warning.
6. Any in-scope weapon with a missing or unrecognized `WeapClass`, or a `specialWeapon` outside that recognized flare/signal ancestry after the rocket rule has run, emits one warning and inherits the selected basis.

The ordering is part of the contract. In particular, rocket ancestry must be evaluated before the general `specialWeapon` fallback.

## Mapping

| SPT discriminator | Required ancestry | Config category / behavior |
| --- | --- | --- |
| any `WeapClass` | `ROCKET_LAUNCHER` | `launcher` |
| `grenadeLauncher` | none | `launcher` |
| `pistol` | `REVOLVER` | `revolver` |
| `pistol` | otherwise | `pistol` |
| `smg` | none | `submachineGun` |
| `shotgun` | none | `shotgun` |
| `assaultCarbine` | none | `assaultCarbine` |
| `assaultRifle` | none | `assaultRifle` |
| `marksmanRifle` | none | `marksmanRifle` |
| `sniperRifle` | none | `sniperRifle` |
| `machinegun` | none | `machineGun` |
| `specialWeapon` | `GRENADE_LAUNCHER` | Excluded flare/signal weapon; Tier 1, no warning |
| missing, unrecognized, or other `specialWeapon` | none of the recognized cases above | Warn once and inherit the selected basis |

## Why immediate-parent classification fails

Vanilla SPT contains several direct conflicts between `_parent` and the category expressed by `WeapClass`:

- AGS-30 has parent `MACHINE_GUN`, but `WeapClass == "grenadeLauncher"`.
- M32 has parent `REVOLVER`, but `WeapClass == "grenadeLauncher"`.
- MTs-255 has parent `REVOLVER`, but `WeapClass == "shotgun"`.
- MP-18 has parent `SHOTGUN`, but `WeapClass == "marksmanRifle"`.
- Ordinary revolvers have parent `REVOLVER`, but use `WeapClass == "pistol"`; ancestry is required to split them from other pistols.
- Eight signal/flare items have parent `GRENADE_LAUNCHER`, but `WeapClass == "specialWeapon"`; treating the parent as authoritative would incorrectly grant launcher behavior.
- RShG-2 uses `WeapClass == "specialWeapon"`, but rocket-launcher ancestry requires it to map to `launcher`.

These conflicts also show why a single discriminator cannot cover the domain. `WeapClass` supplies the main taxonomy, while ancestry resolves the intentional exceptions.

## Vanilla SPT 4.0.13 validation

The ordered algorithm classifies all 163 vanilla weapon descendants in the validated SPT 4.0.13 template dataset:

| Result | Count |
| --- | ---: |
| `assaultRifle` | 45 |
| `pistol` | 26 |
| `revolver` | 3 |
| `shotgun` | 15 |
| `sniperRifle` | 9 |
| `assaultCarbine` | 9 |
| `marksmanRifle` | 10 |
| `submachineGun` | 22 |
| `machineGun` | 10 |
| `launcher` | 6 (5 grenade launchers, 1 rocket launcher) |
| Tier 1 flare/signal exclusion | 8 |
| Unknown fallback | 0 |
| **Total** | **163** |

Validation source:

- Local file: `C:\Games\SPT 4.0\4.0.13\Single Player Tarkov\SPT\SPT_Data\database\templates\items.json`
- SHA-256: `3F35BD85BC19C6C224E0DCC0A05120B7CBA412EC885C4F73613C615092A2FE96`

## Modded descendant and unknown fallback contract

The classifier must not assume that a weapon's immediate parent is one of SPT's built-in base-class IDs. A mod may place its template beneath one or more custom intermediate parents. Recursive ancestry from `WEAPON`, `ROCKET_LAUNCHER`, `REVOLVER`, and `GRENADE_LAUNCHER` preserves the same behavior for those descendants.

An in-scope modded weapon with a known `WeapClass` follows the same ordered rules as vanilla data. A missing or novel value must not crash classification or silently acquire a guessed category: emit one warning for that fallback case and inherit the selected basis. The known signal/flare case is not an error and therefore does not warn.

## Implementation constraints

- Use recursive base-class ancestry, matching `ItemBaseClassService.ItemHasBaseClass`; do not compare only `_parent`.
- Treat `weapClass` as optional input. The server model exposes it as a nullable string, so missing values are valid data to handle.
- Keep the precedence above explicit and test it with the conflicting vanilla examples.
- Do not derive the category from `WeapUseType`, inventory slots, or hard-coded item-ID allowlists.
- Do not fold signal/flare weapons into `launcher`; retain their Tier 1 exclusion.
- Deduplicate fallback warnings so an affected template does not generate repeated noise.
- The project currently references SPT 4.0.9, where the required constants are also present, but this mapping and its validation dataset target SPT 4.0.13.

## Primary sources

- [`BaseClasses.cs` at SPT 4.0.13](https://github.com/sp-tarkov/server-csharp/blob/4.0.13/Libraries/SPTarkov.Server.Core/Models/Enums/BaseClasses.cs) defines the base-class IDs used for weapon scope and exception ancestry.
- [`TemplateItem.cs` at SPT 4.0.13, lines 702-703](https://github.com/sp-tarkov/server-csharp/blob/4.0.13/Libraries/SPTarkov.Server.Core/Models/Eft/Common/Tables/TemplateItem.cs#L702-L703) models `WeapClass` as a nullable string serialized as `weapClass`.
- [`ItemBaseClassService.cs` at SPT 4.0.13](https://github.com/sp-tarkov/server-csharp/blob/4.0.13/Libraries/SPTarkov.Server.Core/Services/ItemBaseClassService.cs) recursively caches parent IDs and implements ancestry-based `ItemHasBaseClass` checks.
- [`Weapons.cs` at SPT 4.0.13](https://github.com/sp-tarkov/server-csharp/blob/4.0.13/Libraries/SPTarkov.Server.Core/Models/Enums/Weapons.cs) is the server's weapon-related enum source.
