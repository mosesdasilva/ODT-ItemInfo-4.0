# Recolor Configuration vNext migration

vNext replaces every recoloring input with the single `RarityRecolor` object in
`config/config.json`. It does not auto-migrate the legacy schema.

## Clean replacement

1. Stop the SPT server.
2. Back up the old `config/config.json` outside the mod directory.
3. Delete the existing ODT Item Info mod directory and extract the vNext package into a clean directory.
4. Start from the shipped vNext `config/config.json`.
5. Reapply unrelated Item Info preferences manually from the backup.
6. Rebuild `RarityRecolor` with the vNext fields below; do not copy the legacy block wholesale.
7. Do not restore `tiers.json` or `tiers_hex.json`.

A legacy-only block emits one actionable warning and disables only background
recoloring for that startup. A complete vNext block remains authoritative when
stale legacy keys are present and emits one cleanup warning. Stale tier files
are ignored and produce one clean-replacement warning.

## Setting lineage

| Legacy setting | vNext location or behavior | Semantic change | Reason |
| --- | --- | --- | --- |
| `RarityRecolor.enabled` | `RarityRecolor.enabled` | None | Preserve the master recoloring switch. |
| `useTraderBuyPriceForRecolor` | `basis`: `TraderBuyValue` when true, `TraderTier` when false | Boolean becomes an explicit Background Recolor Basis. | Make the selected rule readable and extensible. |
| `addColorToName` | `display.addColorToName` | None | Group display behavior. |
| `addTierNameToPricesInfo` | `display.addContextualLabelToPricesInfo` | The displayed reason is contextual rather than an RPG rarity name. | Keep labels neutral and classifier-aware. |
| `customRarity` | `customOverrides.itemIdToTier` | Only Recolor Tiers 1 through 6 are valid. | Keep exactly six neutral tiers. |
| root `RarityRecolorBlacklist` | `RarityRecolor.blacklist.itemOrParentIds` | The absolute opt-out moves inside the sole Recolor Configuration. | Remove split ownership. |
| `fallbackValueBasedRecolor` | No direct equivalent; choose `basis` explicitly | Unbuyable items no longer switch to a hidden secondary rule. | One selected basis must explain normal classification. |
| `bypassAmmoRecolor` | No direct equivalent in this slice | Ammunition currently inherits the selected basis; its explicit classifier owns later behavior. | Remove overlapping bypass toggles. |
| `bypassKeysRecolor` | No direct equivalent; keys use the selected basis | Key behavior is no longer a hidden exception. | Keep the normal basis consistent. |
| `markFleaMarketBannedItemsAsOverpowered` | No direct equivalent; the later Flea Ban Warning owns this behavior | “Overpowered” is removed as a tier. | Flea restriction is contextual, not Tier 7. |
| `usePenetrationForAmmoRecolor` | No direct equivalent in this slice; later ammunition classifier `enabled` | The setting ships only with working classifier behavior. | No inert accepted fields. |
| `useArmorClassForRecolor` | No direct equivalent in this slice; later Protective Item classifier `enabled` | Protective recognition and extraction own the toggle. | No inert accepted fields. |
| `useRigCapacityForRecolor` | No direct equivalent in this slice; later unarmored-rig classifier `enabled` | Armored rigs are separated from capacity. | Avoid overlapping meanings. |
| `useBackpackCapacityForRecolor` | No direct equivalent in this slice; later backpack classifier `enabled` | The setting ships with its classifier. | No inert accepted fields. |
| `tiers.json` `COMMON`, `RARE`, `EPIC`, `LEGENDARY`, `UBER`, `UNOBTAINIUM` | `tiers.colors[0]` through `tiers.colors[5]` | Named rarity keys become six ordinal Recolor Tier Color Specifications. | Keep tiers neutral and ordered. |
| `tiers_hex.json` `COMMON`, `RARE`, `EPIC`, `LEGENDARY`, `UBER`, `UNOBTAINIUM` | `tiers.colors[0]` through `tiers.colors[5]` | Native names and hex share one semantic Color Specification. | Eliminate duplicate palettes and project rich text deterministically. |
| `tiers.json` or `tiers_hex.json` `OVERPOWERED` | No direct equivalent; later `fleaBanWarning.color` | Removed from the tier ladder. | Flea Restricted is contextual, not Tier 7. |
| `tiers.json` or `tiers_hex.json` `CUSTOM`, `CUSTOM2`, `UNKNOWN` | No direct equivalent | Removed extra pseudo-tiers. | The schema contains exactly six Recolor Tiers. |
| `tiers.json` `TRADER_BUY_VALUE` | `tiers.traderBuyValuePerSlotCutoffs` | Same five exact lower-bound transitions, validated locally. | Consolidate value thresholds. |
| `tiers.json` `AMMO_PENETRATION` | No direct equivalent in this slice; later ammunition `penetrationCutoffs` | Moves with its functional classifier. | No inert accepted fields. |
| `tiers.json` `RIG_CAPACITY` | No direct equivalent in this slice; later unarmored-rig `capacityCutoffs` | Moves with its functional classifier. | No inert accepted fields. |
| `tiers.json` `BACKPACK_CAPACITY` | No direct equivalent in this slice; later backpack `capacityCutoffs` | Moves with its functional classifier. | No inert accepted fields. |
| `COMMON_VALUE_FALLBACK`, `RARE_VALUE_FALLBACK`, `EPIC_VALUE_FALLBACK`, `LEGENDARY_VALUE_FALLBACK`, `UBER_VALUE_FALLBACK` in either tier file | No direct equivalent | Removed handbook/value fallback path. | Trader Buy Value cutoffs are authoritative only when that basis is selected. |

## Color Converter client requirement

Native Tarkov color names require no additional client plugin. Any configured
hex background requires Color Converter API 1.1.1 or newer on every EFT client,
including every Fika client. Keep it external: do not place its DLL in the
server mod, add a project reference, or declare it as an SPT server dependency.
