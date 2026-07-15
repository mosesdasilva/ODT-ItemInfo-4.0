# Trader Buy Value and Equipment-Aware Background Recoloring

> Historical specification: superseded for Recoloring and Release vNext by
> [GitHub issue #24](https://github.com/mosesdasilva/ODT-ItemInfo-4.0/issues/24)
> and its child implementation tickets. Retained as prior design history; do
> not use its conflicting tier, warning, or release decisions for vNext work.

## Problem Statement

ODT Item Info currently derives most item background colors from trader acquisition tiers and uses handbook value only as a limited fallback. That does not communicate the practical value of loot in a trader-only or no-flea-market playthrough. It can also select a trader without comparing all eligible offers, exclude Peacekeeper because his offer is denominated in dollars, and allow Fence to become an undesirable fallback.

Price is also the wrong signal for several equipment categories. Ammunition is better understood by penetration, armor by its shipped protection class, and storage equipment by the space it provides. Players need these meanings expressed through ODT Item Info's familiar colors without requiring dynamic client-side or per-instance calculations.

## Solution

Extend ODT Item Info with a selectable static Trader Buy Value Background Recolor Basis. At server startup, compare every Eligible Trader except Fence, normalize non-rouble offers, select the highest offer, divide it by Inventory Footprint, and map that value to configurable ODT color tiers. Preserve Trader Tier as the alternative basis.

Add independently configurable classifiers for Ammo Penetration Tier, Armor Class Tier, Rig Capacity Tier, and Backpack Capacity Tier. Preserve ODT's existing colors, explicit Custom Rarity Override behavior, and Recolor Blacklist. Make the Flea Ban Warning optional and disabled by default. Run all classification once during the delayed post-database Static Recolor Pass.

## User Stories

1. As a trader-only player, I want item backgrounds based on trader resale value, so that loot colors remain useful without the flea market.
2. As an existing ODT Item Info user, I want to switch between Trader Buy Value and Trader Tier, so that I can retain the original behavior whenever I prefer it.
3. As a player, I want the best Eligible Trader offer used, so that an item is not undervalued because the first matching trader pays less.
4. As a player, I want Fence excluded from value comparison, so that Fence does not distort normal trader-selling decisions.
5. As a player, I want Peacekeeper included, so that dollar-denominated offers compete fairly with rouble offers.
6. As a player, I want non-rouble offers converted using the game's handbook exchange value, so that comparisons use the same economy data as SPT.
7. As a modded-trader user, I want compatible loaded traders included, so that added traders can become the best buyer when they accept an item.
8. As a player, I want trader acceptance and prohibition rules respected, so that impossible sales are not presented as available value.
9. As a player, I want value normalized per Inventory Footprint, so that compact valuable loot ranks above bulky loot with the same total price.
10. As a player, I want an Unsellable Item treated as zero roubles per slot, so that it receives Common rather than a misleading handbook or Fence value.
11. As a weapon looter, I want Base Weapon Value only, so that static colors are not inflated by default-preset components or attachments.
12. As a player, I want static full-condition template values, so that item colors are stable and predictable across inventories.
13. As a player, I want durability, remaining uses, and installed contents ignored, so that this server-only feature does not pretend to represent live instance value.
14. As a player, I want configurable Trader Buy Value cutoffs, so that I can tune colors to my economy.
15. As a player, I want exact price boundaries to enter the higher tier, so that `10,000`, `15,000`, `20,000`, `40,000`, and `60,000` have unambiguous results.
16. As an ammunition user, I want ammunition colored by penetration instead of price, so that its background communicates combat effectiveness.
17. As an ammunition user, I want penetration from 0 through 60 divided into ODT's six normal colors, so that progression is easy to understand.
18. As an ammunition user, I want penetration above 60 marked Overpowered, so that exceptional rounds are immediately recognizable.
19. As a compatibility-conscious user, I want ammunition penetration coloring independently switchable, so that another ammunition-coloring mod can take control.
20. As a player, I want armor colored by armor class, so that protection matters more than resale price.
21. As a plate-carrier user, I want removable armor classified from its Default Front Plate Class, so that the color represents the primary shipped protection.
22. As a plate-carrier user, I want back and side plates ignored for representative class, so that a high-class side plate does not inflate the entire carrier.
23. As a soft-armor user, I want armor without a front plate classified from its built-in soft-armor class, so that items such as PACA receive the correct tier.
24. As a player, I want armor classes 1 through 6 mapped directly from Common through Unobtainium, so that the relationship is consistent and memorable.
25. As an armored-rig user, I want armored rigs treated as armor, so that protection always determines their specialized color.
26. As an armored-rig user, I do not want storage capacity to classify armored rigs, so that armored and unarmored rigs remain distinct categories.
27. As a compatibility-conscious user, I want armor-class coloring independently switchable, so that disabling it returns armor to the selected normal basis.
28. As a rig user, I want unarmored rigs colored by Container Storage Capacity, so that their utility is visible at a glance.
29. As a backpack user, I want backpacks colored by Container Storage Capacity, so that larger bags receive higher tiers.
30. As a player, I want rig and backpack capacity scales separated, so that the narrower rig range is not judged against the broader backpack range.
31. As a player, I want every direct internal grid cell counted, so that capacity reflects total physical storage space.
32. As a player, I want grid filters and shape ignored, so that classification remains a simple total-cell measure.
33. As a compatibility-conscious user, I want rig-capacity coloring independently switchable, so that unarmored rigs can fall back to the selected normal basis.
34. As a compatibility-conscious user, I want backpack-capacity coloring independently switchable, so that backpacks can fall back independently.
35. As a no-flea-market player, I want Flea Ban Warning disabled by default, so that flea restrictions do not override useful trader-focused colors.
36. As a flea-market player, I want an optional Flea Ban Warning, so that banned items can be marked Overpowered when I care about that restriction.
37. As a player who enables the Flea Ban Warning, I want it to override calculated category colors, so that the warning is never hidden.
38. As a configuration author, I want Custom Rarity Override to remain above automatic rules, so that explicit item choices remain authoritative.
39. As a configuration author, I want the Recolor Blacklist to remain an absolute opt-out, so that blacklisted items are never changed, even by custom rarity.
40. As a mod user, I want malformed specialized item data to warn and fall back, so that one unusual item cannot stop server startup.
41. As a mod user, I want each invalid threshold section replaced independently with built-in defaults, so that valid sibling configuration remains active.
42. As a configuration author, I want non-ascending cutoffs rejected rather than silently sorted, so that mistakes are visible and intent is not rewritten.
43. As a mod user, I want all calculations performed after other mods register items and traders, so that static compatibility includes modded content.
44. As a mod user, I want recoloring performed once per startup, so that the feature remains lightweight and deterministic.
45. As an existing ODT user, I want existing tier names and colors preserved, so that this fork feels familiar.
46. As an existing ODT user, I want unrelated item-info features preserved, so that adopting the new recoloring system does not remove current functionality.
47. As a maintainer, I want warnings to identify the affected item or configuration section, so that bad data is actionable.
48. As a maintainer, I want automated behavioral coverage for precedence and boundaries, so that later changes cannot silently alter the agreed color rules.

## Implementation Decisions

- Keep the implementation server-side in C# and compatible with the fork's SPT 4.0 / .NET 9 baseline.
- Retain ODT Item Info's existing delayed post-database lifecycle and perform one Static Recolor Pass after loaded mods have registered content.
- Represent the normal Background Recolor Basis with `useTraderBuyPriceForRecolor`: `true` selects Trader Buy Value and `false` selects Trader Tier.
- Default `markFleaMarketBannedItemsAsOverpowered` to `false`.
- Default `usePenetrationForAmmoRecolor`, `useArmorClassForRecolor`, `useRigCapacityForRecolor`, and `useBackpackCapacityForRecolor` to `true`.
- Keep specialized toggles independent. A disabled classifier returns its category to the selected normal basis.
- Classify armored rigs as armor. They never enter Rig Capacity Tier; with armor classification disabled they use the selected normal basis.
- Determine Trader Buy Value from the highest Rouble-Normalized Offer among all Eligible Traders.
- Determine eligibility from the trader's accepted item categories and explicit prohibitions, applying the same rule to built-in and modded traders.
- Exclude Fence unconditionally.
- Use the trader's first static loyalty-level buy-price coefficient because the Static Recolor Pass has no player-profile context.
- Normalize USD, EUR, and modded non-rouble currencies using the corresponding handbook currency value available at startup.
- Use Inventory Footprint as width multiplied by height and divide the selected offer by that value.
- Guard invalid or zero footprint data through Specialized Classification Fallback rather than dividing by zero.
- Treat no eligible offer as zero roubles per slot; do not use handbook price or Fence as a value fallback.
- Value weapons as their root template only; exclude default-preset components and attachments.
- Use price boundaries `10000`, `15000`, `20000`, `40000`, and `60000` with `<` comparisons. Exact boundaries enter the next tier.
- Use ammo penetration boundaries `10`, `20`, `30`, `40`, `50`, and `60`; values above 60 are Overpowered.
- Map armor classes 1 through 6 directly to Common, Rare, Epic, Legendary, Uber, and Unobtainium.
- For removable armor, retrieve the Default Armor Preset by root template and use only the Default Front Plate Class.
- If no default front plate exists, use the root item's Soft-Armor Fallback Class.
- Never derive armor color from compatible maximum plates, back or side plates, current installed plates, or current durability.
- Calculate Container Storage Capacity by summing `cellsH * cellsV` for every direct internal grid.
- Ignore placement filters and internal grid shape when totaling capacity.
- Use rig boundaries `8`, `12`, `16`, `20`, and `24` as inclusive upper cutoffs.
- Use backpack boundaries `12`, `20`, `25`, `30`, and `40` as inclusive upper cutoffs.
- Store Trader Buy Value, Ammo Penetration Tier, Rig Capacity Tier, and Backpack Capacity Tier settings in separate `tiers.json` sections without replacing existing fallback settings.
- Validate expected cutoff count, numeric values, and strict ascending order independently per section.
- On invalid threshold configuration, warn and use built-in defaults for that section only. Do not silently sort values.
- On missing or invalid specialized item data, warn with item context and use the selected normal basis.
- Apply recoloring precedence as: Recolor Blacklist; Custom Rarity Override; enabled Flea Ban Warning; applicable enabled specialized classifier; selected normal Background Recolor Basis.
- Preserve all existing ODT color names, color values, description/name integration, translations, and unrelated information features unless a change is strictly required by this specification.
- Preserve upstream attribution and the mod metadata's MIT license declaration. Item Evaluation C# is also MIT-licensed behavioral prior art; retain required notices if any source is adapted.

## Testing Decisions

- Use one primary behavioral seam: execute the Static Recolor Pass against fixture-backed SPT services and assert externally visible item-template background colors and emitted warnings.
- Prefer this high seam over direct tests of private arithmetic helpers. Tests should describe player-visible classification behavior rather than implementation structure.
- Create deterministic fixtures for item templates, handbook currencies, built-in and modded traders, trader acceptance/prohibition rules, default armor presets, internal grids, configuration, and tier colors.
- Cover Trader Buy Value selection with multiple eligible traders, a higher Peacekeeper USD offer, a prohibited high-paying trader, a modded trader, Fence-only acceptance, and no eligible trader.
- Cover Inventory Footprint normalization and exact price boundary values immediately below, equal to, and above every cutoff.
- Cover Base Weapon Value by proving default-preset attachments do not change the output.
- Cover static behavior by proving durability, remaining uses, and live installed contents do not change the output.
- Cover all ammo penetration boundaries, including 60 and values above 60, plus disabled ammo classification.
- Cover armor classes 1 through 6, a mixed-class default preset whose side plate is higher than its front plate, a PACA-style soft-armor fallback, missing preset data, and disabled armor classification.
- Cover an armored rig to prove armor wins and rig capacity is never used, including when armor classification is disabled.
- Cover rig and backpack values immediately below, equal to, and above every capacity cutoff.
- Cover multi-grid capacity and prove filters do not reduce the physical-cell sum.
- Cover precedence explicitly: blacklist over custom rarity, custom rarity over Flea Ban Warning, Flea Ban Warning over specialized classifiers, and specialized classifiers over the normal basis.
- Cover configuration validation per section: missing section, wrong cutoff count, nonnumeric value, duplicate/non-ascending values, valid sibling sections, and built-in fallback warnings.
- Cover warning behavior for missing specialized data without failing the overall pass.
- Add a loader-level smoke test that verifies the delayed lifecycle invokes the behavioral seam once after data registration; keep detailed classification assertions at the single primary seam.
- The fork currently has no automated test suite, so establish a .NET 9 test project and run it in CI with the solution build.
- Require an SPT 4.0.13 server smoke test and an in-game inventory inspection before release, because compilation and logs alone do not verify rendered background colors.

## Out of Scope

- Dynamic, per-instance, or client-side recoloring.
- Recalculation based on current durability, remaining uses, stack size, installed attachments, equipped plates, or container contents.
- Flea-market price-based coloring.
- Using Fence as an Eligible Trader or fallback value.
- Handbook price as a substitute for an Unsellable Item.
- Default weapon preset valuation.
- Player-specific loyalty-level or profile-specific buy coefficients.
- Maximum-compatible-plate armor classification.
- Combining front, back, and side armor classes into an aggregate score.
- Treating armored rigs as capacity-classified rigs.
- Weighting restricted grid cells differently or calculating recursive capacity from nested containers.
- Reworking ODT's tier names, color palette, translations, or unrelated item-description features.
- Recreating undocumented Eyes of a Trader behavior beyond the approved capacity rules.

## Further Notes

- Upstream project: https://github.com/thuynguyentrungdang/ODT-ItemInfo-4.0
- Working fork: https://github.com/mosesdasilva/ODT-ItemInfo-4.0
- Behavioral reference: https://github.com/acidphantasm/itemvaluation-csharp
- GitHub does not currently detect a standalone license file in the fork, although the mod metadata declares MIT. Add or confirm a root license file before release.
- The installed SPT 4.0.13 data snapshot used during grooming contained 61 rigs spanning 6-25 direct storage cells and 44 normal backpacks spanning 6-48 cells; the approved capacity bands were chosen from that snapshot.
- The specification intentionally uses a static first-loyalty-level trader coefficient because the server-startup pass has no player profile. Player-specific pricing would require a different, dynamic architecture and remains out of scope.
