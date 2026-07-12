# Trader Buy Value Recolor Design Record

Status: Approved for specification

## Goal

Add a selectable Trader Buy Value basis for ODT Item Info's background recoloring while preserving the original Trader Tier basis and ODT color system. Add independent static classifiers for ammunition, armor, unarmored rigs, and backpacks.

## Configuration

- `useTraderBuyPriceForRecolor`: `true` selects Trader Buy Value; `false` preserves Trader Tier.
- `markFleaMarketBannedItemsAsOverpowered`: defaults to `false`.
- `usePenetrationForAmmoRecolor`: defaults to `true`.
- `useArmorClassForRecolor`: defaults to `true`.
- `useRigCapacityForRecolor`: defaults to `true`.
- `useBackpackCapacityForRecolor`: defaults to `true`.
- Disabling a specialized classifier sends that item category to the selected Background Recolor Basis.
- An armored rig never enters Rig Capacity Tier, even when armor coloring is disabled.
- Specialized thresholds live in separate sections of `tiers.json`; existing value-fallback thresholds remain intact.
- Each threshold section is validated independently. Missing, malformed, or non-ascending values warn and use built-in defaults for that section only.

## Trader Buy Value

- Run one Static Recolor Pass during the delayed post-database phase after loaded mods have registered their items and traders.
- Calculate from full-condition item templates, not live inventory instances.
- Compare all Eligible Traders, including modded traders, and select the highest Rouble-Normalized Offer.
- Exclude Fence unconditionally.
- Normalize Peacekeeper's dollars and any other non-rouble currency through the game's handbook exchange value.
- Divide the selected offer by Inventory Footprint.
- An Unsellable Item has a Trader Buy Value of zero roubles per slot and therefore Common.
- Do not fall back to handbook price or Fence.
- Weapons use Base Weapon Value only.
- Ignore durability, remaining uses, installed attachments, and other per-instance state.

## Trader Buy Value bands

| Tier | Roubles per slot |
| --- | ---: |
| Common | less than 10,000 |
| Rare | 10,000 to less than 15,000 |
| Epic | 15,000 to less than 20,000 |
| Legendary | 20,000 to less than 40,000 |
| Uber | 40,000 to less than 60,000 |
| Unobtainium | 60,000 or more |

Store `10000`, `15000`, `20000`, `40000`, and `60000` as boundaries and compare with `<` so exact boundaries enter the next tier.

## Ammunition

| Tier | Penetration |
| --- | ---: |
| Common | 0-10 |
| Rare | 11-20 |
| Epic | 21-30 |
| Legendary | 31-40 |
| Uber | 41-50 |
| Unobtainium | 51-60 |
| Overpowered | 61 or more |

Store the boundaries `10`, `20`, `30`, `40`, `50`, and `60` in an `ammoPenetration` section.

## Armor

| Armor class | Tier |
| ---: | --- |
| 1 | Common |
| 2 | Rare |
| 3 | Epic |
| 4 | Legendary |
| 5 | Uber |
| 6 | Unobtainium |

- Removable-plate armor uses the Default Front Plate Class from its Default Armor Preset.
- Back and side plates do not influence the tier.
- Armor without a default front plate uses its Soft-Armor Fallback Class.
- Missing armor data warns and uses the selected Background Recolor Basis.
- Armored rigs are armor; Armor Class Tier always wins over Rig Capacity Tier.

## Unarmored rigs and backpacks

Container Storage Capacity is the sum of `cellsH * cellsV` across all direct internal grids. Count all physical cells and ignore grid shape and item filters.

| Tier | Unarmored rig cells | Backpack cells |
| --- | ---: | ---: |
| Common | 8 or fewer | 12 or fewer |
| Rare | 9-12 | 13-20 |
| Epic | 13-16 | 21-25 |
| Legendary | 17-20 | 26-30 |
| Uber | 21-24 | 31-40 |
| Unobtainium | 25 or more | 41 or more |

Store rig boundaries `8`, `12`, `16`, `20`, and `24`; store backpack boundaries `12`, `20`, `25`, `30`, and `40`.

## Recoloring precedence

1. Recolor Blacklist: absolute opt-out.
2. Custom Rarity Override: highest rule for eligible items.
3. Enabled Flea Ban Warning: Overpowered.
4. Enabled category classifier: Ammo Penetration Tier, Armor Class Tier, Rig Capacity Tier, or Backpack Capacity Tier.
5. Selected Background Recolor Basis: Trader Buy Value or Trader Tier.

## Failure behavior

- Missing or invalid item data for a specialized classifier logs a warning and uses the selected Background Recolor Basis.
- Invalid threshold configuration logs a warning and uses built-in defaults for that section only.
- Configuration errors do not silently reorder values and do not prevent server startup.
